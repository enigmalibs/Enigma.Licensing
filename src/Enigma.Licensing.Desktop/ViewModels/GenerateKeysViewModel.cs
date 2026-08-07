using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// The keys page: generating a signing key pair and writing both halves to disk as
/// <c>enigma-key/1</c> documents.
/// </summary>
/// <remarks>
/// <para>
/// This is the first thing an issuer does, and the only one of the three workflows whose output is a
/// secret. The private half signs licences and never leaves the issuer's machine; the public half ships
/// inside the application that checks them. Both files carry the same fingerprint, which is what the page
/// shows when it is done — it is the value that will appear in every licence signed with this pair.
/// </para>
/// <para>
/// <b>Nothing here is remembered.</b> The paths and the password live as long as the page does and are
/// written to no preference file. The password becomes a <see cref="char"/> array for the one call that
/// needs it and is cleared immediately afterwards; the string the text box holds cannot be cleared, which
/// is precisely why it is never persisted or logged.
/// </para>
/// <para>
/// Generation runs on a background thread behind the blocking overlay. RSA is a search for primes and takes
/// seconds at 4096 bits and longer at 8192; the Argon2id derivation that encrypts the private half costs
/// 64 MiB of work on top. Both would freeze the window if they ran where the click arrived.
/// </para>
/// </remarks>
public sealed class GenerateKeysViewModel : ObservableObject
{
    /// <summary>The base name offered when there is nothing to derive one from.</summary>
    private const string DefaultBaseName = "signing";

    /// <summary>The suffix that makes a file recognisably the public half of a pair.</summary>
    private const string PublicKeySuffix = ".public.key.json";

    /// <summary>The suffix that makes a file recognisably the private half of a pair.</summary>
    private const string PrivateKeySuffix = ".private.key.json";

    private readonly IKeyService _keys;
    private readonly IFileDialogService _files;
    private readonly IBusyOverlayService _busy;
    private readonly INotificationService _notifications;
    private readonly IClipboardService _clipboard;

    /// <summary>Initializes a new instance of the <see cref="GenerateKeysViewModel"/> class.</summary>
    /// <param name="keys">Generates the pair and writes both halves.</param>
    /// <param name="files">Asks where each half should be written.</param>
    /// <param name="busy">Blocks the application while generation runs.</param>
    /// <param name="notifications">Reports the outcome.</param>
    /// <param name="clipboard">Takes the fingerprint when the user asks for it.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public GenerateKeysViewModel(
        IKeyService keys,
        IFileDialogService files,
        IBusyOverlayService busy,
        INotificationService notifications,
        IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);

        _keys = keys;
        _files = files;
        _busy = busy;
        _notifications = notifications;
        _clipboard = clipboard;

        BrowsePublicKeyPathCommand = new AsyncRelayCommand(OnBrowsePublicKeyPathAsync);
        BrowsePrivateKeyPathCommand = new AsyncRelayCommand(OnBrowsePrivateKeyPathAsync);
        GenerateKeyPairCommand = new AsyncRelayCommand(OnGenerateKeyPairAsync, CanGenerateKeyPair);
        CopyKeyIdCommand = new AsyncRelayCommand(OnCopyKeyIdAsync, () => LastKeyId is not null);
    }

    /// <summary>Gets the command that asks where the public half should be written.</summary>
    public AsyncRelayCommand BrowsePublicKeyPathCommand { get; }

    /// <summary>Gets the command that asks where the private half should be written.</summary>
    public AsyncRelayCommand BrowsePrivateKeyPathCommand { get; }

    /// <summary>Gets the command that generates the pair and writes both halves.</summary>
    public AsyncRelayCommand GenerateKeyPairCommand { get; }

    /// <summary>Gets the command that copies the last fingerprint to the clipboard.</summary>
    public AsyncRelayCommand CopyKeyIdCommand { get; }

    /// <summary>Gets the signing schemes on offer.</summary>
    public IReadOnlyList<SignatureAlgorithmOption> Algorithms => SignatureAlgorithmOption.All;

    /// <summary>Gets the RSA modulus sizes on offer, in bits. Ignored unless an RSA scheme is selected.</summary>
    /// <remarks>
    /// The library accepts anything from 2048 to 8192; these are the four sizes worth offering. 4096 is the
    /// default here rather than the library's 3072, matching the predecessor application: an issuing key is
    /// generated once and lives for years, and the seconds it costs are paid once.
    /// </remarks>
    public IReadOnlyList<int> RsaKeySizes { get; } = [2048, 3072, 4096, 8192];

    /// <summary>Gets or sets the scheme the pair will be generated for.</summary>
    public SignatureAlgorithmOption SelectedAlgorithm
    {
        get;
        set
        {
            // A selector hands back null while its items are being replaced; that is not a choice.
            if (value is null || !SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsRsaSelected));
        }
    } = SignatureAlgorithmOption.Default;

    /// <summary>Gets or sets the RSA modulus size in bits.</summary>
    public int SelectedRsaKeySize { get; set => SetProperty(ref field, value); } = 4096;

    /// <summary>
    /// Gets a value indicating whether the selected scheme needs a modulus size — the page shows the size
    /// picker only then, because the ML-DSA parameter sets fix their own key sizes.
    /// </summary>
    public bool IsRsaSelected => SelectedAlgorithm.IsRsa;

    /// <summary>Gets or sets the password the private half is encrypted with, or empty to write it in the clear.</summary>
    public string? Password
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPasswordChanged();
            }
        }
    }

    /// <summary>Gets or sets the repeat of <see cref="Password"/>, which has to match it.</summary>
    public string? ConfirmPassword
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPasswordChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the confirmation contradicts the password.
    /// </summary>
    /// <remarks>
    /// It reports a mismatch only once something has been typed into the confirmation, so the error does not
    /// appear under a field the user has not reached yet. An <i>unconfirmed</i> password is a separate thing
    /// and is what keeps <see cref="GenerateKeyPairCommand"/> disabled.
    /// </remarks>
    public bool HasPasswordMismatch =>
        !string.IsNullOrEmpty(ConfirmPassword) && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal);

    /// <summary>Gets the message shown under the confirmation field, or <see langword="null"/> when it agrees.</summary>
    public string? PasswordMismatchMessage => HasPasswordMismatch ? "The two passwords are not the same." : null;

    /// <summary>Gets or sets where the public half will be written.</summary>
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

    /// <summary>Gets or sets where the private half will be written.</summary>
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

    /// <summary>Gets a value indicating whether the public-key path was left empty when generation was asked for.</summary>
    public bool PublicKeyPathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether the private-key path was left empty when generation was asked for.</summary>
    public bool PrivateKeyPathHasError { get; private set => SetProperty(ref field, value); }

    /// <summary>Gets a value indicating whether a generation is running.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                GenerateKeyPairCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the fingerprint of the last pair generated, or <see langword="null"/> if none has been.</summary>
    public string? LastKeyId
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasResult));
                CopyKeyIdCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets a value indicating whether a pair has been generated on this page.</summary>
    public bool HasResult => LastKeyId is not null;

    /// <summary>
    /// Whether generation may be asked for: not while one is running, and not with a password the user has
    /// not repeated correctly.
    /// </summary>
    /// <returns><see langword="true"/> when the command may run.</returns>
    private bool CanGenerateKeyPair() =>
        !IsBusy && (string.IsNullOrEmpty(Password) || string.Equals(Password, ConfirmPassword, StringComparison.Ordinal));

    private void OnPasswordChanged()
    {
        OnPropertyChanged(nameof(HasPasswordMismatch));
        OnPropertyChanged(nameof(PasswordMismatchMessage));
        GenerateKeyPairCommand.NotifyCanExecuteChanged();
    }

    private async Task OnBrowsePublicKeyPathAsync()
    {
        string? path = await _files.ShowSaveFileDialogAsync(
            "Save the public key",
            SuggestedName(PrivateKeyPath, PrivateKeySuffix) + PublicKeySuffix,
            DirectoryOf(PrivateKeyPath));

        if (path is not null)
        {
            PublicKeyPath = path;
        }
    }

    private async Task OnBrowsePrivateKeyPathAsync()
    {
        string? path = await _files.ShowSaveFileDialogAsync(
            "Save the private key",
            SuggestedName(PublicKeyPath, PublicKeySuffix) + PrivateKeySuffix,
            DirectoryOf(PublicKeyPath));

        if (path is not null)
        {
            PrivateKeyPath = path;
        }
    }

    private async Task OnGenerateKeyPairAsync()
    {
        if (!ValidateOutputPaths())
        {
            return;
        }

        // The library never touches a password array it is handed — clearing it is the caller's job, and
        // this copy is the only part of the secret this application can actually erase.
        char[]? password = string.IsNullOrEmpty(Password) ? null : Password.ToCharArray();

        LicenseSignatureAlgorithm algorithm = SelectedAlgorithm.Algorithm;
        int rsaKeySizeBits = SelectedRsaKeySize;
        string publicKeyPath = PublicKeyPath!;
        string privateKeyPath = PrivateKeyPath!;

        IsBusy = true;

        try
        {
            await _busy.ShowAsync(
                "Generating the key pair",
                "Finding a key of the size you asked for. This can take a while, and cannot be hurried.");

            string keyId;

            try
            {
                // Both halves of the work go to the thread pool: generation is CPU-bound, and encrypting the
                // private half runs Argon2id over 64 MiB before a single byte is written.
                keyId = await Task.Run(async () =>
                {
                    LicensingKeyPair pair = _keys.GenerateKeyPair(algorithm, rsaKeySizeBits);

                    await _keys.SaveKeyPairAsync(pair, publicKeyPath, privateKeyPath, password)
                        .ConfigureAwait(false);

                    return pair.PublicKey.KeyId;
                });
            }
            finally
            {
                await _busy.HideAsync();
            }

            LastKeyId = keyId;

            await _notifications.ShowSuccessAsync(
                "Key pair generated",
                $"Fingerprint {keyId}. The public half is at {publicKeyPath} and the private half at {privateKeyPath}.");
        }
        catch (Exception exception)
        {
            // Everything that can go wrong here is worth the same response: say so and leave the page as it
            // was. A rejected key size, an unwritable directory, a full disk and a failure inside the
            // cryptography all reach the user as the message the failure itself carried, and letting any of
            // them escape a command handler would take the application down instead.
            await _notifications.ShowErrorAsync("The key pair could not be generated", exception.Message);
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

    private async Task OnCopyKeyIdAsync()
    {
        if (LastKeyId is string keyId)
        {
            await _clipboard.CopyTextAsync(keyId);
        }
    }

    /// <summary>
    /// Flags whichever output path was left empty, and reports whether generation may go ahead.
    /// </summary>
    /// <returns><see langword="true"/> when both paths are set.</returns>
    /// <remarks>
    /// The paths are checked here rather than in <see cref="CanGenerateKeyPair"/> so that the button stays
    /// live and clicking it says <i>what</i> is missing. A disabled button explains nothing.
    /// </remarks>
    private bool ValidateOutputPaths()
    {
        PublicKeyPathHasError = string.IsNullOrWhiteSpace(PublicKeyPath);
        PrivateKeyPathHasError = string.IsNullOrWhiteSpace(PrivateKeyPath);

        return !PublicKeyPathHasError && !PrivateKeyPathHasError;
    }

    /// <summary>
    /// Derives the base name to offer for one half from the path already chosen for the other.
    /// </summary>
    /// <param name="otherPath">The path chosen for the other half, if any.</param>
    /// <param name="otherSuffix">The suffix that half's file carries.</param>
    /// <returns>The base name, without a suffix.</returns>
    /// <remarks>
    /// Choosing <c>acme.public.key.json</c> and then browsing for the private half offers
    /// <c>acme.private.key.json</c> in the same folder, so the two files of a pair end up named after each
    /// other without the user typing anything twice.
    /// </remarks>
    private static string SuggestedName(string? otherPath, string otherSuffix)
    {
        if (string.IsNullOrWhiteSpace(otherPath))
        {
            return DefaultBaseName;
        }

        string name = Path.GetFileName(otherPath);

        name = name.EndsWith(otherSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^otherSuffix.Length]
            : Path.GetFileNameWithoutExtension(name);

        return string.IsNullOrEmpty(name) ? DefaultBaseName : name;
    }

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
