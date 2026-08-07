using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the library's key service: it hands back a pair it was given, keeps every argument it was
/// called with, and can be made to fail or to pause in the middle.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pair has to be a real one.</b> <see cref="LicensingKey"/>'s constructor is internal to the
/// library — deliberately, so that the only way to hold a key is to have generated or parsed one — and the
/// application's test assembly is not a friend of it. A caller therefore generates one real ML-DSA-44 pair,
/// which costs microseconds, and hands it here.
/// </para>
/// <para>
/// Only the two members the keys page uses are implemented. The rest throw, so a page that started calling
/// one fails loudly in a test rather than silently doing nothing.
/// </para>
/// </remarks>
internal sealed class FakeKeyService : IKeyService
{
    private readonly LicensingKeyPair _pair;

    /// <summary>Initializes a new instance of the <see cref="FakeKeyService"/> class.</summary>
    /// <param name="pair">The pair every generation returns.</param>
    internal FakeKeyService(LicensingKeyPair pair) => _pair = pair;

    /// <summary>Gets or sets a failure to raise instead of generating.</summary>
    internal Exception? FailGeneration { get; set; }

    /// <summary>Gets or sets a failure to raise instead of saving.</summary>
    internal Exception? FailSave { get; set; }

    /// <summary>
    /// Gets or sets work to run inside the save, after its arguments have been recorded — the seam a test
    /// uses to look at the page while the operation is still running.
    /// </summary>
    internal Func<Task>? DuringSave { get; set; }

    /// <summary>Gets how many pairs were generated.</summary>
    internal int Generations { get; private set; }

    /// <summary>Gets how many saves were asked for.</summary>
    internal int Saves { get; private set; }

    /// <summary>Gets the scheme the last generation asked for.</summary>
    internal LicenseSignatureAlgorithm? Algorithm { get; private set; }

    /// <summary>Gets the modulus size the last generation asked for.</summary>
    internal int? RsaKeySizeBits { get; private set; }

    /// <summary>Gets where the last save was told to write the public half.</summary>
    internal string? PublicKeyPath { get; private set; }

    /// <summary>Gets where the last save was told to write the private half.</summary>
    internal string? PrivateKeyPath { get; private set; }

    /// <summary>
    /// Gets the password array the last save was handed — the array itself, so a test can watch what the
    /// caller does to it afterwards.
    /// </summary>
    internal char[]? Password { get; private set; }

    /// <summary>Gets what the password said while the save could still read it.</summary>
    internal string? PasswordText { get; private set; }

    /// <inheritdoc/>
    public LicensingKeyPair GenerateKeyPair(LicenseSignatureAlgorithm algorithm, int rsaKeySizeBits = 3072)
    {
        Generations++;
        Algorithm = algorithm;
        RsaKeySizeBits = rsaKeySizeBits;

        return FailGeneration is null ? _pair : throw FailGeneration;
    }

    /// <inheritdoc/>
    public async Task SaveKeyPairAsync(
        LicensingKeyPair pair,
        string publicKeyPath,
        string privateKeyPath,
        char[]? password = null,
        CancellationToken cancellationToken = default)
    {
        Saves++;
        PublicKeyPath = publicKeyPath;
        PrivateKeyPath = privateKeyPath;
        Password = password;
        PasswordText = password is null ? null : new string(password);

        if (DuringSave is not null)
        {
            await DuringSave().ConfigureAwait(false);
        }

        if (FailSave is not null)
        {
            throw FailSave;
        }
    }

    /// <inheritdoc/>
    public Task SaveKeyAsync(
        LicensingKey key,
        Stream output,
        char[]? password = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<LicensingKey> LoadKeyAsync(
        Stream input,
        char[]? password = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<LicensingKey> LoadKeyAsync(
        string path,
        char[]? password = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
