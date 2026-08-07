using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Core.Asymmetric.Pqc;
using Enigma.Core.Asymmetric.PublicKey;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.Keys;

/// <summary>
/// The default <see cref="IKeyService"/> — key generation and <c>enigma-key/1</c> storage on top of
/// Enigma.Core.
/// </summary>
/// <remarks>
/// <para>
/// Stateless and safe to share: it holds only the two Enigma.Core services it dispatches to, both of
/// which are themselves stateless. A single instance per application is the intended use, which is why
/// <see cref="Microsoft.Extensions.DependencyInjection.EnigmaLicensingServiceCollectionExtensions.AddEnigmaLicensing"/>
/// registers it as a singleton.
/// </para>
/// <para>
/// RSA keys are generated <b>unencrypted</b> at the Enigma.Core layer even when a password is supplied.
/// Core's PEM-level encryption is deliberately unused: password protection happens once, at the file
/// level, over the material bytes, so that RSA and ML-DSA keys are protected identically rather than by
/// two different mechanisms with two different failure modes. See <see cref="KeyMaterialCipher"/>.
/// </para>
/// </remarks>
public sealed class KeyService : IKeyService
{
    private readonly IPublicKeyService _rsaService = new PublicKeyServiceFactory().CreatePublicKeyService();
    private readonly IMLDsaServiceFactory _mlDsaServiceFactory = new MLDsaServiceFactory();

    /// <inheritdoc />
    public LicensingKeyPair GenerateKeyPair(
        LicenseSignatureAlgorithm algorithm,
        int rsaKeySizeBits = KeyFormat.DefaultRsaKeySizeBits)
    {
        if (AlgorithmIds.ToIdentifier(algorithm) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm,
                "The algorithm is not a defined LicenseSignatureAlgorithm member.");
        }

        if (!CoreAlgorithms.IsRsa(algorithm))
        {
            IMLDsaService mlDsaService = _mlDsaServiceFactory.CreateMLDsaService(
                CoreAlgorithms.ToParameterSet(algorithm));

            (byte[] mlDsaPublicKey, byte[] mlDsaPrivateKey) = mlDsaService.GenerateKeyPair();

            return CreatePair(algorithm, mlDsaPublicKey, mlDsaPrivateKey);
        }

        if (rsaKeySizeBits < KeyFormat.MinRsaKeySizeBits || rsaKeySizeBits > KeyFormat.MaxRsaKeySizeBits)
        {
            throw new ArgumentOutOfRangeException(nameof(rsaKeySizeBits), rsaKeySizeBits,
                $"The RSA key size must be between {KeyFormat.MinRsaKeySizeBits} and " +
                $"{KeyFormat.MaxRsaKeySizeBits} bits.");
        }

        // password: null — the PEM is always written unencrypted here; the file-level cipher is what
        // protects a private key.
        (string publicKeyPem, string privateKeyPem) = _rsaService.GenerateRsaKeyPair(rsaKeySizeBits);

        return CreatePair(algorithm, Encoding.UTF8.GetBytes(publicKeyPem), Encoding.UTF8.GetBytes(privateKeyPem));
    }

    /// <inheritdoc />
    public async Task SaveKeyAsync(
        LicensingKey key,
        Stream output,
        char[]? password = null,
        CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        RequireUsablePassword(password);

        if (password is not null && key.Kind == KeyKind.Public)
        {
            throw new ArgumentException(
                "A public key is never encrypted — it is meant to be distributed. Pass null to write it.",
                nameof(password));
        }

        string algorithmId = AlgorithmIds.ToIdentifier(key.Algorithm)
            ?? throw new ArgumentException("The key's algorithm is not a defined member.", nameof(key));
        string kindId = KeyKindIds.ToIdentifier(key.Kind)
            ?? throw new ArgumentException("The key's kind is not a defined member.", nameof(key));

        KeyEncryption? encryption = null;
        string encodedKey;

        if (password is null)
        {
            encodedKey = KeyMaterial.Encode(key.Algorithm, key.Material);
        }
        else
        {
            (byte[] ciphertext, KeyEncryption parameters) = await KeyMaterialCipher
                .EncryptAsync(key.Material, password, cancellationToken)
                .ConfigureAwait(false);

            encryption = parameters;
            encodedKey = Convert.ToBase64String(ciphertext);
        }

        KeyEnvelope envelope = new()
        {
            Format = LicensingKey.CurrentFormat,
            Kind = kindId,
            Algorithm = algorithmId,
            KeyId = key.KeyId,
            Encryption = encryption,
            Key = encodedKey,
        };

        await JsonSerializer.SerializeAsync(output, envelope, LicenseJson.KeyFile, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LicensingKey> LoadKeyAsync(
        Stream input,
        char[]? password = null,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        RequireUsablePassword(password);

        KeyEnvelope envelope = await ReadEnvelopeAsync(input, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(envelope.Format, LicensingKey.CurrentFormat, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported key format '{envelope.Format}'; this library reads " +
                $"'{LicensingKey.CurrentFormat}'.",
                KeyMaterial.DocumentParameterName);
        }

        if (!KeyKindIds.TryParse(envelope.Kind, out KeyKind kind))
        {
            throw new ArgumentException(
                $"Unknown key kind '{envelope.Kind}'; expected '{KeyKindIds.Public}' or " +
                $"'{KeyKindIds.Private}'.",
                KeyMaterial.DocumentParameterName);
        }

        if (!AlgorithmIds.TryParse(envelope.Algorithm, out LicenseSignatureAlgorithm algorithm))
        {
            throw new ArgumentException(
                $"Unknown signature algorithm '{envelope.Algorithm}'; this library does not implement it.",
                KeyMaterial.DocumentParameterName);
        }

        if (kind == KeyKind.Public)
        {
            if (envelope.Encryption is not null)
            {
                throw new ArgumentException(
                    "The document is a public key carrying an 'encryption' object. The format never " +
                    "encrypts public keys, so the file is malformed.",
                    KeyMaterial.DocumentParameterName);
            }

            if (password is not null)
            {
                throw new ArgumentException(
                    "A public key is never encrypted, so no password is needed to load one.",
                    nameof(password));
            }
        }

        byte[] material = await RecoverMaterialAsync(envelope, algorithm, password, cancellationToken)
            .ConfigureAwait(false);

        KeyMaterial.Validate(kind, algorithm, material);

        return new LicensingKey(kind, algorithm, envelope.KeyId, material);
    }

    /// <inheritdoc />
    public async Task SaveKeyPairAsync(
        LicensingKeyPair pair,
        string publicKeyPath,
        string privateKeyPath,
        char[]? password = null,
        CancellationToken cancellationToken = default)
    {
        if (pair is null)
        {
            throw new ArgumentNullException(nameof(pair));
        }

        DocumentFile.RequirePath(publicKeyPath, nameof(publicKeyPath));
        DocumentFile.RequirePath(privateKeyPath, nameof(privateKeyPath));

        if (pair.PublicKey.Kind != KeyKind.Public || pair.PrivateKey.Kind != KeyKind.Private)
        {
            throw new ArgumentException(
                "The pair's halves are the wrong way round: PublicKey must be a public key and " +
                "PrivateKey a private one.",
                nameof(pair));
        }

        // Writing both halves to one path would leave whichever was written second, silently
        // destroying the other — and if that were the private key, an unencrypted one.
        if (string.Equals(Path.GetFullPath(publicKeyPath), Path.GetFullPath(privateKeyPath), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The public and private keys cannot be written to the same file.",
                nameof(privateKeyPath));
        }

        await SaveToPathAsync(pair.PublicKey, publicKeyPath, password: null, cancellationToken)
            .ConfigureAwait(false);
        await SaveToPathAsync(pair.PrivateKey, privateKeyPath, password, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LicensingKey> LoadKeyAsync(
        string path,
        char[]? password = null,
        CancellationToken cancellationToken = default)
    {
        DocumentFile.RequirePath(path, nameof(path));

        using FileStream input = DocumentFile.OpenRead(path);

        return await LoadKeyAsync(input, password, cancellationToken).ConfigureAwait(false);
    }

    private static LicensingKeyPair CreatePair(
        LicenseSignatureAlgorithm algorithm,
        byte[] publicMaterial,
        byte[] privateMaterial)
    {
        // Computed from the public half only, so both files of the pair carry the same value.
        string keyId = KeyFingerprint.Compute(algorithm, publicMaterial);

        return new LicensingKeyPair(
            new LicensingKey(KeyKind.Public, algorithm, keyId, publicMaterial),
            new LicensingKey(KeyKind.Private, algorithm, keyId, privateMaterial));
    }

    private static async Task<KeyEnvelope> ReadEnvelopeAsync(Stream input, CancellationToken cancellationToken)
    {
        KeyEnvelope? envelope;

        try
        {
            envelope = await JsonSerializer
                .DeserializeAsync(input, LicenseJson.KeyFile, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The stream does not contain a well-formed key file.",
                KeyMaterial.DocumentParameterName, ex);
        }

        return envelope
            ?? throw new ArgumentException("The stream contains the JSON literal 'null', not a key file.",
                KeyMaterial.DocumentParameterName);
    }

    private static async Task<byte[]> RecoverMaterialAsync(
        KeyEnvelope envelope,
        LicenseSignatureAlgorithm algorithm,
        char[]? password,
        CancellationToken cancellationToken)
    {
        if (envelope.Encryption is null)
        {
            // A password for a private key that is not encrypted is unnecessary rather than wrong: a
            // caller holding one has no way to know in advance whether a given file needs it, so it is
            // ignored. (For a public key it is rejected — that one is never encrypted by anybody.)
            return KeyMaterial.Decode(algorithm, envelope.Key);
        }

        if (password is null)
        {
            throw new CryptographicException(
                "The private key is password-protected; a password is required to load it.");
        }

        return await KeyMaterialCipher
            .DecryptAsync(KeyMaterial.DecodeBase64(envelope.Key), envelope.Encryption, password, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SaveToPathAsync(
        LicensingKey key,
        string path,
        char[]? password,
        CancellationToken cancellationToken)
    {
        // DocumentFile.Create is what makes the missing parent directory a non-issue.
        using FileStream output = DocumentFile.Create(path);

        await SaveKeyAsync(key, output, password, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireUsablePassword(char[]? password)
    {
        if (password is { Length: 0 })
        {
            throw new ArgumentException(
                "The password must not be empty. Pass null to leave the key unencrypted.",
                nameof(password));
        }
    }
}
