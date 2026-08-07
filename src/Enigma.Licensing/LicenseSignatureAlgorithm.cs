namespace Enigma.Licensing;

/// <summary>
/// The signature scheme a license envelope was signed with, and the kind of key that signs and
/// verifies it.
/// </summary>
/// <remarks>
/// <para>
/// Every member maps to exactly one identifier string, which is what a license envelope carries in
/// its <c>algorithm</c> field — <c>"RSA-SHA256"</c>, <c>"RSA-SHA384"</c>, <c>"RSA-SHA512"</c>,
/// <c>"ML-DSA-44"</c>, <c>"ML-DSA-65"</c>, <c>"ML-DSA-87"</c>. The mapping is defined in one internal
/// place so the enum and the wire strings can never drift apart.
/// </para>
/// <para>
/// The set deliberately covers the whole signing surface of the underlying cryptographic library
/// rather than a curated subset: the RSA members are classical PKCS#1 v1.5 signatures over a
/// SHA-2 digest and work with PEM-encoded keys; the ML-DSA members are the FIPS 204 post-quantum
/// scheme at its three parameter sets and work with raw key encodings. Which family you pick decides
/// what a key file contains, so it is chosen once, when the key pair is generated.
/// </para>
/// </remarks>
public enum LicenseSignatureAlgorithm
{
    /// <summary>RSA PKCS#1 v1.5 over SHA-256. Wire identifier <c>"RSA-SHA256"</c>.</summary>
    RsaSha256,

    /// <summary>RSA PKCS#1 v1.5 over SHA-384. Wire identifier <c>"RSA-SHA384"</c>.</summary>
    RsaSha384,

    /// <summary>RSA PKCS#1 v1.5 over SHA-512. Wire identifier <c>"RSA-SHA512"</c>.</summary>
    RsaSha512,

    /// <summary>
    /// ML-DSA-44, the FIPS 204 post-quantum signature scheme at its smallest parameter set
    /// (NIST security category 2). Wire identifier <c>"ML-DSA-44"</c>.
    /// </summary>
    MLDsa44,

    /// <summary>
    /// ML-DSA-65, the FIPS 204 post-quantum signature scheme at its middle parameter set
    /// (NIST security category 3). Wire identifier <c>"ML-DSA-65"</c>.
    /// </summary>
    MLDsa65,

    /// <summary>
    /// ML-DSA-87, the FIPS 204 post-quantum signature scheme at its largest parameter set
    /// (NIST security category 5). Wire identifier <c>"ML-DSA-87"</c>.
    /// </summary>
    MLDsa87,
}
