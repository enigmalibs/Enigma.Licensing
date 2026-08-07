using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Core.KeyDerivation;
using Enigma.Core.Padding;
using Enigma.Core.Symmetric.BlockCiphers;
using Enigma.Core.Utils;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The password protection of a private key: Argon2id turns the password into a key, AES-256-GCM
/// encrypts the material with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Encryption happens here, at the envelope layer, for both algorithm families alike.</b> RSA has a
/// perfectly good encrypted-PEM form and Enigma.Core will write one, but ML-DSA has no equivalent — so
/// using it would mean one protection scheme for RSA keys and a second for ML-DSA keys, with different
/// KDFs, different parameters and two sets of failure modes to document. One scheme over the material
/// bytes, whatever they are, is the whole reason <c>enigma-key/1</c> exists.
/// </para>
/// <para>
/// GCM is an AEAD, so the ciphertext authenticates itself: a key file with a flipped byte fails to
/// decrypt rather than yielding subtly wrong material. The tag is appended to the ciphertext by the
/// cipher itself and travels inside the same base64 blob; only its length is recorded, in
/// <see cref="KeyEncryption.TagBits"/>.
/// </para>
/// <para>
/// A wrong password is indistinguishable from a tampered file, and deliberately so — both fail the tag
/// check, and both surface as <see cref="CryptographicException"/>. There is no key-confirmation value
/// that would let a caller tell them apart, because there is nothing useful to do differently.
/// </para>
/// <para>
/// The caller's <see cref="char"/> array is never touched: the UTF-8 buffer derived from it, and the
/// AES key derived from that, are both cleared in a <c>finally</c>, but the password itself belongs to
/// whoever created it (the Enigma.Core convention).
/// </para>
/// </remarks>
internal static class KeyMaterialCipher
{
    // Both Enigma.Core services are stateless per call — Argon2Service holds nothing, and the AES
    // service builds a fresh cipher and rents its own buffer each time — so one shared instance each
    // is safe across threads.
    private static readonly IArgon2Service Argon2 = new Argon2ServiceFactory().CreateArgon2Service();
    private static readonly IBlockCipherService Aes = new BlockCipherServiceFactory().CreateAesService();

    /// <summary>
    /// Encrypts key material under a password, at the format's current cost settings.
    /// </summary>
    /// <param name="material">The private key material to protect.</param>
    /// <param name="password">The password. Not modified and not cleared.</param>
    /// <param name="cancellationToken">Token to cancel the encryption.</param>
    /// <returns>The ciphertext with its tag appended, and the parameters needed to reverse it.</returns>
    internal static async Task<(byte[] Ciphertext, KeyEncryption Parameters)> EncryptAsync(
        byte[] material,
        char[] password,
        CancellationToken cancellationToken)
    {
        byte[] salt = RandomUtils.GenerateRandomBytes(KeyFormat.SaltSizeBytes);
        byte[] nonce = RandomUtils.GenerateRandomBytes(KeyFormat.NonceSizeBytes);
        byte[] derivedKey = DeriveKey(
            password,
            salt,
            KeyFormat.Argon2Iterations,
            KeyFormat.Argon2MemorySizeKb,
            KeyFormat.Argon2DegreeOfParallelism);

        try
        {
            using MemoryStream input = new(material, writable: false);
            using MemoryStream output = new();

            await Aes.EncryptAsync(
                input,
                output,
                derivedKey,
                nonce,
                BlockCipherMode.Gcm,
                PaddingScheme.None,
                KeyFormat.GcmMacSizeBits,
                associatedData: null,
                progress: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            KeyEncryption parameters = new()
            {
                Kdf = KeyFormat.Argon2idKdf,
                Iterations = KeyFormat.Argon2Iterations,
                MemoryKb = KeyFormat.Argon2MemorySizeKb,
                Parallelism = KeyFormat.Argon2DegreeOfParallelism,
                Salt = salt,
                Cipher = KeyFormat.Aes256GcmCipher,
                Nonce = nonce,
                TagBits = KeyFormat.GcmMacSizeBits,
            };

            return (output.ToArray(), parameters);
        }
        finally
        {
            Array.Clear(derivedKey, 0, derivedKey.Length);
        }
    }

    /// <summary>
    /// Recovers key material from its ciphertext, using the parameters the file recorded.
    /// </summary>
    /// <param name="ciphertext">The ciphertext with its tag appended.</param>
    /// <param name="parameters">The <c>encryption</c> object read from the file.</param>
    /// <param name="password">The password. Not modified and not cleared.</param>
    /// <param name="cancellationToken">Token to cancel the decryption.</param>
    /// <returns>The recovered material.</returns>
    /// <exception cref="CryptographicException">
    /// The recorded parameters are not ones this format accepts, or the material failed to
    /// authenticate — a wrong password, or a modified file.
    /// </exception>
    internal static async Task<byte[]> DecryptAsync(
        byte[] ciphertext,
        KeyEncryption parameters,
        char[] password,
        CancellationToken cancellationToken)
    {
        ValidateParameters(parameters);

        byte[] derivedKey = DeriveKey(
            password,
            parameters.Salt,
            parameters.Iterations,
            parameters.MemoryKb,
            parameters.Parallelism);

        try
        {
            using MemoryStream input = new(ciphertext, writable: false);
            using MemoryStream output = new();

            await Aes.DecryptAsync(
                input,
                output,
                derivedKey,
                parameters.Nonce,
                BlockCipherMode.Gcm,
                PaddingScheme.None,
                parameters.TagBits,
                associatedData: null,
                progress: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return output.ToArray();
        }
        finally
        {
            Array.Clear(derivedKey, 0, derivedKey.Length);
        }
    }

    /// <summary>
    /// Rejects an <c>encryption</c> object this format cannot honour — including one whose costs are
    /// an attack rather than a setting.
    /// </summary>
    /// <remarks>
    /// The upper bounds matter more than they look: the costs are read from the document <i>before</i>
    /// the password is checked, so a file asking for a terabyte of Argon2 memory would take the process
    /// down without ever being authenticated. See <see cref="KeyFormat"/>.
    /// </remarks>
    private static void ValidateParameters(KeyEncryption parameters)
    {
        if (!string.Equals(parameters.Kdf, KeyFormat.Argon2idKdf, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                $"Unsupported key-derivation function '{parameters.Kdf}'; this format uses " +
                $"'{KeyFormat.Argon2idKdf}'.");
        }

        if (!string.Equals(parameters.Cipher, KeyFormat.Aes256GcmCipher, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                $"Unsupported cipher '{parameters.Cipher}'; this format uses " +
                $"'{KeyFormat.Aes256GcmCipher}'.");
        }

        RequireInRange(parameters.Iterations, KeyFormat.MaxArgon2Iterations, "iteration count");
        RequireInRange(parameters.MemoryKb, KeyFormat.MaxArgon2MemorySizeKb, "memory cost");
        RequireInRange(parameters.Parallelism, KeyFormat.MaxArgon2DegreeOfParallelism, "degree of parallelism");

        if (parameters.Salt.Length == 0)
        {
            throw new CryptographicException("The key file records an empty key-derivation salt.");
        }

        if (parameters.Nonce.Length == 0)
        {
            throw new CryptographicException("The key file records an empty cipher nonce.");
        }

        if (!GcmMacSize.IsValid(parameters.TagBits))
        {
            throw new CryptographicException(
                $"The key file records an invalid authentication tag size of {parameters.TagBits} bits. " +
                GcmMacSize.RangeDescription);
        }
    }

    private static void RequireInRange(int value, int maximum, string description)
    {
        if (value < 1 || value > maximum)
        {
            throw new CryptographicException(
                $"The key file's key-derivation {description} of {value} is outside the accepted range " +
                $"of 1 to {maximum}.");
        }
    }

    // The UTF-8 buffer is this method's own copy and is cleared before it returns; the caller's
    // char[] is left exactly as it was passed in.
    private static byte[] DeriveKey(char[] password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            return Argon2.DeriveKey(
                passwordBytes,
                salt,
                iterations,
                memoryKb,
                parallelism,
                KeyFormat.DerivedKeySizeBytes);
        }
        finally
        {
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
        }
    }
}
