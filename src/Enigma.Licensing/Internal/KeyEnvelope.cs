namespace Enigma.Licensing.Internal;

/// <summary>
/// The on-disk shape of an <c>enigma-key/1</c> file: six fields, in this order, of which
/// <c>encryption</c> is the only optional one.
/// </summary>
/// <remarks>
/// <para>
/// Internal, unlike <see cref="LicenseEnvelope"/>, and for a reason: a license envelope is something
/// callers hand around, inspect and pass to a validator, whereas a key file is only ever a way to get
/// a <see cref="Keys.LicensingKey"/> in and out of storage. Exposing it would put the PEM-versus-raw
/// difference, and the encrypted-versus-plain difference, back on the public API that
/// <see cref="Keys.LicensingKey"/> exists to hide.
/// </para>
/// <para>
/// <b>The document is not signed, and nothing here pretends otherwise.</b> AES-GCM authenticates the
/// private material and the material alone; the fields around it are not covered. Editing
/// <c>algorithm</c> or <c>keyId</c> in someone else's key file is therefore possible — and pointless,
/// because it can only make the key fail to load or produce signatures that do not verify. The value
/// that must be trustworthy is the <b>public</b> key an application ships, and that one is protected
/// by being inside the application, not by anything in this file.
/// </para>
/// </remarks>
internal sealed record KeyEnvelope
{
    /// <summary>The format version. Always <see cref="Keys.LicensingKey.CurrentFormat"/> for files this library writes.</summary>
    public required string Format { get; init; }

    /// <summary>Which half of the pair this is: <c>"public"</c> or <c>"private"</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The signature scheme the key belongs to, as a wire identifier — <c>"RSA-SHA256"</c> and so on.</summary>
    public required string Algorithm { get; init; }

    /// <summary>The pair's fingerprint, identical in both halves' files.</summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// How <see cref="Key"/> is protected, or <see langword="null"/> when it is not. Only ever present
    /// on a private key.
    /// </summary>
    public KeyEncryption? Encryption { get; init; }

    /// <summary>
    /// The key material, in one of three encodings — which one is determined by
    /// <see cref="Encryption"/> and <see cref="Algorithm"/>, never guessed from the content.
    /// </summary>
    /// <remarks>
    /// With <see cref="Encryption"/> present it is base64 of the AES-GCM ciphertext (tag appended),
    /// whatever the algorithm. Without it, an RSA key is its PEM text — recognizable at a glance for
    /// what it is, though JSON string escaping means it is not literally copy-pasteable — and an ML-DSA
    /// key is base64 of the raw FIPS 204 encoding, which has no text form to preserve.
    /// </remarks>
    public required string Key { get; init; }
}
