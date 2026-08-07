using System;
using Enigma.Core;
using Enigma.Core.Asymmetric.Pqc;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Maps a <see cref="LicenseSignatureAlgorithm"/> onto the Enigma.Core primitive that implements it.
/// </summary>
/// <remarks>
/// <para>
/// A different job from <see cref="AlgorithmIds"/>, which maps the enum to the strings that go in a
/// document. This one maps it to the cryptography, and it is the only place in the library that names
/// an Enigma.Core algorithm type — so the two families' differences (PEM strings versus raw bytes, a
/// digest choice versus a parameter set) are decided here and nowhere else.
/// </para>
/// <para>
/// Unlike <see cref="AlgorithmIds"/> these lookups <b>throw</b> on a value they cannot map. There is no
/// caller-facing meaning to fall back on: an algorithm that reached this point has already been
/// resolved from a document or supplied as a defined enum member, so an unmappable value is a bug in
/// this library, not bad input.
/// </para>
/// </remarks>
internal static class CoreAlgorithms
{
    /// <summary>
    /// Whether <paramref name="algorithm"/> is one of the RSA members — the ones whose key material is
    /// PEM text and whose signatures come from <c>IPublicKeyService</c>.
    /// </summary>
    /// <param name="algorithm">The algorithm to classify.</param>
    /// <returns><see langword="true"/> for the RSA members, <see langword="false"/> for the ML-DSA members.</returns>
    internal static bool IsRsa(LicenseSignatureAlgorithm algorithm) => algorithm is
        LicenseSignatureAlgorithm.RsaSha256 or
        LicenseSignatureAlgorithm.RsaSha384 or
        LicenseSignatureAlgorithm.RsaSha512;

    /// <summary>
    /// Returns the Enigma.Core RSASSA-PKCS1-v1_5 scheme behind one of the RSA members.
    /// </summary>
    /// <param name="algorithm">One of the RSA members.</param>
    /// <returns>The matching Enigma.Core signature algorithm — the digest choice, paired with RSA.</returns>
    /// <remarks>
    /// The digest is what distinguishes the three RSA members from one another, and it is chosen from
    /// the <i>envelope's</i> algorithm identifier rather than the key's. That is deliberate: an RSA key
    /// can sign under any of the three digests, so rewriting the identifier in a stored license makes
    /// the signature be verified under a scheme it was not produced with, and it fails.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="algorithm"/> is not an RSA member. Callers gate on <see cref="IsRsa"/> first.
    /// </exception>
    internal static RsaSignatureAlgorithm ToRsaSignatureAlgorithm(LicenseSignatureAlgorithm algorithm) =>
        algorithm switch
        {
            LicenseSignatureAlgorithm.RsaSha256 => RsaSignatureAlgorithm.Sha256WithRsa,
            LicenseSignatureAlgorithm.RsaSha384 => RsaSignatureAlgorithm.Sha384WithRsa,
            LicenseSignatureAlgorithm.RsaSha512 => RsaSignatureAlgorithm.Sha512WithRsa,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm,
                "Only the RSA members map to an RSASSA-PKCS1-v1_5 scheme."),
        };

    /// <summary>
    /// Returns the FIPS 204 parameter set behind one of the ML-DSA members.
    /// </summary>
    /// <param name="algorithm">One of the ML-DSA members.</param>
    /// <returns>The matching Enigma.Core parameter set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="algorithm"/> is not an ML-DSA member. Callers gate on <see cref="IsRsa"/> first.
    /// </exception>
    internal static MLDsaParameterSet ToParameterSet(LicenseSignatureAlgorithm algorithm) => algorithm switch
    {
        LicenseSignatureAlgorithm.MLDsa44 => MLDsaParameterSet.MLDsa44,
        LicenseSignatureAlgorithm.MLDsa65 => MLDsaParameterSet.MLDsa65,
        LicenseSignatureAlgorithm.MLDsa87 => MLDsaParameterSet.MLDsa87,
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm,
            "Only the ML-DSA members have a FIPS 204 parameter set."),
    };
}
