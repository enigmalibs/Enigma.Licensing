using System;
using Enigma.Core.Asymmetric.Pqc;
using Enigma.Core.Asymmetric.PublicKey;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Signs and verifies payload bytes, dispatching to whichever Enigma.Core primitive the algorithm
/// names.
/// </summary>
/// <remarks>
/// <para>
/// The generator and the validator both need exactly this and nothing else, so it lives in one place:
/// the two families differ in what a key <i>is</i> — a PEM string for RSA, raw FIPS 204 bytes for
/// ML-DSA — and that difference is resolved here rather than twice.
/// </para>
/// <para>
/// <b>Verification takes the algorithm as a separate argument from the key.</b> That looks redundant,
/// since a <see cref="LicensingKey"/> already knows its own algorithm, and it is the whole point: the
/// validator passes the algorithm the <i>envelope</i> declares. A license whose <c>algorithm</c> field
/// has been rewritten is therefore checked under the scheme the file claims, using the key the
/// application actually trusts, and the mismatch shows up as a signature that does not verify. Signing
/// has no such split — the generator signs under the key's own algorithm, because there is nothing
/// else it could mean.
/// </para>
/// </remarks>
internal sealed class LicenseSignatures
{
    private readonly IPublicKeyService _rsaService = new PublicKeyServiceFactory().CreatePublicKeyService();
    private readonly IMLDsaServiceFactory _mlDsaServiceFactory = new MLDsaServiceFactory();

    /// <summary>
    /// Signs <paramref name="payload"/> with <paramref name="privateKey"/>, under that key's own
    /// algorithm.
    /// </summary>
    /// <param name="privateKey">The private half of the signing pair.</param>
    /// <param name="payload">The exact bytes to sign — the payload as it will be stored.</param>
    /// <returns>The signature.</returns>
    /// <remarks>
    /// The RSA path passes <see langword="null"/> for Enigma.Core's PEM password because an
    /// <c>enigma-key/1</c> file never holds a PEM that is encrypted at the PEM layer; by the time a key
    /// reaches here its material is already in the clear.
    /// </remarks>
    internal byte[] Sign(LicensingKey privateKey, byte[] payload) =>
        CoreAlgorithms.IsRsa(privateKey.Algorithm)
            ? _rsaService.Sign(
                payload,
                privateKey.Pem,
                CoreAlgorithms.ToRsaSignatureAlgorithm(privateKey.Algorithm),
                password: null)
            : CreateMLDsaService(privateKey.Algorithm).Sign(payload, privateKey.Material);

    /// <summary>
    /// Checks <paramref name="signature"/> against <paramref name="payload"/> under
    /// <paramref name="algorithm"/>, using <paramref name="publicKey"/>.
    /// </summary>
    /// <param name="algorithm">
    /// The scheme to verify under — the one the envelope declares, <b>not</b> the key's own.
    /// </param>
    /// <param name="publicKey">The public half the application trusts.</param>
    /// <param name="payload">The bytes the signature is supposed to cover.</param>
    /// <param name="signature">The signature to check.</param>
    /// <returns>
    /// <see langword="true"/> only when the signature genuinely verifies. Every failure — a bad
    /// signature, a key from the wrong family, an ML-DSA key of the wrong parameter set, material the
    /// primitive cannot parse — comes back as <see langword="false"/> rather than an exception, because
    /// to the validator they are one verdict: this key does not vouch for these bytes.
    /// </returns>
    internal bool Verify(
        LicenseSignatureAlgorithm algorithm,
        LicensingKey publicKey,
        byte[] payload,
        byte[] signature)
    {
        // A key from the other family cannot be fed to this primitive at all: PEM text is not a FIPS
        // 204 encoding and vice versa. Caught here so the failure is a verdict rather than whatever
        // exception the provider would raise several layers down.
        if (CoreAlgorithms.IsRsa(algorithm) != CoreAlgorithms.IsRsa(publicKey.Algorithm))
        {
            return false;
        }

        try
        {
            return CoreAlgorithms.IsRsa(algorithm)
                ? _rsaService.Verify(
                    payload,
                    signature,
                    publicKey.Pem,
                    CoreAlgorithms.ToRsaSignatureAlgorithm(algorithm))
                : CreateMLDsaService(algorithm).Verify(payload, signature, publicKey.Material);
        }
        catch (Exception ex) when (IsVerificationFailure(ex))
        {
            return false;
        }
    }

    private IMLDsaService CreateMLDsaService(LicenseSignatureAlgorithm algorithm) =>
        _mlDsaServiceFactory.CreateMLDsaService(CoreAlgorithms.ToParameterSet(algorithm));

    // Deliberately broad, and deliberately not a bare catch-all. Enigma.Core wraps BouncyCastle, whose
    // "this is not a key / not a signature" failures surface as several unrelated exception types and
    // are not part of Core's documented contract; enumerating them would make this brittle against a
    // Core or BouncyCastle upgrade for no gain, since every one of them means the same thing here.
    // What must still escape is the class of failure that says the process is in trouble rather than
    // the license — those are rethrown.
    private static bool IsVerificationFailure(Exception exception) => exception is not (
        OutOfMemoryException or
        StackOverflowException or
        OperationCanceledException);
}
