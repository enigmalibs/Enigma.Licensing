using System.Text;

namespace Enigma.Licensing.Keys;

/// <summary>
/// One half of a signing key pair, as read from — or about to be written to — an
/// <c>enigma-key/1</c> file.
/// </summary>
/// <remarks>
/// <para>
/// The key material itself is deliberately not on this type. What a caller can see is which
/// <see cref="Algorithm"/> the key belongs to, whether it is the <see cref="KeyKind.Public"/> or
/// <see cref="KeyKind.Private"/> half, and its <see cref="KeyId"/>; the bytes go to
/// <see cref="IKeyService"/> and to the generator and validator, and nowhere else. Keeping them
/// internal means a private key cannot be logged, serialized or copied by accident, and it keeps the
/// PEM-versus-raw difference between the RSA and ML-DSA families out of the public API.
/// </para>
/// <para>
/// Instances come from <see cref="IKeyService.GenerateKeyPair"/> or one of its load methods — there is
/// no public constructor, because a <see cref="LicensingKey"/> that had never been through the format
/// would be a promise the type cannot keep.
/// </para>
/// </remarks>
public sealed class LicensingKey
{
    /// <summary>
    /// The only key file format this library reads or writes: <c>"enigma-key/1"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is a small JSON object — the same idea as <see cref="LicenseEnvelope"/>, and equally
    /// meant to be readable when opened:
    /// </para>
    /// <code language="json">
    /// {
    ///   "format": "enigma-key/1",
    ///   "kind": "private",
    ///   "algorithm": "RSA-SHA256",
    ///   "keyId": "3F9A1C40D2B87E56",
    ///   "encryption": { "kdf": "argon2id", ... },
    ///   "key": "-----BEGIN PRIVATE KEY-----..."
    /// }
    /// </code>
    /// <para>
    /// One format covers both algorithm families and both halves of a pair, so tooling reads a key
    /// file without knowing in advance what is in it. <c>encryption</c> is present only on a
    /// password-protected private key. As with the license envelope, there is deliberately no reader
    /// for Enigma.LicenseManager's key files and none will be added.
    /// </para>
    /// </remarks>
    public const string CurrentFormat = "enigma-key/1";

    private readonly byte[] _material;

    // Internal so the only way to obtain a key is through IKeyService — every instance has therefore
    // either just been generated or just come out of a parsed enigma-key/1 document.
    internal LicensingKey(KeyKind kind, LicenseSignatureAlgorithm algorithm, string keyId, byte[] material)
    {
        Kind = kind;
        Algorithm = algorithm;
        KeyId = keyId;
        _material = material;
    }

    /// <summary>Whether this is the public (verifying) or private (signing) half of the pair.</summary>
    public KeyKind Kind { get; }

    /// <summary>
    /// The signature scheme this key belongs to. Fixed when the pair was generated: an RSA key cannot
    /// produce an ML-DSA signature, and the digest size is part of the RSA members' identity too.
    /// </summary>
    public LicenseSignatureAlgorithm Algorithm { get; }

    /// <summary>
    /// The pair's fingerprint — the uppercase hex of the first eight bytes of SHA-256 over the
    /// algorithm identifier followed by the public key material.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is computed from the <b>public</b> half, so both files of a pair carry the same value. That
    /// is what makes it useful: the generator stamps it into every license it signs
    /// (<see cref="LicenseEnvelope.KeyId"/>), and an application shipping several public keys uses it
    /// to pick the right one before verifying.
    /// </para>
    /// <para>
    /// It is an identifier, not a security control. It says which key <i>claims</i> to be involved;
    /// only a verified signature says which one actually was. Eight bytes is plenty to tell a handful
    /// of an issuer's own keys apart, and no amount would make an unverified claim trustworthy.
    /// </para>
    /// </remarks>
    public string KeyId { get; }

    /// <summary>
    /// The key material: the UTF-8 bytes of the PEM text for the RSA members, or the raw FIPS 204
    /// encoding for the ML-DSA members. Returned by reference — internal callers must not mutate it.
    /// </summary>
    internal byte[] Material => _material;

    /// <summary>
    /// The key material as PEM text. Only meaningful for the RSA members, whose material <i>is</i> a
    /// PEM string; the ML-DSA members have no PEM form.
    /// </summary>
    internal string Pem => Encoding.UTF8.GetString(_material, 0, _material.Length);
}
