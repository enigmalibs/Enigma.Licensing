namespace Enigma.Licensing.Internal;

/// <summary>
/// The <c>encryption</c> object of an <c>enigma-key/1</c> file — everything a reader needs to turn a
/// password back into the AES key that opens the private material.
/// </summary>
/// <remarks>
/// <para>
/// Present only on a password-protected private key; a public key never carries one, and neither does
/// an unencrypted private key. Its presence <i>is</i> the signal that <c>key</c> holds base64
/// ciphertext rather than plain material.
/// </para>
/// <para>
/// Everything here is public by design. A salt and a nonce are not secrets — they exist to make the
/// derivation and the encryption unique, and both are useless without the password. Recording the
/// costs alongside them is what lets the defaults be raised later without stranding keys encrypted
/// under the old ones.
/// </para>
/// <para>
/// The authentication tag is <b>not</b> a field: AES-GCM appends it to the ciphertext, so it travels
/// inside <c>key</c>. <see cref="TagBits"/> records how long it is, which is what a reader has to know
/// to find the boundary.
/// </para>
/// </remarks>
internal sealed record KeyEncryption
{
    /// <summary>The key-derivation function. Always <see cref="KeyFormat.Argon2idKdf"/> in this format version.</summary>
    public required string Kdf { get; init; }

    /// <summary>The Argon2id passes over memory used when this key was encrypted.</summary>
    public required int Iterations { get; init; }

    /// <summary>The Argon2id memory cost in kibibytes used when this key was encrypted.</summary>
    public required int MemoryKb { get; init; }

    /// <summary>The Argon2id lane count used when this key was encrypted.</summary>
    public required int Parallelism { get; init; }

    /// <summary>The Argon2id salt, base64 in the file. Random per encryption.</summary>
    public required byte[] Salt { get; init; }

    /// <summary>The symmetric cipher. Always <see cref="KeyFormat.Aes256GcmCipher"/> in this format version.</summary>
    public required string Cipher { get; init; }

    /// <summary>The GCM nonce, base64 in the file. Random per encryption — never reused with a derived key.</summary>
    public required byte[] Nonce { get; init; }

    /// <summary>The length in bits of the GCM tag appended to the ciphertext.</summary>
    public required int TagBits { get; init; }
}
