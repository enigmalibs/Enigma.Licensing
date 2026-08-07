using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// The validate page: checking a licence against a public key, and showing both the verdict and what the
/// licence says.
/// </summary>
/// <remarks>
/// <para>
/// This is the page the issuer opens when a customer says the licence does not work. It answers two questions
/// at once, which is why the result is a card rather than a notification: <i>is this document authentic</i>,
/// and <i>what does it actually say</i>. The second half is often the whole answer — a licence that is genuine
/// and simply names another product looks identical to a broken one until you can read it.
/// </para>
/// <para>
/// <b>Verdicts go to the card; exceptions go to the info bar.</b> That is the library's own split, honoured
/// here: nothing a <i>licence</i> can be makes the validator throw, so every one of the seven failures is a
/// verdict and belongs on the page where it can be read twice. A missing file, a public key file that is not a
/// key, or one holding the private half are the opposite — the application's own mistake, reported as an error
/// notification and leaving the previous result alone.
/// </para>
/// <para>
/// <b>The expected product and device are optional, and leaving them empty means <i>skipping</i> the check,
/// not failing it.</b> The page says so under each field. That matters most for the device: a licence locked
/// to one machine validates anywhere if nothing is passed, which is exactly the mistake this page exists to
/// let an issuer reproduce.
/// </para>
/// <para>
/// Nothing here is expensive enough to move off the UI thread or to hide behind the blocking overlay: a public
/// key is never encrypted, so there is no key derivation to pay for, and verifying a signature is microseconds
/// either side of the two small file reads. <see cref="IsBusy"/> exists to stop a second click landing on top
/// of the first, not to cover a wait.
/// </para>
/// </remarks>
public sealed class ValidateLicenseViewModel : ObservableObject
{
    private readonly ILicenseValidator _validator;
    private readonly IFileDialogService _files;
    private readonly INotificationService _notifications;

    /// <summary>Initializes a new instance of the <see cref="ValidateLicenseViewModel"/> class.</summary>
    /// <param name="validator">Reaches the verdict.</param>
    /// <param name="files">Asks which licence and which key to check.</param>
    /// <param name="notifications">Reports the failures that are not verdicts.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ValidateLicenseViewModel(
        ILicenseValidator validator,
        IFileDialogService files,
        INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(notifications);

        _validator = validator;
        _files = files;
        _notifications = notifications;

        BrowseLicensePathCommand = new AsyncRelayCommand(OnBrowseLicensePathAsync);
        BrowsePublicKeyPathCommand = new AsyncRelayCommand(OnBrowsePublicKeyPathAsync);
        UseThisMachineCommand = new RelayCommand(OnUseThisMachine);
        ValidateLicenseCommand = new AsyncRelayCommand(OnValidateLicenseAsync, CanValidateLicense);
    }

    /// <summary>Gets the command that asks which licence to check.</summary>
    public AsyncRelayCommand BrowseLicensePathCommand { get; }

    /// <summary>Gets the command that asks which public key to check it against.</summary>
    public AsyncRelayCommand BrowsePublicKeyPathCommand { get; }

    /// <summary>Gets the command that fills the device field with this machine's identifier.</summary>
    public RelayCommand UseThisMachineCommand { get; }

    /// <summary>Gets the command that reaches the verdict.</summary>
    public AsyncRelayCommand ValidateLicenseCommand { get; }

    /// <summary>Gets or sets the licence file to check.</summary>
    public string? LicensePath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                LicensePathHasError = false;
            }
        }
    }

    /// <summary>Gets or sets the public key the licence should have been signed with.</summary>
    /// <remarks>
    /// The public half, and only the public half: the library refuses the private one rather than verifying
    /// with it, because an application that verifies with a private key ships the ability to mint its own
    /// licences. That refusal arrives here as an error notification.
    /// </remarks>
    public string? PublicKeyPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                PublicKeyPathHasError = false;
            }
        }
    }

    /// <summary>Gets or sets the product to check the licence against, or empty to skip that check.</summary>
    public string? ExpectedProductId { get; set => SetProperty(ref field, value); }

    /// <summary>Gets or sets the device to check the licence against, or empty to skip that check.</summary>
    /// <remarks>
    /// Skipping means <b>passing</b>: a licence locked to another machine validates when nothing is entered
    /// here. An application that locks licences to devices has to pass one on every call.
    /// </remarks>
    public string? ExpectedDeviceId { get; set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the licence path was left empty when a check was asked for.</summary>
    public bool LicensePathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the key path was left empty when a check was asked for.</summary>
    public bool PublicKeyPathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether a check is running.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                ValidateLicenseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the verdict on the last licence checked, or <see langword="null"/> before the first check.</summary>
    public LicenseVerdict? Verdict
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasVerdict));
            }
        }
    }

    /// <summary>
    /// Gets what the last licence checked says, or <see langword="null"/> when its payload could not be read.
    /// </summary>
    /// <remarks>
    /// Present on a failed verdict too, whenever the payload was readable — that is what lets the page say
    /// which product a licence is actually for rather than only that it is the wrong one. It is
    /// <b>unverified</b> on a failure: safe to read, not something to act on.
    /// </remarks>
    public LicenseSummary? Summary
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSummary));
            }
        }
    }

    /// <summary>Gets the signature scheme the last document named, or <see langword="null"/> if it named none.</summary>
    public string? DocumentAlgorithm { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets the fingerprint the last document was stamped with, or the words for one carrying none.</summary>
    public string? DocumentKeyId { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the last document parsed as an envelope at all.</summary>
    public bool HasDocumentDetails { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether a licence has been checked on this page.</summary>
    public bool HasVerdict => Verdict is not null;

    /// <summary>Gets a value indicating whether the checked licence's contents could be read.</summary>
    public bool HasSummary => Summary is not null;

    /// <summary>
    /// Whether a check may be asked for: not while one is running.
    /// </summary>
    /// <returns><see langword="true"/> when the command may run.</returns>
    /// <remarks>
    /// The two paths are deliberately <b>not</b> checked here. As on the other two pages, a field the user has
    /// simply not filled in yet leaves the button live and is flagged when they press it, because a disabled
    /// button explains nothing. Unlike the generate page there is no contradictory state to guard against —
    /// every combination of the four inputs is a question the library can answer.
    /// </remarks>
    private bool CanValidateLicense() => !IsBusy;

    private void OnUseThisMachine() => ExpectedDeviceId = DeviceIdentifier.Generate();

    private async Task OnBrowseLicensePathAsync()
    {
        string? path = await _files.ShowOpenFileDialogAsync(
            "Select the licence",
            DirectoryOf(LicensePath) ?? DirectoryOf(PublicKeyPath));

        if (path is not null)
        {
            LicensePath = path;
        }
    }

    private async Task OnBrowsePublicKeyPathAsync()
    {
        string? path = await _files.ShowOpenFileDialogAsync(
            "Select the public key",
            DirectoryOf(PublicKeyPath) ?? DirectoryOf(LicensePath));

        if (path is not null)
        {
            PublicKeyPath = path;
        }
    }

    private async Task OnValidateLicenseAsync()
    {
        if (!ValidateInputs())
        {
            return;
        }

        // Snapshotted before the await, so a form edited while the check runs cannot change what was checked.
        string licensePath = LicensePath!;
        string publicKeyPath = PublicKeyPath!;
        string? productId = Trimmed(ExpectedProductId);
        string? deviceId = Trimmed(ExpectedDeviceId);

        IsBusy = true;
        ClearResult();

        try
        {
            LicenseValidationResult result =
                await _validator.ValidateAsync(licensePath, publicKeyPath, productId, deviceId);

            ShowDocument(await TryReadEnvelopeAsync(licensePath));

            Summary = result.License is { } license ? new LicenseSummary(license) : null;

            // Last, so that a card appearing on screen is always complete.
            Verdict = LicenseVerdict.For(result);
        }
        catch (Exception exception)
        {
            // Everything reaching here is the application's own mistake rather than a verdict: a file that is
            // not there, a key document that is not a key, or the private half where the public one belongs.
            // The library throws for exactly those, and letting one escape a command handler would take the
            // application down instead of explaining itself.
            await _notifications.ShowErrorAsync("The licence could not be checked", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Flags whichever path was left empty, and reports whether the check may go ahead.
    /// </summary>
    /// <returns><see langword="true"/> when both files have been chosen.</returns>
    private bool ValidateInputs()
    {
        LicensePathHasError = string.IsNullOrWhiteSpace(LicensePath);
        PublicKeyPathHasError = string.IsNullOrWhiteSpace(PublicKeyPath);

        return !LicensePathHasError && !PublicKeyPathHasError;
    }

    private void ClearResult()
    {
        Verdict = null;
        Summary = null;
        ShowDocument(null);
    }

    /// <summary>Shows what the document itself said, as opposed to what the licence inside it says.</summary>
    /// <param name="envelope">The parsed envelope, or <see langword="null"/> when there was none.</param>
    private void ShowDocument(LicenseEnvelope? envelope)
    {
        DocumentAlgorithm = envelope?.Algorithm;
        DocumentKeyId = envelope is null ? null : envelope.KeyId ?? "Not stamped";
        HasDocumentDetails = envelope is not null;
    }

    /// <summary>Reads the envelope's own fields back, for display beside the verdict.</summary>
    /// <param name="path">The licence file, already checked by the validator.</param>
    /// <returns>The envelope, or <see langword="null"/> if it could not be read.</returns>
    /// <remarks>
    /// <para>
    /// The document is opened a second time on purpose. The verdict has to come from the library's own path
    /// overload — it owns loading the key, parsing the envelope and mapping a parse failure onto
    /// <see cref="LicenseFailure.InvalidFormat"/> — and that overload hands back a verdict, not the document it
    /// read. The <c>algorithm</c> and <c>keyId</c> fields are worth showing anyway: on a signature failure they
    /// are the diagnosis, because a fingerprint that does not match the key being checked against says
    /// "wrong key" rather than "forged licence". A second read of a few kilobytes buys that, and cannot
    /// influence the verdict.
    /// </para>
    /// <para>
    /// Both failures are swallowed because both are already accounted for: a document that is not an envelope
    /// has produced an <see cref="LicenseFailure.InvalidFormat"/> verdict saying so, and a file that cannot be
    /// read at all never got as far as this — the validator would have thrown first.
    /// </para>
    /// </remarks>
    private static async Task<LicenseEnvelope?> TryReadEnvelopeAsync(string path)
    {
        try
        {
            await using FileStream input = File.OpenRead(path);

            return await LicenseEnvelope.LoadAsync(input);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Trims a field, treating whitespace as nothing at all.</summary>
    /// <param name="value">What the field holds.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when there was nothing in it.</returns>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Returns the folder a path is in, or <see langword="null"/> when there is nothing to go on.</summary>
    /// <param name="path">The path to read the folder from.</param>
    /// <returns>The folder, or <see langword="null"/>.</returns>
    private static string? DirectoryOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(path);

        return string.IsNullOrEmpty(directory) ? null : directory;
    }
}
