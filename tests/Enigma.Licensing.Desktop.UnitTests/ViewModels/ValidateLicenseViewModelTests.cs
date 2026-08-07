using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The validate page against doubles: what it asks the validator, what it does with the answer, and what it
/// does with an answer that is an exception instead.
/// </summary>
/// <remarks>
/// <para>
/// The verdict itself is a fake's, so nothing here proves any cryptography — that is
/// <see cref="ValidateLicenseRoundTripTests"/>'s job. What these tests pin is the page: the arguments it passes,
/// the fields it flags, the result it clears before a new check, and the fact that a thrown exception becomes a
/// notification rather than a crash.
/// </para>
/// <para>
/// A temporary folder is still needed, because the page reads the licence document a second time itself to show
/// the <c>algorithm</c> and <c>keyId</c> fields. That read goes to the real file system whatever the validator
/// is, which is exactly why it has its own tests here: a document that is not an envelope, and one that is not
/// there at all, must both leave the card off rather than take the page down.
/// </para>
/// </remarks>
public sealed class ValidateLicenseViewModelTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("enigma-licensing-validate-").FullName;

    private readonly FakeLicenseValidator _validator = new();
    private readonly FakeFileDialogService _files = new();
    private readonly FakeNotificationService _notifications = new();

    private readonly string _licensePath;
    private readonly string _publicKeyPath;

    /// <summary>Initializes a new instance of the <see cref="ValidateLicenseViewModelTests"/> class.</summary>
    public ValidateLicenseViewModelTests()
    {
        _licensePath = Path.Combine(_directory, "acme.license.json");
        _publicKeyPath = Path.Combine(_directory, "acme.public.key.json");
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Constructor_RejectsANullValidator()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateLicenseViewModel(null!, _files, _notifications));

    [Fact]
    public void Constructor_RejectsNullFileDialogs()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateLicenseViewModel(_validator, null!, _notifications));

    [Fact]
    public void Constructor_RejectsNullNotifications()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateLicenseViewModel(_validator, _files, null!));

    [Fact]
    public void ANewPage_ShowsNoResult()
    {
        ValidateLicenseViewModel viewModel = Build();

        Assert.False(viewModel.HasVerdict);
        Assert.False(viewModel.HasSummary);
        Assert.False(viewModel.HasDocumentDetails);
        Assert.Null(viewModel.Verdict);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// The house rule, applied here: the button stays live with the fields empty, and pressing it says what is
    /// missing rather than leaving the user in front of something greyed out.
    /// </summary>
    [Fact]
    public void WithNoFilesChosen_TheButtonIsStillLive()
        => Assert.True(Build().ValidateLicenseCommand.CanExecute(null));

    [Fact]
    public async Task WithNoFilesChosen_PressingIt_FlagsBothFields_AndChecksNothing()
    {
        ValidateLicenseViewModel viewModel = Build(withLicense: false, withKey: false);

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.LicensePathHasError);
        Assert.True(viewModel.PublicKeyPathHasError);
        Assert.Equal(0, _validator.Validations);
        Assert.False(viewModel.HasVerdict);
        Assert.Empty(_notifications.Notifications);
    }

    [Fact]
    public async Task WithOnlyTheKeyChosen_OnlyTheLicenceFieldIsFlagged()
    {
        ValidateLicenseViewModel viewModel = Build(withLicense: false);

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.LicensePathHasError);
        Assert.False(viewModel.PublicKeyPathHasError);
    }

    [Fact]
    public async Task ChoosingAFile_ClearsItsFlag()
    {
        ValidateLicenseViewModel viewModel = Build(withLicense: false, withKey: false);
        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        viewModel.LicensePath = _licensePath;
        viewModel.PublicKeyPath = _publicKeyPath;

        Assert.False(viewModel.LicensePathHasError);
        Assert.False(viewModel.PublicKeyPathHasError);
    }

    [Fact]
    public async Task TheValidator_IsAskedAboutTheFilesAndTheExpectations()
    {
        ValidateLicenseViewModel viewModel = Build();
        viewModel.ExpectedProductId = "Acme Suite";
        viewModel.ExpectedDeviceId = "abc123";

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(1, _validator.Validations);
        Assert.Equal(_licensePath, _validator.LicensePath);
        Assert.Equal(_publicKeyPath, _validator.PublicKeyPath);
        Assert.Equal("Acme Suite", _validator.ProductId);
        Assert.Equal("abc123", _validator.DeviceId);
    }

    /// <summary>
    /// An empty expectation has to reach the library as <see langword="null"/> and not as an empty string: null
    /// skips the check, and <c>""</c> would be a product no licence is for.
    /// </summary>
    /// <param name="typed">What the user left in the field.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyExpectation_SkipsTheCheck(string? typed)
    {
        ValidateLicenseViewModel viewModel = Build();
        viewModel.ExpectedProductId = typed;
        viewModel.ExpectedDeviceId = typed;

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Null(_validator.ProductId);
        Assert.Null(_validator.DeviceId);
    }

    [Fact]
    public async Task AnExpectation_IsTrimmedBeforeItIsUsed()
    {
        ValidateLicenseViewModel viewModel = Build();
        viewModel.ExpectedProductId = "  Acme Suite  ";
        viewModel.ExpectedDeviceId = "  abc123 ";

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("Acme Suite", _validator.ProductId);
        Assert.Equal("abc123", _validator.DeviceId);
    }

    [Fact]
    public async Task AValidLicence_IsShownAsAVerdictAndASummary()
    {
        _validator.Result = LicenseSamples.Valid(LicenseSamples.Licence(productId: "Acme Suite 2.*"));

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasVerdict);
        Assert.Equal(LicenseVerdictSeverity.Success, viewModel.Verdict!.Severity);
        Assert.True(viewModel.HasSummary);
        Assert.Equal("Acme Suite 2.*", viewModel.Summary!.ProductId);
        Assert.Empty(_notifications.Notifications);
    }

    /// <summary>
    /// The whole reason the card and the verdict are separate: a licence that failed is still worth reading, and
    /// on a product mismatch reading it is the answer.
    /// </summary>
    [Fact]
    public async Task AFailedLicenceWhosePayloadWasReadable_IsStillShown()
    {
        _validator.Result = LicenseSamples.Invalid(
            LicenseFailure.ProductMismatch,
            "The license is for 'Acme Suite 1.*', not 'Acme Suite 2.0'.",
            LicenseSamples.Licence(productId: "Acme Suite 1.*"));

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(LicenseVerdictSeverity.Warning, viewModel.Verdict!.Severity);
        Assert.True(viewModel.HasSummary);
        Assert.Equal("Acme Suite 1.*", viewModel.Summary!.ProductId);
    }

    /// <summary>A failure that never got as far as the payload has nothing to show, and must not pretend to.</summary>
    [Fact]
    public async Task AFailureBeforeThePayload_ShowsNoSummary()
    {
        _validator.Result = LicenseSamples.Invalid(LicenseFailure.InvalidSignature);

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasVerdict);
        Assert.False(viewModel.HasSummary);
        Assert.Null(viewModel.Summary);
    }

    /// <summary>
    /// The document's own two fields, read straight off the file — the diagnosis on a signature failure, and the
    /// one part of the page that does not come from the validator.
    /// </summary>
    [Fact]
    public async Task TheDocumentsOwnFields_AreReadFromTheFile()
    {
        await WriteEnvelopeAsync(algorithm: "ML-DSA-87", keyId: "378A86040C38330F");

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasDocumentDetails);
        Assert.Equal("ML-DSA-87", viewModel.DocumentAlgorithm);
        Assert.Equal("378A86040C38330F", viewModel.DocumentKeyId);
    }

    /// <summary>A fingerprint is optional in the format, so a document without one says so in words.</summary>
    [Fact]
    public async Task ADocumentWithNoFingerprint_SaysSo()
    {
        await WriteEnvelopeAsync(algorithm: "RSA-SHA256", keyId: null);

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasDocumentDetails);
        Assert.Equal("Not stamped", viewModel.DocumentKeyId);
    }

    /// <summary>
    /// A file that is not an envelope leaves the document card off. The verdict says what happened; a second
    /// failed parse must not add anything of its own.
    /// </summary>
    [Fact]
    public async Task AFileThatIsNotAnEnvelope_LeavesTheDocumentCardOff()
    {
        await File.WriteAllTextAsync(_licensePath, "{ \"this\": \"is not a licence\" }", Token);
        _validator.Result = LicenseSamples.Invalid(LicenseFailure.InvalidFormat);

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasVerdict);
        Assert.False(viewModel.HasDocumentDetails);
        Assert.Null(viewModel.DocumentAlgorithm);
        Assert.Empty(_notifications.Notifications);
    }

    /// <summary>
    /// The validator would have thrown before the page got here, so this can only happen if the file went away
    /// mid-check. It still may not take the page down.
    /// </summary>
    [Fact]
    public async Task AMissingFile_LeavesTheDocumentCardOff()
    {
        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasVerdict);
        Assert.False(viewModel.HasDocumentDetails);
    }

    /// <summary>
    /// The library throws for the application's own mistakes — a missing file, a key that is not a key, the
    /// private half where the public one belongs. Every one of those is a notification, and none of them is a
    /// verdict.
    /// </summary>
    [Fact]
    public async Task AThrownFailure_IsReported_AndIsNotAVerdict()
    {
        _validator.Fail = new ArgumentException("Verifying needs the public half of the pair.");

        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.True(reported.IsError);
        Assert.Contains("public half", reported.Message, StringComparison.Ordinal);
        Assert.False(viewModel.HasVerdict);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task AThrownFailure_LeavesNoStaleResultOnScreen()
    {
        await WriteEnvelopeAsync();
        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasVerdict);
        Assert.True(viewModel.HasSummary);
        Assert.True(viewModel.HasDocumentDetails);

        _validator.Fail = new FileNotFoundException("Gone.");
        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasVerdict);
        Assert.False(viewModel.HasSummary);
        Assert.False(viewModel.HasDocumentDetails);
    }

    /// <summary>A second check replaces the first one's result rather than adding to it.</summary>
    [Fact]
    public async Task ASecondCheck_ReplacesTheFirstResult()
    {
        ValidateLicenseViewModel viewModel = Build();

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);
        Assert.Equal(LicenseVerdictSeverity.Success, viewModel.Verdict!.Severity);

        _validator.Result = LicenseSamples.Invalid(LicenseFailure.Expired, license: LicenseSamples.Licence());
        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(LicenseVerdictSeverity.Warning, viewModel.Verdict!.Severity);
        Assert.Equal("Expired", viewModel.Verdict.Headline);
    }

    /// <summary>While a check runs the page says so, and a second click cannot land on top of the first.</summary>
    [Fact]
    public async Task WhileACheckRuns_TheCommandIsClosed()
    {
        ValidateLicenseViewModel viewModel = Build();
        bool busyDuring = false;
        bool executableDuring = true;

        _validator.DuringValidate = () =>
        {
            busyDuring = viewModel.IsBusy;
            executableDuring = viewModel.ValidateLicenseCommand.CanExecute(null);

            return Task.CompletedTask;
        };

        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(busyDuring);
        Assert.False(executableDuring);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.ValidateLicenseCommand.CanExecute(null));
    }

    /// <summary>The form is snapshotted before the check, so editing it mid-check cannot change what was asked.</summary>
    [Fact]
    public async Task AFormEditedMidCheck_DoesNotChangeWhatWasChecked()
    {
        ValidateLicenseViewModel viewModel = Build();

        _validator.DuringValidate = () =>
        {
            viewModel.ExpectedProductId = "Something else";

            return Task.CompletedTask;
        };

        viewModel.ExpectedProductId = "Acme Suite";
        await viewModel.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("Acme Suite", _validator.ProductId);
    }

    [Fact]
    public async Task BrowsingForALicence_FillsTheField()
    {
        _files.AnswerOpen(_licensePath);
        ValidateLicenseViewModel viewModel = Build(withLicense: false);

        await viewModel.BrowseLicensePathCommand.ExecuteAsync(null);

        Assert.Equal(_licensePath, viewModel.LicensePath);
    }

    [Fact]
    public async Task BrowsingForAKey_FillsTheField()
    {
        _files.AnswerOpen(_publicKeyPath);
        ValidateLicenseViewModel viewModel = Build(withKey: false);

        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Equal(_publicKeyPath, viewModel.PublicKeyPath);
    }

    /// <summary>A cancelled dialog leaves the field as it was — it is not a request to clear it.</summary>
    [Fact]
    public async Task ACancelledDialog_LeavesTheFieldAlone()
    {
        ValidateLicenseViewModel viewModel = Build();

        await viewModel.BrowseLicensePathCommand.ExecuteAsync(null);
        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Equal(_licensePath, viewModel.LicensePath);
        Assert.Equal(_publicKeyPath, viewModel.PublicKeyPath);
    }

    /// <summary>
    /// The two documents of a check normally live in the same folder, so each dialog opens where the other file
    /// already is.
    /// </summary>
    [Fact]
    public async Task EachDialog_OpensWhereTheOtherFileIs()
    {
        ValidateLicenseViewModel viewModel = Build(withLicense: false);

        await viewModel.BrowseLicensePathCommand.ExecuteAsync(null);

        Assert.Equal(_directory, Assert.Single(_files.OpenRequests).SuggestedStartLocation);
    }

    [Fact]
    public async Task WithNothingChosenYet_ADialogOpensWhereverThePlatformWould()
    {
        ValidateLicenseViewModel viewModel = Build(withLicense: false, withKey: false);

        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(_files.OpenRequests).SuggestedStartLocation);
    }

    /// <summary>
    /// The button behind the device field: the same identifier the generate page would have stamped into a
    /// licence, which is what makes reproducing a device lock possible at all.
    /// </summary>
    [Fact]
    public void ThisMachine_FillsTheDeviceField()
    {
        ValidateLicenseViewModel viewModel = Build();

        viewModel.UseThisMachineCommand.Execute(null);

        Assert.Equal(DeviceIdentifier.Generate(), viewModel.ExpectedDeviceId);
    }

    /// <summary>Writes a real envelope at the licence path, so the page has a document to read its fields from.</summary>
    /// <param name="algorithm">The scheme the document names.</param>
    /// <param name="keyId">The fingerprint it is stamped with, if any.</param>
    /// <returns>A task that completes when the file exists.</returns>
    private async Task WriteEnvelopeAsync(string algorithm = "ML-DSA-65", string? keyId = "378A86040C38330F")
    {
        LicenseEnvelope envelope = new()
        {
            Format = LicenseEnvelope.CurrentFormat,
            Algorithm = algorithm,
            KeyId = keyId,
            Payload = [0x01, 0x02, 0x03],
            Signature = [0x04, 0x05, 0x06],
        };

        await using FileStream output = File.Create(_licensePath);

        await envelope.SaveAsync(output, Token);
    }

    private ValidateLicenseViewModel Build(bool withLicense = true, bool withKey = true) =>
        new(_validator, _files, _notifications)
        {
            LicensePath = withLicense ? _licensePath : null,
            PublicKeyPath = withKey ? _publicKeyPath : null,
        };
}
