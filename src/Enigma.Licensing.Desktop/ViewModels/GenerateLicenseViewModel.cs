using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Licensing.Desktop.Models;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// The generate page: filling in a licence, signing it with a private key and saving the envelope — plus the
/// profiles that keep a form worth typing once.
/// </summary>
/// <remarks>
/// <para>
/// This is the issuer's daily work. The keys page runs once a year; this one runs per customer, which is why
/// it carries profiles: everything except the password and the destination is worth keeping between issues.
/// </para>
/// <para>
/// <b>Two rules govern the generate button, and they are different on purpose</b>, following the keys page.
/// A field the user has simply not filled in yet leaves the button <i>live</i> and is flagged when they press
/// it, because a disabled button explains nothing. A validity window that ends before it starts <i>disables</i>
/// it, because that is not an omission but a contradiction — the library would refuse it, and the message
/// belongs under the dates rather than in a notification.
/// </para>
/// <para>
/// <b>The password is never remembered.</b> It is not in a profile, not in the preference file and not in a
/// log; it becomes a <see cref="char"/> array for the one call that needs it and is cleared in a
/// <c>finally</c>. The string the text box holds cannot be cleared, which is exactly why it never leaves this
/// object.
/// </para>
/// <para>
/// Signing runs on a background thread behind the blocking overlay. Signing itself is quick, but an encrypted
/// private key costs an Argon2id derivation over 64 MiB before a single byte is signed, and that is long
/// enough to freeze the window if it ran where the click arrived.
/// </para>
/// </remarks>
public sealed class GenerateLicenseViewModel : ObservableValidator
{
    /// <summary>The suffix that makes a file recognisably a licence.</summary>
    private const string LicenseSuffix = ".license.json";

    /// <summary>The suffix that makes a file recognisably one of this application's profiles.</summary>
    private const string ProfileSuffix = ".profile.json";

    private readonly ILicenseGenerator _generator;
    private readonly ILicenseProfileStore _profiles;
    private readonly IFileDialogService _files;
    private readonly IBusyOverlayService _busy;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;

    /// <summary>Initializes a new instance of the <see cref="GenerateLicenseViewModel"/> class.</summary>
    /// <param name="generator">Signs the request and writes the envelope.</param>
    /// <param name="profiles">Reads and writes the saved forms.</param>
    /// <param name="files">Asks which key to sign with, and where each document goes.</param>
    /// <param name="busy">Blocks the application while signing runs.</param>
    /// <param name="notifications">Reports the outcome.</param>
    /// <param name="clipboard">Takes the licence identifier when the user asks for it.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public GenerateLicenseViewModel(
        ILicenseGenerator generator,
        ILicenseProfileStore profiles,
        IFileDialogService files,
        IBusyOverlayService busy,
        INotificationService notifications,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);

        _generator = generator;
        _profiles = profiles;
        _files = files;
        _busy = busy;
        _notifications = notifications;
        _clipboard = clipboard;

        UseThisMachineCommand = new RelayCommand(OnUseThisMachine);
        BrowsePrivateKeyPathCommand = new AsyncRelayCommand(OnBrowsePrivateKeyPathAsync);
        BrowseOutputPathCommand = new AsyncRelayCommand(OnBrowseOutputPathAsync);
        GenerateLicenseCommand = new AsyncRelayCommand(OnGenerateLicenseAsync, CanGenerateLicense);
        SaveProfileCommand = new AsyncRelayCommand(OnSaveProfileAsync);
        LoadProfileCommand = new AsyncRelayCommand(OnLoadProfileAsync);
        CopyLicenseIdCommand = new AsyncRelayCommand(OnCopyLicenseIdAsync, () => LastLicenseId is not null);
    }

    /// <summary>Gets the command that fills the device field with this machine's identifier.</summary>
    public RelayCommand UseThisMachineCommand { get; }

    /// <summary>Gets the command that asks which private key to sign with.</summary>
    public AsyncRelayCommand BrowsePrivateKeyPathCommand { get; }

    /// <summary>Gets the command that asks where the licence should be written.</summary>
    public AsyncRelayCommand BrowseOutputPathCommand { get; }

    /// <summary>Gets the command that signs the licence and writes it.</summary>
    public AsyncRelayCommand GenerateLicenseCommand { get; }

    /// <summary>Gets the command that writes the form to a profile file.</summary>
    public AsyncRelayCommand SaveProfileCommand { get; }

    /// <summary>Gets the command that fills the form from a profile file.</summary>
    public AsyncRelayCommand LoadProfileCommand { get; }

    /// <summary>Gets the command that copies the last licence identifier to the clipboard.</summary>
    public AsyncRelayCommand CopyLicenseIdCommand { get; }

    /// <summary>Gets or sets the product the licence is for — the one field a licence cannot do without.</summary>
    /// <remarks>
    /// A <c>*</c> anywhere in it makes the match a wildcard one, which is how a single licence covers a range
    /// of versions. Only the licence's side of the comparison honours it; the identifier the application
    /// passes at validation time is always literal.
    /// </remarks>
    [Required(ErrorMessage = "A product identifier is required — it is the one thing a licence must say.")]
    [MaxLength(200, ErrorMessage = "That is longer than a product identifier should be.")]
    public string? ProductId
    {
        get;
        set
        {
            if (SetProperty(ref field, value, validate: true))
            {
                OnProductIdChanged();
            }
        }
    }

    /// <summary>Gets or sets who the licence is issued to. Descriptive only — validation never reads it.</summary>
    public string? Owner { get; set => SetProperty(ref field, value); }

    /// <summary>Gets or sets the device the licence is locked to, or empty for one that runs anywhere.</summary>
    public string? DeviceId { get; set => SetProperty(ref field, value); }

    /// <summary>Gets or sets the day the licence starts being usable, or <see langword="null"/> for immediately.</summary>
    public DateTime? ValidFromDate
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnWindowChanged();
            }
        }
    }

    /// <summary>Gets or sets the last day the licence is usable, or <see langword="null"/> for a perpetual one.</summary>
    /// <remarks>
    /// The day chosen is <b>included</b>: the licence stops being usable at midnight at the end of it, so a
    /// licence valid until the 31st still works on the 31st. That is what "valid until December" means to
    /// everyone who is not writing the comparison, and the alternative — expiring at the start of the chosen
    /// day — costs a support ticket every time.
    /// </remarks>
    public DateTime? ValidUntilDate
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnWindowChanged();
            }
        }
    }

    /// <summary>Gets or sets the private key file the licence is signed with.</summary>
    public string? PrivateKeyPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                PrivateKeyPathHasError = false;
            }
        }
    }

    /// <summary>Gets or sets the password that opens the private key, or empty for a key in the clear.</summary>
    public string? PrivateKeyPassword { get; set => SetProperty(ref field, value); }

    /// <summary>Gets or sets where the signed licence will be written.</summary>
    public string? OutputPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OutputPathHasError = false;
            }
        }
    }

    /// <summary>Gets a value indicating whether the product identifier has been found wanting.</summary>
    public bool ProductIdHasError => ProductIdError is not null;

    /// <summary>Gets the message shown under the product field, or <see langword="null"/> when it is fine.</summary>
    public string? ProductIdErrorMessage => ProductIdError?.ErrorMessage;

    /// <summary>Gets a value indicating whether the private-key path was left empty when signing was asked for.</summary>
    public bool PrivateKeyPathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the output path was left empty when signing was asked for.</summary>
    public bool OutputPathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the validity window ends before it starts.</summary>
    public bool HasWindowError =>
        ValidFromDate is DateTime from && ValidUntilDate is DateTime until && from.Date > until.Date;

    /// <summary>Gets the message shown under the dates, or <see langword="null"/> when they agree.</summary>
    public string? WindowErrorMessage =>
        HasWindowError ? "The licence would stop being valid before it started." : null;

    /// <summary>Gets a value indicating whether a licence is being signed.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                GenerateLicenseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the identifier of the last licence issued here, or <see langword="null"/> if none has been.</summary>
    public string? LastLicenseId
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                CopyLicenseIdCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets where the last licence was written, or <see langword="null"/> if none has been.</summary>
    public string? LastLicensePath
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    /// <summary>Gets the fingerprint of the pair that signed the last licence.</summary>
    public string? LastKeyId { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether a licence has been issued on this page.</summary>
    public bool HasResult => LastLicensePath is not null;

    private ValidationResult? ProductIdError => GetErrors(nameof(ProductId)).FirstOrDefault();

    /// <summary>
    /// Whether signing may be asked for: not while one is running, and not with a window that contradicts
    /// itself.
    /// </summary>
    /// <returns><see langword="true"/> when the command may run.</returns>
    /// <remarks>
    /// The empty fields are deliberately <b>not</b> checked here — see the class remarks. This is the rule for
    /// the thing the user cannot fix by reading a message under a disabled button.
    /// </remarks>
    private bool CanGenerateLicense() => !IsBusy && !HasWindowError;

    private void OnProductIdChanged()
    {
        OnPropertyChanged(nameof(ProductIdHasError));
        OnPropertyChanged(nameof(ProductIdErrorMessage));
    }

    private void OnWindowChanged()
    {
        OnPropertyChanged(nameof(HasWindowError));
        OnPropertyChanged(nameof(WindowErrorMessage));
        GenerateLicenseCommand.NotifyCanExecuteChanged();
    }

    private void OnUseThisMachine() => DeviceId = DeviceIdentifier.Generate();

    private async Task OnBrowsePrivateKeyPathAsync()
    {
        string? path = await _files.ShowOpenFileDialogAsync("Select the private key", DirectoryOf(PrivateKeyPath));

        if (path is not null)
        {
            PrivateKeyPath = path;
        }
    }

    private async Task OnBrowseOutputPathAsync()
    {
        string? path = await _files.ShowSaveFileDialogAsync(
            "Save the licence",
            SuggestedFileName(LicenseSuffix),
            DirectoryOf(OutputPath) ?? DirectoryOf(PrivateKeyPath));

        if (path is not null)
        {
            OutputPath = path;
        }
    }

    private async Task OnGenerateLicenseAsync()
    {
        if (!ValidateForGeneration())
        {
            return;
        }

        // The library never touches a password array it is handed — clearing it is the caller's job, and this
        // copy is the only part of the secret this application can actually erase.
        char[]? password = string.IsNullOrEmpty(PrivateKeyPassword) ? null : PrivateKeyPassword.ToCharArray();

        // Snapshotted before the await, so a form edited while signing runs cannot change what gets signed.
        LicenseRequest request = BuildRequest();
        string privateKeyPath = PrivateKeyPath!;
        string outputPath = OutputPath!;

        IsBusy = true;

        try
        {
            await _busy.ShowAsync(
                "Signing the licence",
                "Reading the private key and signing. An encrypted key costs an Argon2id derivation first.");

            LicenseEnvelope envelope;

            try
            {
                // Off the UI thread: opening an encrypted key is 64 MiB of Argon2id, and RSA signing is not
                // free either.
                envelope = await Task.Run(() =>
                    _generator.GenerateAndSaveAsync(request, privateKeyPath, password, outputPath));
            }
            finally
            {
                await _busy.HideAsync();
            }

            // The generator mints a ULID per call and the envelope carries it only inside the signed bytes,
            // so this is the one thing about the licence that has to be read back out of it. A null means
            // the payload could not be read — the licence is on disk by then, and not being able to name it
            // is no reason to report the issue as having failed.
            LastLicenseId = envelope.ReadLicense()?.Id;
            LastKeyId = envelope.KeyId;
            LastLicensePath = outputPath;

            await _notifications.ShowSuccessAsync(
                "Licence generated",
                LastLicenseId is string id
                    ? $"Licence {id}, written to {outputPath}."
                    : $"Written to {outputPath}.");
        }
        catch (Exception exception)
        {
            // A key file that is missing, encrypted with another password, or the public half of a pair; a
            // request the library refuses; an unwritable destination. All of them reach the user as the
            // message the failure itself carried, because letting any escape a command handler would take the
            // application down instead.
            await _notifications.ShowErrorAsync("The licence could not be generated", exception.Message);
        }
        finally
        {
            if (password is not null)
            {
                Array.Clear(password);
            }

            IsBusy = false;
        }
    }

    private async Task OnSaveProfileAsync()
    {
        string? path = await _files.ShowSaveFileDialogAsync(
            "Save the licence profile",
            SuggestedFileName(ProfileSuffix),
            DirectoryOf(PrivateKeyPath));

        if (path is null)
        {
            return;
        }

        try
        {
            await _profiles.SaveAsync(CaptureProfile(), path);

            await _notifications.ShowSuccessAsync(
                "Profile saved",
                $"The form was written to {path}. The password was not: a profile never holds one.");
        }
        catch (Exception exception)
        {
            await _notifications.ShowErrorAsync("The profile could not be saved", exception.Message);
        }
    }

    private async Task OnLoadProfileAsync()
    {
        string? path = await _files.ShowOpenFileDialogAsync("Open a licence profile", DirectoryOf(PrivateKeyPath));

        if (path is null)
        {
            return;
        }

        try
        {
            ApplyProfile(await _profiles.LoadAsync(path));

            await _notifications.ShowSuccessAsync(
                "Profile loaded",
                "The form was filled from the profile. Type the private key's password to sign with it.");
        }
        catch (Exception exception)
        {
            await _notifications.ShowErrorAsync("The profile could not be loaded", exception.Message);
        }
    }

    private async Task OnCopyLicenseIdAsync()
    {
        if (LastLicenseId is string id)
        {
            await _clipboard.CopyTextAsync(id);
        }
    }

    /// <summary>
    /// Flags whatever was left out, and reports whether signing may go ahead.
    /// </summary>
    /// <returns><see langword="true"/> when the form is complete enough to sign.</returns>
    private bool ValidateForGeneration()
    {
        ValidateAllProperties();
        OnProductIdChanged();

        PrivateKeyPathHasError = string.IsNullOrWhiteSpace(PrivateKeyPath);
        OutputPathHasError = string.IsNullOrWhiteSpace(OutputPath);

        return !HasErrors && !PrivateKeyPathHasError && !OutputPathHasError && !HasWindowError;
    }

    /// <summary>Turns the form into the request the library signs.</summary>
    /// <returns>The request.</returns>
    /// <remarks>
    /// Everything optional that was left blank becomes <see langword="null"/> rather than an empty string or
    /// an empty collection: the envelope omits a null field entirely, so a licence that says nothing about an
    /// owner does not carry <c>"owner": ""</c> for the rest of its life.
    /// </remarks>
    private LicenseRequest BuildRequest() => new()
    {
        ProductId = Trimmed(ProductId)!,
        Owner = Trimmed(Owner),
        DeviceId = Trimmed(DeviceId),
        ValidFrom = StartOfDay(ValidFromDate),
        ValidUntil = EndOfDay(ValidUntilDate),
    };

    /// <summary>Turns the form into the profile that is written to disk.</summary>
    /// <returns>The profile.</returns>
    private LicenseProfile CaptureProfile() => new()
    {
        ProductId = Trimmed(ProductId),
        Owner = Trimmed(Owner),
        DeviceId = Trimmed(DeviceId),
        ValidFrom = ToDateOnly(ValidFromDate),
        ValidUntil = ToDateOnly(ValidUntilDate),
        PrivateKeyPath = Trimmed(PrivateKeyPath),

        // No password, and no output path: the first must never be written down, and the second belongs to
        // one issue rather than to the kind of licence this profile describes.
    };

    /// <summary>Fills the form from a loaded profile.</summary>
    /// <param name="profile">The profile to read.</param>
    private void ApplyProfile(LicenseProfile profile)
    {
        ProductId = profile.ProductId;
        Owner = profile.Owner;
        DeviceId = profile.DeviceId;
        ValidFromDate = FromDateOnly(profile.ValidFrom);
        ValidUntilDate = FromDateOnly(profile.ValidUntil);
        PrivateKeyPath = profile.PrivateKeyPath;

        // The profile just named a key — possibly a different one. A password typed for the previous key is
        // no longer the password for anything, so it goes.
        PrivateKeyPassword = null;
    }

    /// <summary>Offers a file name derived from the product identifier.</summary>
    /// <param name="suffix">The suffix identifying what kind of document is being named.</param>
    /// <returns>The name to offer, extension included.</returns>
    /// <remarks>
    /// A product identifier is not a file name: it may hold a wildcard, a slash or a colon. The wildcard is
    /// dropped and anything the platform forbids becomes a hyphen, so <c>"Acme Suite 2.*"</c> is offered as
    /// <c>Acme Suite 2.license.json</c>. When there is nothing to derive a name from, the suffix stands on its
    /// own as <c>license.json</c>.
    /// </remarks>
    private string SuggestedFileName(string suffix)
    {
        StringBuilder builder = new();

        foreach (char character in ProductId ?? string.Empty)
        {
            if (character == '*')
            {
                continue;
            }

            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '-' : character);
        }

        string name = builder.ToString().Trim(' ', '.', '-');

        return name.Length == 0 ? suffix[1..] : name + suffix;
    }

    /// <summary>The instant a licence starting on <paramref name="date"/> becomes usable: local midnight.</summary>
    /// <param name="date">The chosen day, or <see langword="null"/>.</param>
    /// <returns>The instant, or <see langword="null"/>.</returns>
    private static DateTimeOffset? StartOfDay(DateTime? date) =>
        date is DateTime chosen ? new DateTimeOffset(chosen.Date) : null;

    /// <summary>The instant a licence ending on <paramref name="date"/> stops being usable.</summary>
    /// <param name="date">The chosen day, or <see langword="null"/>.</param>
    /// <returns>Local midnight at the <b>end</b> of the chosen day, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The chosen day is included — see <see cref="ValidUntilDate"/>. The library treats
    /// <see cref="License.ValidUntil"/> as the first instant the licence is no longer usable, so the day after
    /// at midnight is exactly "through the end of the chosen day".
    /// </remarks>
    private static DateTimeOffset? EndOfDay(DateTime? date) =>
        date is DateTime chosen ? new DateTimeOffset(chosen.Date.AddDays(1)) : null;

    private static DateOnly? ToDateOnly(DateTime? date) =>
        date is DateTime chosen ? DateOnly.FromDateTime(chosen) : null;

    private static DateTime? FromDateOnly(DateOnly? date) =>
        date is DateOnly chosen ? chosen.ToDateTime(TimeOnly.MinValue) : null;

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
