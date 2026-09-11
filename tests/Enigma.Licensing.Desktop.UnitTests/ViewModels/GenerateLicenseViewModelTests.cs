using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Models;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The generate page: what the form becomes on its way into a <see cref="LicenseRequest"/>, what it refuses
/// to sign, what a profile does and does not carry, and what it does with the password on the way through.
/// </summary>
/// <remarks>
/// <para>
/// Every dependency is a double here; the real library is exercised by <c>GenerateLicenseRoundTripTests</c>.
/// What this suite is for is the mapping — the place where a blank box has to become a <see langword="null"/>
/// rather than an empty string, and a chosen day has to become the right instant.
/// </para>
/// <para>
/// The failures worth catching are the ones that still look like a working page: a licence that quietly
/// expires a day early, a password copy left in memory, an overlay that never comes down, and a profile
/// that remembers a secret.
/// </para>
/// </remarks>
public sealed class GenerateLicenseViewModelTests
{
    private readonly FakeLicenseGenerator _generator = new();
    private readonly FakeLicenseProfileStore _profiles = new();
    private readonly FakeFileDialogService _files = new();
    private readonly FakeBusyOverlayService _busy = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeClipboardService _clipboard = new();

    [Fact]
    public async Task Generate_MapsTheFormOntoTheRequest()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "  Acme Suite 2.*  ";
        viewModel.Owner = "Contoso Ltd";
        viewModel.DeviceId = "9E3F0A";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseRequest request = Assert.IsType<LicenseRequest>(_generator.Request);
        Assert.Equal("Acme Suite 2.*", request.ProductId);
        Assert.Equal("Contoso Ltd", request.Owner);
        Assert.Equal("9E3F0A", request.DeviceId);
        Assert.Equal("/keys/acme.private.key.json", _generator.PrivateKeyPath);
        Assert.Equal("/licences/acme.license.json", _generator.OutputPath);

        // Nothing was set on the request that the page has no business deciding: the identifier and the issue
        // timestamp are the generator's to stamp.
        Assert.Null(request.Id);
        Assert.Null(request.CreatedAt);
    }

    /// <summary>
    /// A box left blank has to become <see langword="null"/>, not an empty string: the envelope omits a null
    /// field entirely, so the difference is whether a licence carries <c>"owner": ""</c> for the rest of its
    /// life.
    /// </summary>
    [Fact]
    public async Task Generate_TurnsEveryBlankOptionalIntoNothingAtAll()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.Owner = "   ";
        viewModel.DeviceId = string.Empty;

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseRequest request = Assert.IsType<LicenseRequest>(_generator.Request);
        Assert.Null(request.Owner);
        Assert.Null(request.DeviceId);
        Assert.Null(request.ValidFrom);
        Assert.Null(request.ValidUntil);
    }

    /// <summary>
    /// The rule that decides whether a customer can use their licence on its last day. The chosen day is
    /// included, so the window ends at midnight at the <i>end</i> of it — one day after the start of the day
    /// the picker returned.
    /// </summary>
    [Fact]
    public async Task Generate_IncludesTheLastDayOfTheWindow()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.ValidFromDate = new DateTime(2026, 1, 1, 17, 42, 0, DateTimeKind.Unspecified);
        viewModel.ValidUntilDate = new DateTime(2026, 12, 31, 9, 15, 0, DateTimeKind.Unspecified);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseRequest request = Assert.IsType<LicenseRequest>(_generator.Request);

        // The time of day the picker happened to carry is discarded on both ends.
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 1, 1)), request.ValidFrom);
        Assert.Equal(new DateTimeOffset(new DateTime(2027, 1, 1)), request.ValidUntil);
    }

    [Fact]
    public async Task Generate_AcceptsAWindowOfASingleDay()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.ValidFromDate = new DateTime(2026, 6, 1);
        viewModel.ValidUntilDate = new DateTime(2026, 6, 1);

        Assert.False(viewModel.HasWindowError);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        LicenseRequest request = Assert.IsType<LicenseRequest>(_generator.Request);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 1)), request.ValidFrom);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 2)), request.ValidUntil);
    }

    /// <summary>
    /// A window that ends before it starts is a contradiction rather than an omission, so it disables the
    /// button and says why under the dates — the one rule on this page that works the way the keys page's
    /// password confirmation does.
    /// </summary>
    [Fact]
    public async Task AWindowThatEndsBeforeItStarts_BlocksTheCommand()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.ValidFromDate = new DateTime(2026, 12, 31);
        viewModel.ValidUntilDate = new DateTime(2026, 1, 1);

        Assert.True(viewModel.HasWindowError);
        Assert.NotNull(viewModel.WindowErrorMessage);
        Assert.False(viewModel.GenerateLicenseCommand.CanExecute(null));

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);
        Assert.Equal(0, _generator.Generations);

        viewModel.ValidUntilDate = new DateTime(2027, 1, 1);

        Assert.False(viewModel.HasWindowError);
        Assert.Null(viewModel.WindowErrorMessage);
        Assert.True(viewModel.GenerateLicenseCommand.CanExecute(null));
    }

    /// <summary>
    /// One end of the window on its own is not a contradiction: each date is independently optional.
    /// </summary>
    /// <param name="from">Whether a start date is set.</param>
    /// <param name="until">Whether an end date is set.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OneEndOfTheWindow_IsNeverAnError(bool from, bool until)
    {
        GenerateLicenseViewModel viewModel = Ready();

        viewModel.ValidFromDate = from ? new DateTime(2026, 12, 31) : null;
        viewModel.ValidUntilDate = until ? new DateTime(2026, 1, 1) : null;

        Assert.False(viewModel.HasWindowError);
    }

    /// <summary>
    /// The product identifier is the one field a licence cannot do without. The button stays live and says so
    /// when it is pressed — the keys page's rule, because a disabled button explains nothing.
    /// </summary>
    [Fact]
    public async Task Generate_WithNoProduct_FlagsTheFieldAndSignsNothing()
    {
        GenerateLicenseViewModel viewModel = Ready();

        Assert.False(viewModel.ProductIdHasError);
        Assert.True(viewModel.GenerateLicenseCommand.CanExecute(null));

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.ProductIdHasError);
        Assert.NotNull(viewModel.ProductIdErrorMessage);
        Assert.Equal(0, _generator.Generations);
        Assert.Empty(_notifications.Notifications);
        Assert.False(_busy.IsShowing);

        viewModel.ProductId = "Acme";
        Assert.False(viewModel.ProductIdHasError);
    }

    /// <summary>Whitespace is not a product identifier.</summary>
    [Fact]
    public async Task Generate_WithABlankProduct_FlagsTheField()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "   ";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.ProductIdHasError);
        Assert.Equal(0, _generator.Generations);
    }

    /// <summary>
    /// Both paths are flagged the same way, on their own fields, and nothing is signed until both are there.
    /// </summary>
    /// <param name="privateKeyPath">What the key field holds, if anything.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("/keys/acme.private.key.json")]
    public async Task Generate_WithAPathMissing_FlagsTheFieldAndDoesNothing(string? privateKeyPath)
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme";
        viewModel.PrivateKeyPath = privateKeyPath;

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(privateKeyPath is null, viewModel.PrivateKeyPathHasError);
        Assert.True(viewModel.OutputPathHasError);
        Assert.Equal(0, _generator.Generations);
        Assert.Empty(_notifications.Notifications);

        viewModel.OutputPath = "/licences/acme.license.json";
        Assert.False(viewModel.OutputPathHasError);
    }

    [Fact]
    public async Task Generate_ReportsTheLicenceIdentifier_AndKeepsItOnThePage()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        _generator.LicenseId = "01JYQ7ZK3MTESTTESTTESTTEST";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("01JYQ7ZK3MTESTTESTTESTTEST", viewModel.LastLicenseId);
        Assert.Equal("378A86040C38330F", viewModel.LastKeyId);
        Assert.Equal("/licences/acme.license.json", viewModel.LastLicensePath);
        Assert.True(viewModel.HasResult);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.False(reported.IsError);
        Assert.Contains("01JYQ7ZK3MTESTTESTTESTTEST", reported.Message, StringComparison.Ordinal);
        Assert.Contains("/licences/acme.license.json", reported.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The licence is on disk by the time its identifier is read out of the payload, so a payload that cannot
    /// be read is still a licence that was issued. Reporting it as a failure would send the user looking for a
    /// file that is already there.
    /// </summary>
    [Fact]
    public async Task Generate_WithAPayloadItCannotRead_StillReportsSuccess()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        _generator.PayloadJson = "not json at all";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.Null(viewModel.LastLicenseId);
        Assert.True(viewModel.HasResult);
        Assert.False(Assert.Single(_notifications.Notifications).IsError);
    }

    [Fact]
    public async Task Generate_RaisesTheOverlay_AndTakesItDownAgain()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.Single(_busy.Titles);
        Assert.Equal(1, _busy.Hides);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// The one that matters most: an overlay still up after a failure is an application the user has to kill.
    /// </summary>
    [Fact]
    public async Task Generate_WhenSigningFails_ReportsTheError_AndStillTakesTheOverlayDown()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        _generator.Fail = new System.Security.Cryptography.CryptographicException("The password is wrong.");

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.True(reported.IsError);
        Assert.Equal("The password is wrong.", reported.Message);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.LastLicenseId);
        Assert.False(viewModel.HasResult);
    }

    [Fact]
    public async Task Generate_IsBlocked_WhileOneIsAlreadyRunning()
    {
        // Asynchronous continuations, so neither side of the handshake resumes the other inline: the signing
        // runs on the thread pool, and a synchronous continuation would nest this test inside it.
        TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        _generator.DuringGenerate = async () =>
        {
            reached.SetResult();
            await release.Task;
        };

        Task running = viewModel.GenerateLicenseCommand.ExecuteAsync(null);
        await reached.Task;

        Assert.True(viewModel.IsBusy);
        Assert.True(_busy.IsShowing);
        Assert.False(viewModel.GenerateLicenseCommand.CanExecute(null));

        release.SetResult();
        await running;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.GenerateLicenseCommand.CanExecute(null));
    }

    [Fact]
    public async Task Generate_WithNoPassword_SaysSo()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.Null(_generator.Password);
    }

    [Fact]
    public async Task Generate_WithAPassword_PassesIt_AndThenClearsItsOwnCopy()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.PrivateKeyPassword = "correct horse";

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        // What the library saw while it could still read it…
        Assert.Equal("correct horse", _generator.PasswordText);

        // …and what is left in the array afterwards. The library never touches a password it is handed, so
        // this array being blank is the page having cleaned up after itself.
        Assert.NotNull(_generator.Password);
        Assert.Equal(new char[13], _generator.Password);
    }

    /// <summary>
    /// The form is read before the first await, so editing the page while a signature is being produced
    /// cannot change what is being signed.
    /// </summary>
    [Fact]
    public async Task Generate_SignsTheFormAsItStoodWhenTheButtonWasPressed()
    {
        TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";
        viewModel.Owner = "Contoso Ltd";
        _generator.DuringGenerate = async () =>
        {
            reached.SetResult();
            await release.Task;
        };

        Task running = viewModel.GenerateLicenseCommand.ExecuteAsync(null);
        await reached.Task;

        viewModel.ProductId = "Something Else";
        viewModel.Owner = "Someone Else";

        release.SetResult();
        await running;

        LicenseRequest request = Assert.IsType<LicenseRequest>(_generator.Request);
        Assert.Equal("Acme", request.ProductId);
        Assert.Equal("Contoso Ltd", request.Owner);
    }

    [Fact]
    public void UseThisMachine_FillsInADeviceIdentifier()
    {
        GenerateLicenseViewModel viewModel = Build();

        viewModel.UseThisMachineCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.DeviceId));
        Assert.Equal(DeviceIdentifier.Generate(), viewModel.DeviceId);
    }

    [Fact]
    public async Task Browsing_ForTheKey_OpensRatherThanSaves()
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.PrivateKeyPath = "/keys/acme.private.key.json";
        _files.AnswerOpen("/elsewhere/other.private.key.json");

        await viewModel.BrowsePrivateKeyPathCommand.ExecuteAsync(null);

        FakeFileDialogService.OpenRequest request = Assert.Single(_files.OpenRequests);
        Assert.Equal(Path.GetDirectoryName("/keys/acme.private.key.json"), request.SuggestedStartLocation);
        Assert.Equal("/elsewhere/other.private.key.json", viewModel.PrivateKeyPath);
        Assert.Empty(_files.Requests);
    }

    /// <summary>
    /// A product identifier is not a file name — it can hold a wildcard, a slash or a colon. The wildcard goes
    /// and anything the platform forbids becomes a hyphen.
    /// </summary>
    /// <param name="productId">What the product field holds.</param>
    /// <param name="expected">The name the dialog should be offered.</param>
    [Theory]
    [InlineData("Acme", "Acme.license.json")]
    [InlineData("Acme Suite 2.*", "Acme Suite 2.license.json")]
    [InlineData("  ", "license.json")]
    [InlineData(null, "license.json")]
    public async Task Browsing_ForTheOutput_OffersANameDerivedFromTheProduct(string? productId, string expected)
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = productId;

        await viewModel.BrowseOutputPathCommand.ExecuteAsync(null);

        Assert.Equal(expected, Assert.Single(_files.Requests).SuggestedFileName);
    }

    [Fact]
    public async Task Browsing_ThatIsCancelled_LeavesThePathAlone()
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.OutputPath = "/licences/acme.license.json";
        viewModel.PrivateKeyPath = "/keys/acme.private.key.json";

        // Nothing primed, so both dialogs answer with null — which is what a cancellation is.
        await viewModel.BrowseOutputPathCommand.ExecuteAsync(null);
        await viewModel.BrowsePrivateKeyPathCommand.ExecuteAsync(null);

        Assert.Equal("/licences/acme.license.json", viewModel.OutputPath);
        Assert.Equal("/keys/acme.private.key.json", viewModel.PrivateKeyPath);
    }

    /// <summary>
    /// The invariant that makes profiles safe to keep next to the key they name: the password is not in the
    /// profile, and neither is the destination of one particular issue.
    /// </summary>
    [Fact]
    public async Task SavingAProfile_CapturesTheForm_AndNeitherThePasswordNorTheOutput()
    {
        GenerateLicenseViewModel viewModel = Filled();
        viewModel.PrivateKeyPassword = "correct horse";
        viewModel.OutputPath = "/licences/acme.license.json";
        _files.Answer("/profiles/acme.profile.json");

        await viewModel.SaveProfileCommand.ExecuteAsync(null);

        LicenseProfile saved = Assert.IsType<LicenseProfile>(_profiles.Saved);
        Assert.Equal("/profiles/acme.profile.json", _profiles.SavedPath);
        Assert.Equal("Acme Suite 2.*", saved.ProductId);
        Assert.Equal("Contoso Ltd", saved.Owner);
        Assert.Equal("9E3F0A", saved.DeviceId);
        Assert.Equal(new DateOnly(2026, 1, 1), saved.ValidFrom);
        Assert.Equal(new DateOnly(2026, 12, 31), saved.ValidUntil);
        Assert.Equal("/keys/acme.private.key.json", saved.PrivateKeyPath);

        // Nowhere for either of them to go, which is the point.
        Assert.Null(typeof(LicenseProfile).GetProperty("PrivateKeyPassword"));
        Assert.Null(typeof(LicenseProfile).GetProperty("OutputPath"));

        Assert.False(Assert.Single(_notifications.Notifications).IsError);
    }

    [Fact]
    public async Task SavingAProfile_OffersANameDerivedFromTheProduct()
    {
        GenerateLicenseViewModel viewModel = Filled();

        await viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal("Acme Suite 2.profile.json", Assert.Single(_files.Requests).SuggestedFileName);
    }

    [Fact]
    public async Task SavingAProfile_ThatIsCancelled_WritesNothing()
    {
        GenerateLicenseViewModel viewModel = Filled();

        await viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal(0, _profiles.Saves);
        Assert.Empty(_notifications.Notifications);
    }

    [Fact]
    public async Task LoadingAProfile_FillsTheForm()
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.PrivateKeyPassword = "for the previous key";
        _files.AnswerOpen("/profiles/acme.profile.json");
        _profiles.Profile = new LicenseProfile
        {
            ProductId = "Acme Suite 2.*",
            Owner = "Contoso Ltd",
            DeviceId = "9E3F0A",
            ValidFrom = new DateOnly(2026, 1, 1),
            ValidUntil = new DateOnly(2026, 12, 31),
            PrivateKeyPath = "/keys/acme.private.key.json",
        };

        await viewModel.LoadProfileCommand.ExecuteAsync(null);

        Assert.Equal("/profiles/acme.profile.json", _profiles.LoadedPath);
        Assert.Equal("Acme Suite 2.*", viewModel.ProductId);
        Assert.Equal("Contoso Ltd", viewModel.Owner);
        Assert.Equal("9E3F0A", viewModel.DeviceId);
        Assert.Equal(new DateTime(2026, 1, 1), viewModel.ValidFromDate);
        Assert.Equal(new DateTime(2026, 12, 31), viewModel.ValidUntilDate);
        Assert.Equal("/keys/acme.private.key.json", viewModel.PrivateKeyPath);

        // The profile just named a key, possibly a different one — so a password typed for the previous key is
        // no longer the password for anything.
        Assert.Null(viewModel.PrivateKeyPassword);

        Assert.False(Assert.Single(_notifications.Notifications).IsError);
    }

    /// <summary>Loading replaces every field rather than merging into what was there.</summary>
    [Fact]
    public async Task LoadingAProfile_ReplacesWhateverWasThere()
    {
        GenerateLicenseViewModel viewModel = Filled();
        _files.AnswerOpen("/profiles/other.profile.json");
        _profiles.Profile = new LicenseProfile { ProductId = "Other" };

        await viewModel.LoadProfileCommand.ExecuteAsync(null);

        Assert.Equal("Other", viewModel.ProductId);
        Assert.Null(viewModel.Owner);
        Assert.Null(viewModel.DeviceId);
        Assert.Null(viewModel.ValidFromDate);
        Assert.Null(viewModel.ValidUntilDate);
        Assert.Null(viewModel.PrivateKeyPath);
    }

    [Fact]
    public async Task LoadingAProfile_ThatCannotBeRead_IsReportedAndChangesNothing()
    {
        GenerateLicenseViewModel viewModel = Filled();
        _files.AnswerOpen("/profiles/broken.profile.json");
        _profiles.FailLoad = new ArgumentException("That is not a licence profile.");

        await viewModel.LoadProfileCommand.ExecuteAsync(null);

        FakeNotificationService.Notification reported = Assert.Single(_notifications.Notifications);
        Assert.True(reported.IsError);
        Assert.Equal("That is not a licence profile.", reported.Message);
        Assert.Equal("Acme Suite 2.*", viewModel.ProductId);
    }

    [Fact]
    public async Task SavingAProfile_ThatCannotBeWritten_IsReported()
    {
        GenerateLicenseViewModel viewModel = Filled();
        _files.Answer("/profiles/acme.profile.json");
        _profiles.FailSave = new IOException("The disk is full.");

        await viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
    }

    [Fact]
    public async Task LoadingAProfile_ThatIsCancelled_ReadsNothing()
    {
        GenerateLicenseViewModel viewModel = Build();

        await viewModel.LoadProfileCommand.ExecuteAsync(null);

        Assert.Null(_profiles.LoadedPath);
        Assert.Empty(_notifications.Notifications);
    }

    [Fact]
    public async Task CopyingTheIdentifier_IsUnavailableUntilThereIsOne()
    {
        GenerateLicenseViewModel viewModel = Ready();
        viewModel.ProductId = "Acme";

        Assert.False(viewModel.CopyLicenseIdCommand.CanExecute(null));
        Assert.False(viewModel.HasResult);

        await viewModel.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(viewModel.CopyLicenseIdCommand.CanExecute(null));

        string id = Assert.IsType<string>(viewModel.LastLicenseId);

        await viewModel.CopyLicenseIdCommand.ExecuteAsync(null);

        Assert.Equal([id], _clipboard.Copied);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(null!, _profiles, _files, _busy, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(_generator, null!, _files, _busy, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(_generator, _profiles, null!, _busy, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(_generator, _profiles, _files, null!, _notifications, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(_generator, _profiles, _files, _busy, null!, _clipboard));
        Assert.Throws<ArgumentNullException>(() => new GenerateLicenseViewModel(_generator, _profiles, _files, _busy, _notifications, null!));
    }

    private GenerateLicenseViewModel Build() =>
        new(_generator, _profiles, _files, _busy, _notifications, _clipboard);

    /// <summary>A page with both paths chosen, so that only the licence itself is left to vary.</summary>
    /// <returns>The page.</returns>
    private GenerateLicenseViewModel Ready() =>
        new(_generator, _profiles, _files, _busy, _notifications, _clipboard)
        {
            PrivateKeyPath = "/keys/acme.private.key.json",
            OutputPath = "/licences/acme.license.json",
        };

    /// <summary>A page with every field filled, for the profile tests.</summary>
    /// <returns>The page.</returns>
    private GenerateLicenseViewModel Filled()
    {
        GenerateLicenseViewModel viewModel = Build();
        viewModel.ProductId = "Acme Suite 2.*";
        viewModel.Owner = "Contoso Ltd";
        viewModel.DeviceId = "9E3F0A";
        viewModel.ValidFromDate = new DateTime(2026, 1, 1);
        viewModel.ValidUntilDate = new DateTime(2026, 12, 31);
        viewModel.PrivateKeyPath = "/keys/acme.private.key.json";

        return viewModel;
    }
}
