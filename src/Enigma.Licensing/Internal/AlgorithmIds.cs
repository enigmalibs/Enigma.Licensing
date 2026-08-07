namespace Enigma.Licensing.Internal;

/// <summary>
/// The single source of truth for the mapping between <see cref="LicenseSignatureAlgorithm"/> and the
/// identifier strings that travel in the <c>algorithm</c> field of an <c>enigma-license/1</c> envelope
/// and an <c>enigma-key/1</c> key file.
/// </summary>
/// <remarks>
/// <para>
/// Nothing else in the library may spell one of these strings. Two independent enum-to-string
/// conversions are how a format ends up writing <c>"ML-DSA-65"</c> in one place and <c>"MLDsa65"</c>
/// in another; keeping the table here means a new enum member breaks exactly one file, and
/// <c>AlgorithmIdsTests</c> fails until it is added.
/// </para>
/// <para>
/// Both directions use the <b>unknown → null/false</b> pattern rather than throwing. This type maps;
/// it does not decide what an unmappable value means. A caller reading a license treats an unknown
/// identifier as a validation failure, whereas a caller handed an undefined enum value has been given
/// a bad argument — two different outcomes from one lookup, so the decision stays with the caller.
/// </para>
/// <para>
/// Matching is <b>ordinal and case-sensitive</b>. The identifiers are format constants, not user
/// input: a document saying <c>"rsa-sha256"</c> is not an <c>enigma-license/1</c> document, and
/// quietly accepting it would make the format's own conformance untestable.
/// </para>
/// </remarks>
internal static class AlgorithmIds
{
    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.RsaSha256"/>.</summary>
    internal const string RsaSha256 = "RSA-SHA256";

    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.RsaSha384"/>.</summary>
    internal const string RsaSha384 = "RSA-SHA384";

    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.RsaSha512"/>.</summary>
    internal const string RsaSha512 = "RSA-SHA512";

    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.MLDsa44"/>.</summary>
    internal const string MLDsa44 = "ML-DSA-44";

    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.MLDsa65"/>.</summary>
    internal const string MLDsa65 = "ML-DSA-65";

    /// <summary>Identifier for <see cref="LicenseSignatureAlgorithm.MLDsa87"/>.</summary>
    internal const string MLDsa87 = "ML-DSA-87";

    /// <summary>
    /// Returns the wire identifier for <paramref name="algorithm"/>, or <see langword="null"/> when the
    /// value is not a defined <see cref="LicenseSignatureAlgorithm"/> member.
    /// </summary>
    /// <param name="algorithm">The algorithm to write to a license or key envelope.</param>
    /// <returns>The identifier string, or <see langword="null"/> for an undefined enum value.</returns>
    internal static string? ToIdentifier(LicenseSignatureAlgorithm algorithm) => algorithm switch
    {
        LicenseSignatureAlgorithm.RsaSha256 => RsaSha256,
        LicenseSignatureAlgorithm.RsaSha384 => RsaSha384,
        LicenseSignatureAlgorithm.RsaSha512 => RsaSha512,
        LicenseSignatureAlgorithm.MLDsa44 => MLDsa44,
        LicenseSignatureAlgorithm.MLDsa65 => MLDsa65,
        LicenseSignatureAlgorithm.MLDsa87 => MLDsa87,
        _ => null,
    };

    /// <summary>
    /// Resolves a wire identifier read from an envelope back to its <see cref="LicenseSignatureAlgorithm"/>.
    /// </summary>
    /// <param name="identifier">
    /// The identifier as it appeared in the document. <see langword="null"/> — an absent field — is a
    /// normal input here and simply fails to resolve.
    /// </param>
    /// <param name="algorithm">
    /// The resolved algorithm on success; otherwise <see cref="LicenseSignatureAlgorithm.RsaSha256"/>
    /// (the enum's default), which callers must not read when the method returns <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> when the identifier is one this library knows.</returns>
    internal static bool TryParse(string? identifier, out LicenseSignatureAlgorithm algorithm)
    {
        switch (identifier)
        {
            case RsaSha256:
                algorithm = LicenseSignatureAlgorithm.RsaSha256;
                return true;
            case RsaSha384:
                algorithm = LicenseSignatureAlgorithm.RsaSha384;
                return true;
            case RsaSha512:
                algorithm = LicenseSignatureAlgorithm.RsaSha512;
                return true;
            case MLDsa44:
                algorithm = LicenseSignatureAlgorithm.MLDsa44;
                return true;
            case MLDsa65:
                algorithm = LicenseSignatureAlgorithm.MLDsa65;
                return true;
            case MLDsa87:
                algorithm = LicenseSignatureAlgorithm.MLDsa87;
                return true;
            default:
                algorithm = default;
                return false;
        }
    }
}
