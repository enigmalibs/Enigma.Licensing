using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Models;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The generate page driven against the <b>real</b> library, writing real files to a temporary folder: the
/// phase's acceptance criterion is that a licence generated in the application validates in the application
/// and through the library's own API, and this is where that is actually shown.
/// </summary>
/// <remarks>
/// <para>
/// Only the platform seams are doubles — dialogs, overlay, info bar, clipboard. The generator, the key
/// service, the validator and the profile store are the ones the application ships with, so what these tests
/// exercise is the whole path from the page's fields to an <c>enigma-license/1</c> document on disk and back
/// through a verdict.
/// </para>
/// <para>
/// ML-DSA-44 throughout: the cheapest scheme the library has, and what is under test here is the page and the
/// documents, not the arithmetic. The encrypted case pays for one Argon2id derivation each way, which is the
/// deliberate cost of proving that a password typed into this page is the password that opens the key it signs
/// with.
/// </para>
/// </remarks>
public sealed class GenerateLicenseRoundTripTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("enigma-licensing-licences-").FullName;

    private readonly IKeyService _keys = new KeyService();
    private readonly ILicenseValidator _validator = new LicenseValidator();

    private readonly FakeFileDialogService _files = new();
    private readonly FakeBusyOverlayService _busy = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeClipboardService _clipboard = new();

    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;
    private readonly string _licensePath;

    /// <summary>Initializes a new instance of the <see cref="GenerateLicenseRoundTripTests"/> class.</summary>
    public GenerateLicenseRoundTripTests()
    {
        _publicKeyPath = Path.Combine(_directory, "acme.public.key.json");
        _privateKeyPath = Path.Combine(_directory, "acme.private.key.json");
        _licensePath = Path.Combine(_directory, "acme.license.json");
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ALicenceGeneratedOnThePage_ValidatesAgainstTheKeyThatSignedIt()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme Suite 2.*";
        viewModel.Owner = "Contoso Ltd";
        viewModel.NewFeature = "pro";
        viewModel.AddFeatureCommand.Execute(null);
        viewModel.NewClaimKey = "seats";
        viewModel.NewClaimValue = "25";
        viewModel.AddClaimCommand.Execute(null);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(_notifications.Notifications).IsError);
        Assert.True(File.Exists(_licensePath));

        LicenseValidationResult result =
            await _validator.ValidateAsync(_licensePath, _publicKeyPath, "Acme Suite 2.7", cancellationToken: Token);

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);

        License license = Assert.IsType<License>(result.License);
        Assert.Equal("Acme Suite 2.*", license.ProductId);
        Assert.Equal("Contoso Ltd", license.Owner);
        Assert.True(license.HasFeature("pro"));
        Assert.Equal("25", Assert.Contains("seats", license.Claims));

        // The identifier the page reported is the identifier inside the signed bytes.
        Assert.Equal(license.Id, viewModel.LastLicenseId);

        // And the fingerprint it reported is the pair's.
        LicensingKey publicKey = await _keys.LoadKeyAsync(_publicKeyPath, cancellationToken: Token);
        Assert.Equal(publicKey.KeyId, viewModel.LastKeyId);
    }

    /// <summary>
    /// The rule that decides whether a customer can work on the last day of their licence. Today chosen as the
    /// end of the window means the licence works today.
    /// </summary>
    [Fact]
    public async Task ALicenceValidUntilToday_IsStillValidToday()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.ValidFromDate = DateTime.Today;
        viewModel.ValidUntilDate = DateTime.Today;

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseValidationResult result =
            await _validator.ValidateAsync(_licensePath, _publicKeyPath, "Acme", cancellationToken: Token);

        Assert.True(result.IsValid);
    }

    /// <summary>The other side of the same rule: yesterday as the end of the window means expired.</summary>
    [Fact]
    public async Task ALicenceValidUntilYesterday_IsExpired()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.ValidUntilDate = DateTime.Today.AddDays(-1);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseValidationResult result =
            await _validator.ValidateAsync(_licensePath, _publicKeyPath, "Acme", cancellationToken: Token);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.Expired, result.Failure);
    }

    [Fact]
    public async Task ALicenceStartingTomorrow_IsNotYetValid()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.ValidFromDate = DateTime.Today.AddDays(1);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseValidationResult result =
            await _validator.ValidateAsync(_licensePath, _publicKeyPath, "Acme", cancellationToken: Token);

        Assert.Equal(LicenseFailure.NotYetValid, result.Failure);
    }

    /// <summary>
    /// A licence locked to this machine validates here — which is the whole of what the "this machine" button
    /// is for.
    /// </summary>
    [Fact]
    public async Task ALicenceLockedToThisMachine_ValidatesHere()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.UseThisMachineCommand.Execute(null);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True((await _validator.ValidateAsync(
            _licensePath, _publicKeyPath, "Acme", DeviceIdentifier.Generate(), Token)).IsValid);

        Assert.Equal(LicenseFailure.DeviceMismatch, (await _validator.ValidateAsync(
            _licensePath, _publicKeyPath, "Acme", "some other machine", Token)).Failure);
    }

    /// <summary>
    /// The password typed into the page is the password that opens the signing key — and nothing else does.
    /// </summary>
    [Fact]
    public async Task APasswordTypedOnThePage_OpensTheEncryptedSigningKey()
    {
        await WriteKeyPairAsync("correct horse");

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.PrivateKeyPassword = "correct horse";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(_notifications.Notifications).IsError);
        Assert.True((await _validator.ValidateAsync(
            _licensePath, _publicKeyPath, "Acme", cancellationToken: Token)).IsValid);
    }

    /// <summary>
    /// The wrong password is a failure of the cryptography, and it reaches the user as a message rather than
    /// as an application that disappears.
    /// </summary>
    [Fact]
    public async Task TheWrongPassword_IsReported_AndWritesNoLicence()
    {
        await WriteKeyPairAsync("correct horse");

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.PrivateKeyPassword = "wrong horse";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.False(File.Exists(_licensePath));
        Assert.False(viewModel.HasResult);
        Assert.False(_busy.IsShowing);
    }

    /// <summary>
    /// Signing with the <i>public</i> half is the mistake a hurried issuer makes, and the library refuses it.
    /// The page's job is to say so rather than to crash.
    /// </summary>
    [Fact]
    public async Task SigningWithThePublicHalf_IsReported()
    {
        await WriteKeyPairAsync();

        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.PrivateKeyPath = _publicKeyPath;

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.False(File.Exists(_licensePath));
    }

    [Fact]
    public async Task AMissingKeyFile_IsReported()
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// A profile written by one page and read by another produces the same licence — the whole point of
    /// keeping one. The password is typed again on the far side, because the profile does not carry it.
    /// </summary>
    [Fact]
    public async Task AProfileSavedOnOnePage_ReproducesTheLicenceOnAnother()
    {
        await WriteKeyPairAsync();

        string profilePath = Path.Combine(_directory, "acme.profile.json");
        ILicenseProfileStore profiles = new LicenseProfileStore();

        GenerateLicenseViewModel first = Build(profiles);
        first.ProductId = "Acme Suite 2.*";
        first.Owner = "Contoso Ltd";
        first.ValidUntilDate = DateTime.Today.AddYears(1);
        first.NewFeature = "pro";
        first.AddFeatureCommand.Execute(null);
        first.NewClaimKey = "seats";
        first.NewClaimValue = "25";
        first.AddClaimCommand.Execute(null);
        _files.Answer(profilePath);

        await first.SaveProfileCommand.ExecuteAsync(null);
        Assert.True(File.Exists(profilePath));

        GenerateLicenseViewModel second = Build(profiles);
        _files.AnswerOpen(profilePath);

        await second.LoadProfileCommand.ExecuteAsync(null);

        Assert.Equal("Acme Suite 2.*", second.ProductId);
        Assert.Equal("Contoso Ltd", second.Owner);
        Assert.Equal(DateTime.Today.AddYears(1), second.ValidUntilDate);
        Assert.Equal(["pro"], second.Features);
        Assert.Equal(_privateKeyPath, second.PrivateKeyPath);

        second.OutputPath = _licensePath;
        await second.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseValidationResult result =
            await _validator.ValidateAsync(_licensePath, _publicKeyPath, "Acme Suite 2.7", cancellationToken: Token);

        Assert.True(result.IsValid);

        License license = Assert.IsType<License>(result.License);
        Assert.Equal("Contoso Ltd", license.Owner);
        Assert.True(license.HasFeature("pro"));
        Assert.Equal("25", Assert.Contains("seats", license.Claims));
    }

    /// <summary>Generates a real pair and writes both halves, optionally encrypting the private one.</summary>
    /// <param name="password">The password to encrypt the private half with, or <see langword="null"/>.</param>
    /// <returns>A task that completes when both files exist.</returns>
    private Task WriteKeyPairAsync(string? password = null) =>
        _keys.SaveKeyPairAsync(
            _keys.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44),
            _publicKeyPath,
            _privateKeyPath,
            password?.ToCharArray(),
            Token);

    private GenerateLicenseViewModel Build(ILicenseProfileStore? profiles = null) =>
        new(
            new LicenseGenerator(),
            profiles ?? new LicenseProfileStore(),
            _files,
            _busy,
            _notifications,
            _clipboard)
        {
            PrivateKeyPath = _privateKeyPath,
            OutputPath = _licensePath,
        };
}
