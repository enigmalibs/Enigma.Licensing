namespace Enigma.Licensing.Internal;

/// <summary>
/// The fixed constants of the <c>enigma-key/1</c> format: the names of the two cryptographic
/// primitives it uses, the sizes it fixes, the key-derivation costs it writes by default, and the
/// bounds it accepts back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sizes are format invariants; costs are only defaults.</b> The salt, nonce, derived-key and tag
/// sizes are not written to the file and are not selectable — changing one would be a different format
/// version. The Argon2id costs <i>are</i> written to the file, because a key encrypted today has to
/// stay openable after the defaults are raised, which means the reader takes them from the document
/// rather than from this table.
/// </para>
/// <para>
/// <b>Which is exactly why the maxima exist.</b> A cost read from an untrusted document is an
/// instruction to allocate memory and burn CPU: a file asking for a terabyte of Argon2 memory would
/// otherwise take the process down before any password was even checked. The bounds are the
/// <c>Enigma.DataEncryption</c> ones, which are far above any honest setting and far below anything
/// dangerous.
/// </para>
/// </remarks>
internal static class KeyFormat
{
    /// <summary>The <c>kdf</c> value: Argon2id, RFC 9106's side-channel-resistant hybrid variant.</summary>
    internal const string Argon2idKdf = "argon2id";

    /// <summary>The <c>cipher</c> value: AES-256 in GCM, an AEAD, so a tampered key file fails to decrypt.</summary>
    internal const string Aes256GcmCipher = "AES-256-GCM";

    /// <summary>Default Argon2id passes over memory: 3 — RFC 9106's second recommended option.</summary>
    internal const int Argon2Iterations = 3;

    /// <summary>Default Argon2id memory cost: 65,536 KiB (64 MiB) — RFC 9106's second recommended option.</summary>
    internal const int Argon2MemorySizeKb = 65_536;

    /// <summary>Default Argon2id lanes: 4 — RFC 9106's second recommended option.</summary>
    internal const int Argon2DegreeOfParallelism = 4;

    /// <summary>The largest iteration count accepted from a key file.</summary>
    internal const int MaxArgon2Iterations = 64;

    /// <summary>The largest memory cost, in kibibytes, accepted from a key file: 1,048,576 KiB (1 GiB).</summary>
    internal const int MaxArgon2MemorySizeKb = 1_048_576;

    /// <summary>The largest degree of parallelism accepted from a key file.</summary>
    internal const int MaxArgon2DegreeOfParallelism = 64;

    /// <summary>The Argon2id salt size in bytes: 16. Fixed by the format.</summary>
    internal const int SaltSizeBytes = 16;

    /// <summary>The GCM nonce size in bytes: 12 (96-bit, the size GCM is defined for). Fixed by the format.</summary>
    internal const int NonceSizeBytes = 12;

    /// <summary>The derived AES key size in bytes: 32 (256-bit). Fixed by the format.</summary>
    internal const int DerivedKeySizeBytes = 32;

    /// <summary>The GCM authentication tag size in bits: 128, the maximum. Fixed by the format.</summary>
    internal const int GcmMacSizeBits = 128;

    /// <summary>How many bytes of the SHA-256 digest become the <c>keyId</c>: 8, rendered as 16 hex digits.</summary>
    internal const int KeyIdSizeBytes = 8;

    /// <summary>The smallest RSA modulus this library will generate: 2048 bits.</summary>
    internal const int MinRsaKeySizeBits = 2048;

    /// <summary>The largest RSA modulus this library will generate: 8192 bits.</summary>
    internal const int MaxRsaKeySizeBits = 8192;

    /// <summary>
    /// The RSA modulus size used when the caller does not choose one: 3072 bits — roughly 128-bit
    /// security, and the size NIST expects for keys in use past 2030.
    /// </summary>
    internal const int DefaultRsaKeySizeBits = 3072;
}
