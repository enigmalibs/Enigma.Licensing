using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The keys page: what it asks the library for, what it offers the user as a file name, what it does with
/// the password on the way through, and what it says when any of it fails.
/// </summary>
/// <remarks>
/// <para>
/// Every dependency is a double except the pair itself, which is a real ML-DSA-44 one — the cheapest key
/// this library can make, and the only way to hold a <see cref="LicensingKeyPair"/> at all, since the type's
/// constructor is internal to the library on purpose. It is generated once per test, in microseconds.
/// </para>
/// <para>
/// What these tests exist to catch is the class of mistake that still looks like a working page: a password
/// copy left in memory, an overlay that never comes down after a failure, a size sent for an algorithm that
/// ignores it, and a success reported for a save that threw.
/// </para>
/// </remarks>
public sealed class GenerateKeysViewModelTests
{
    private readonly LicensingKeyPair _pair = new KeyService().GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);

    private readonly FakeFileDialogService _files = new();
    private readonly FakeBusyOverlayService _busy = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeClipboardService _clipboard = new();

    [Fact]
    public void Algorithms_AreTheThreePostQuantumSetsThenTheThreeRsaDigests()
    {
        Assert.Collection(
            Build(out _).Algorithms,
            option => Assert.Equal(LicenseSignatureAlgorithm.MLDsa44, option.Algorithm),
            option => Assert.Equal(LicenseSignatureAlgorithm.MLDsa65, option.Algorithm),
            option => Assert.Equal(LicenseSignatureAlgorithm.MLDsa87, option.Algorithm),
            option => Assert.Equal(LicenseSignatureAlgorithm.RsaSha256, option.Algorithm),
            option => Assert.Equal(LicenseSignatureAlgorithm.RsaSha384, option.Algorithm),
            option => Assert.Equal(LicenseSignatureAlgorithm.RsaSha512, option.Algorithm));
    }

    /// <summary>
    /// The two defaults the plan fixes: post-quantum at the balanced parameter set, and — when RSA is chosen
    /// instead — the size the predecessor application defaulted to, not the library's 3072.
    /// </summary>
    [Fact]
    public void TheDefaults_ArePostQuantumAt65_AndRsaAt4096()
    {
        GenerateKeysViewModel viewModel = Build(out _);

        Assert.Equal(LicenseSignatureAlgorithm.MLDsa65, viewModel.SelectedAlgorithm.Algorithm);
        Assert.Equal(4096, viewModel.SelectedRsaKeySize);
        Assert.Equal([2048, 3072, 4096, 8192], viewModel.RsaKeySizes);
    }

    /// <summary>
    /// The size picker belongs to RSA alone: an ML-DSA parameter set fixes its own key size, so offering one
    /// would be offering a choice that does nothing.
    /// </summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44, false)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65, false)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa87, false)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256, true)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha384, true)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha512, true)]
    public void IsRsaSelected_FollowsTheChosenScheme(LicenseSignatureAlgorithm algorithm, bool expected)
    {
        GenerateKeysViewModel viewModel = Build(out _);

        viewModel.SelectedAlgorithm = SignatureAlgorithmOption.For(algorithm);

        Assert.Equal(expected, viewModel.IsRsaSelected);
    }

    [Fact]
    public void SelectedAlgorithm_SetToNull_IsIgnored()
    {
        GenerateKeysViewModel viewModel = Build(out _);

        viewModel.SelectedAlgorithm = null!;

        Assert.Equal(LicenseSignatureAlgorithm.MLDsa65, viewModel.SelectedAlgorithm.Algorithm);
    }

    [Fact]
    public async Task Generate_AsksForTheChosenSchemeAndSize_AndWritesToTheChosenPaths()
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        viewModel.SelectedAlgorithm = SignatureAlgorithmOption.For(LicenseSignatureAlgorithm.RsaSha384);
        viewModel.SelectedRsaKeySize = 8192;
        viewModel.PublicKeyPath = "/keys/acme.public.key.json";
        viewModel.PrivateKeyPath = "/keys/acme.private.key.json";

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.Equal(LicenseSignatureAlgorithm.RsaSha384, keys.Algorithm);
        Assert.Equal(8192, keys.RsaKeySizeBits);
        Assert.Equal("/keys/acme.public.key.json", keys.PublicKeyPath);
        Assert.Equal("/keys/acme.private.key.json", keys.PrivateKeyPath);
    }

    [Fact]
    public async Task Generate_ReportsTheFingerprint_AndKeepsItOnThePage()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        WithPaths(viewModel);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.Equal(_pair.PublicKey.KeyId, viewModel.LastKeyId);
        Assert.True(viewModel.HasResult);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.False(reported.IsError);
        Assert.Contains(_pair.PublicKey.KeyId, reported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_RaisesTheOverlay_AndTakesItDownAgain()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        WithPaths(viewModel);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.Single(_busy.Titles);
        Assert.Equal(1, _busy.Hides);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// The one that matters most: an overlay still up after a failure is an application the user has to kill.
    /// </summary>
    [Fact]
    public async Task Generate_WhenTheSaveFails_ReportsTheError_AndStillTakesTheOverlayDown()
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        keys.FailSave = new IOException("The disk is full.");
        WithPaths(viewModel);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.True(reported.IsError);
        Assert.Equal("The disk is full.", reported.Message);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.LastKeyId);
    }

    [Fact]
    public async Task Generate_WhenGenerationItselfFails_ReportsTheError()
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        keys.FailGeneration = new ArgumentOutOfRangeException("rsaKeySizeBits");
        WithPaths(viewModel);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.Equal(0, keys.Saves);
        Assert.False(_busy.IsShowing);
    }

    /// <summary>
    /// Both cases leave the private half unset; the first leaves both unset. Whichever is missing is flagged
    /// on its own field, and nothing is generated.
    /// </summary>
    /// <param name="publicKeyPath">What the public-key field holds, if anything.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("/keys/acme.public.key.json")]
    public async Task Generate_WithAPathMissing_FlagsTheFieldAndDoesNothing(string? publicKeyPath)
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        viewModel.PublicKeyPath = publicKeyPath;

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.Equal(publicKeyPath is null, viewModel.PublicKeyPathHasError);
        Assert.True(viewModel.PrivateKeyPathHasError);
        Assert.Equal(0, keys.Generations);
        Assert.Empty(_notifications.Notifications);
        Assert.False(_busy.IsShowing);
    }

    [Fact]
    public async Task APathBeingChosen_ClearsItsError()
    {
        GenerateKeysViewModel viewModel = Build(out _);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);
        Assert.True(viewModel.PublicKeyPathHasError);

        viewModel.PublicKeyPath = "/keys/acme.public.key.json";

        Assert.False(viewModel.PublicKeyPathHasError);
    }

    [Fact]
    public async Task Generate_WithNoPassword_WritesThePrivateHalfInTheClear()
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        WithPaths(viewModel);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.Null(keys.Password);
    }

    [Fact]
    public async Task Generate_WithAPassword_PassesIt_AndThenClearsItsOwnCopy()
    {
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        WithPaths(viewModel);
        viewModel.Password = "correct horse";
        viewModel.ConfirmPassword = "correct horse";

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        // What the library saw while it could still read it…
        Assert.Equal("correct horse", keys.PasswordText);

        // …and what is left in the array afterwards. The library never touches a password it is handed, so
        // this array being blank is the page having cleaned up after itself.
        Assert.NotNull(keys.Password);
        Assert.Equal(new char[13], keys.Password);
    }

    /// <summary>
    /// A password typed and not repeated correctly must not reach the library — a key encrypted with a
    /// password the user did not mean to type is a key they cannot open.
    /// </summary>
    [Fact]
    public void Generate_IsBlocked_WhileTheConfirmationDisagrees()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        WithPaths(viewModel);

        viewModel.Password = "correct horse";
        Assert.False(viewModel.GenerateKeyPairCommand.CanExecute(null));

        viewModel.ConfirmPassword = "correct hors";
        Assert.False(viewModel.GenerateKeyPairCommand.CanExecute(null));
        Assert.True(viewModel.HasPasswordMismatch);
        Assert.NotNull(viewModel.PasswordMismatchMessage);

        viewModel.ConfirmPassword = "correct horse";
        Assert.True(viewModel.GenerateKeyPairCommand.CanExecute(null));
        Assert.False(viewModel.HasPasswordMismatch);
        Assert.Null(viewModel.PasswordMismatchMessage);
    }

    /// <summary>
    /// The mismatch message stays away until the user has actually typed into the confirmation field: an
    /// error under a box nobody has reached yet is noise.
    /// </summary>
    [Fact]
    public void APasswordNotYetConfirmed_BlocksTheCommandWithoutShowingAnError()
    {
        GenerateKeysViewModel viewModel = Build(out _);

        viewModel.Password = "correct horse";

        Assert.False(viewModel.HasPasswordMismatch);
        Assert.False(viewModel.GenerateKeyPairCommand.CanExecute(null));
    }

    [Fact]
    public async Task Generate_IsBlocked_WhileOneIsAlreadyRunning()
    {
        // Asynchronous continuations, so neither side of the handshake resumes the other inline: the save
        // runs on the thread pool, and a synchronous continuation would nest this test inside it.
        TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GenerateKeysViewModel viewModel = Build(out FakeKeyService keys);
        WithPaths(viewModel);
        keys.DuringSave = async () =>
        {
            reached.SetResult();
            await release.Task;
        };

        Task running = viewModel.GenerateKeyPairCommand.ExecuteAsync(null);
        await reached.Task;

        Assert.True(viewModel.IsBusy);
        Assert.True(_busy.IsShowing);
        Assert.False(viewModel.GenerateKeyPairCommand.CanExecute(null));

        release.SetResult();
        await running;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.GenerateKeyPairCommand.CanExecute(null));
    }

    [Fact]
    public async Task Browsing_OffersTheConventionalNames_WhenNothingHasBeenChosenYet()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        _files.Answer("/keys/signing.public.key.json");
        _files.Answer("/keys/signing.private.key.json");

        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);
        await viewModel.BrowsePrivateKeyPathCommand.ExecuteAsync(null);

        Assert.Collection(
            _files.Requests,
            request =>
            {
                Assert.Equal("signing.public.key.json", request.SuggestedFileName);
                Assert.Null(request.SuggestedStartLocation);
            },
            request => Assert.Equal("signing.private.key.json", request.SuggestedFileName));
    }

    /// <summary>
    /// The second half is offered next to the first and named after it, so a pair ends up as two files with
    /// one name between them without the user typing it twice.
    /// </summary>
    [Fact]
    public async Task Browsing_ForTheSecondHalf_IsNamedAfterTheFirst_AndStartsBesideIt()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        viewModel.PublicKeyPath = "/keys/acme.public.key.json";

        await viewModel.BrowsePrivateKeyPathCommand.ExecuteAsync(null);

        FakeFileDialogService.SaveRequest request = Assert.Single(_files.Requests);
        Assert.Equal("acme.private.key.json", request.SuggestedFileName);
        Assert.Equal(Path.GetDirectoryName("/keys/acme.public.key.json"), request.SuggestedStartLocation);
    }

    [Fact]
    public async Task Browsing_ForTheSecondHalf_FallsBackToTheWholeNameWhenTheFirstDoesNotFollowTheConvention()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        viewModel.PrivateKeyPath = "/keys/issuer.json";

        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Equal("issuer.public.key.json", Assert.Single(_files.Requests).SuggestedFileName);
    }

    [Fact]
    public async Task Browsing_TakesThePathTheDialogReturns()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        _files.Answer("/elsewhere/chosen.public.key.json");

        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Equal("/elsewhere/chosen.public.key.json", viewModel.PublicKeyPath);
    }

    [Fact]
    public async Task Browsing_ThatIsCancelled_LeavesThePathAlone()
    {
        GenerateKeysViewModel viewModel = Build(out _);
        viewModel.PublicKeyPath = "/keys/acme.public.key.json";

        // Nothing primed, so the dialog answers with null — which is what a cancellation is.
        await viewModel.BrowsePublicKeyPathCommand.ExecuteAsync(null);

        Assert.Equal("/keys/acme.public.key.json", viewModel.PublicKeyPath);
    }

    [Fact]
    public async Task CopyingTheFingerprint_IsUnavailableUntilThereIsOne()
    {
        GenerateKeysViewModel viewModel = Build(out _);

        Assert.False(viewModel.CopyKeyIdCommand.CanExecute(null));
        Assert.False(viewModel.HasResult);

        WithPaths(viewModel);
        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.True(viewModel.CopyKeyIdCommand.CanExecute(null));

        await viewModel.CopyKeyIdCommand.ExecuteAsync(null);

        Assert.Equal([_pair.PublicKey.KeyId], _clipboard.Copied);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        FakeKeyService keys = new(_pair);

        Assert.Throws<ArgumentNullException>(() => new GenerateKeysViewModel(null!, _files, _busy, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateKeysViewModel(keys, null!, _busy, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateKeysViewModel(keys, _files, null!, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateKeysViewModel(keys, _files, _busy, null!, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateKeysViewModel(keys, _files, _busy, _notifications, null!));
    }

    private GenerateKeysViewModel Build(out FakeKeyService keys)
    {
        keys = new FakeKeyService(_pair);

        return new GenerateKeysViewModel(keys, _files, _busy, _notifications, _clipboard);
    }

    private static void WithPaths(GenerateKeysViewModel viewModel)
    {
        viewModel.PublicKeyPath = "/keys/acme.public.key.json";
        viewModel.PrivateKeyPath = "/keys/acme.private.key.json";
    }
}
