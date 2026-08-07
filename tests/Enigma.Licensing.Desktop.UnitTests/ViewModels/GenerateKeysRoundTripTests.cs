using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The keys page driven against the <b>real</b> library, writing real files to a temporary folder: the
/// phase's acceptance criterion is that a pair generated in the application loads back through the library,
/// and this is where that is actually shown.
/// </summary>
/// <remarks>
/// <para>
/// Only the platform seams are doubles — dialogs, overlay, info bar, clipboard. The key service is the one
/// the application ships with, so what these tests exercise is the whole path from the page's fields to two
/// <c>enigma-key/1</c> documents on disk and back.
/// </para>
/// <para>
/// ML-DSA-44 throughout: it is the cheapest scheme the library has, and what is under test here is the page
/// and the file format, not the arithmetic. The encrypted case pays for one Argon2id derivation each way,
/// which is the deliberate cost of proving that a password typed into this page is the password that opens
/// the file.
/// </para>
/// </remarks>
public sealed class GenerateKeysRoundTripTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("enigma-licensing-keys-").FullName;

    private readonly FakeFileDialogService _files = new();
    private readonly FakeBusyOverlayService _busy = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeClipboardService _clipboard = new();

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task APairGeneratedOnThePage_LoadsBackThroughTheLibrary()
    {
        IKeyService keys = new KeyService();
        GenerateKeysViewModel viewModel = Build(keys, out string publicKeyPath, out string privateKeyPath);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(_notifications.Notifications).IsError);

        LicensingKey publicKey = await keys.LoadKeyAsync(publicKeyPath, cancellationToken: Token);
        LicensingKey privateKey = await keys.LoadKeyAsync(privateKeyPath, cancellationToken: Token);

        Assert.Equal(KeyKind.Public, publicKey.Kind);
        Assert.Equal(KeyKind.Private, privateKey.Kind);
        Assert.Equal(LicenseSignatureAlgorithm.MLDsa44, publicKey.Algorithm);
        Assert.Equal(LicenseSignatureAlgorithm.MLDsa44, privateKey.Algorithm);

        // Both halves carry the fingerprint the page reported — the value an issuer copies out of it.
        Assert.Equal(viewModel.LastKeyId, publicKey.KeyId);
        Assert.Equal(viewModel.LastKeyId, privateKey.KeyId);
    }

    /// <summary>
    /// The password typed into the page is the password the file was encrypted with — and the only one that
    /// opens it.
    /// </summary>
    [Fact]
    public async Task APasswordTypedOnThePage_IsThePasswordThatOpensThePrivateHalf()
    {
        IKeyService keys = new KeyService();
        GenerateKeysViewModel viewModel = Build(keys, out _, out string privateKeyPath);
        viewModel.Password = "correct horse";
        viewModel.ConfirmPassword = "correct horse";

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        LicensingKey privateKey = await keys.LoadKeyAsync(
            privateKeyPath,
            "correct horse".ToCharArray(),
            Token);

        Assert.Equal(viewModel.LastKeyId, privateKey.KeyId);

        await Assert.ThrowsAsync<CryptographicException>(
            () => keys.LoadKeyAsync(privateKeyPath, "wrong horse".ToCharArray(), Token));

        await Assert.ThrowsAsync<CryptographicException>(
            () => keys.LoadKeyAsync(privateKeyPath, cancellationToken: Token));
    }

    /// <summary>
    /// A failure the library raises reaches the user as the message it carried, and leaves nothing behind on
    /// the page — no fingerprint, and no overlay.
    /// </summary>
    [Fact]
    public async Task AnUnwritablePath_IsReportedRatherThanThrown()
    {
        GenerateKeysViewModel viewModel = Build(new KeyService(), out _, out _);

        // A directory where the file should be: the write fails, and the failure is the library's own.
        viewModel.PrivateKeyPath = Path.Combine(_directory, "occupied");
        Directory.CreateDirectory(viewModel.PrivateKeyPath);

        await viewModel.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.Null(viewModel.LastKeyId);
        Assert.False(_busy.IsShowing);
        Assert.False(viewModel.IsBusy);
    }

    private GenerateKeysViewModel Build(IKeyService keys, out string publicKeyPath, out string privateKeyPath)
    {
        publicKeyPath = Path.Combine(_directory, "acme.public.key.json");
        privateKeyPath = Path.Combine(_directory, "acme.private.key.json");

        return new GenerateKeysViewModel(keys, _files, _busy, _notifications, _clipboard)
        {
            SelectedAlgorithm = SignatureAlgorithmOption.For(LicenseSignatureAlgorithm.MLDsa44),
            PublicKeyPath = publicKeyPath,
            PrivateKeyPath = privateKeyPath,
        };
    }
}
