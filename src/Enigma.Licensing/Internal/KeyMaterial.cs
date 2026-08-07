using System;
using System.Security.Cryptography;
using System.Text;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Turns key material into the text of a key file's <c>key</c> field and back, and checks that what
/// came back is plausibly key material at all.
/// </summary>
/// <remarks>
/// <para>
/// The two families store differently — an RSA key is its PEM text, an ML-DSA key is base64 of the raw
/// FIPS 204 bytes — and this is the only place that knows which is which. The choice is driven by the
/// <c>algorithm</c> field the document declares, never by looking at the content: guessing an encoding
/// is how a format acquires two ways to say the same thing.
/// </para>
/// <para>
/// <b>The split between the two failure types is deliberate.</b> Text that is not valid base64 is a
/// <i>document</i> defect — the file is malformed, the same as a missing field — and surfaces as
/// <see cref="ArgumentException"/>. Bytes that decode fine but are not a usable key are a
/// <i>cryptographic</i> defect and surface as <see cref="CryptographicException"/>, which is the same
/// exception a wrong password produces, because from the caller's side they are the same situation:
/// the material did not come out right.
/// </para>
/// </remarks>
internal static class KeyMaterial
{
    /// <summary>
    /// The parameter name every "this document is malformed" <see cref="ArgumentException"/> reports.
    /// </summary>
    /// <remarks>
    /// Such a failure is always discovered while parsing a document that arrived through the stream
    /// parameter of <see cref="Keys.IKeyService.LoadKeyAsync(System.IO.Stream, char[], System.Threading.CancellationToken)"/>,
    /// so the exception names that parameter rather than whichever internal argument happened to carry
    /// the value. The path overload delegates to the stream one and inherits the same name.
    /// </remarks>
    internal const string DocumentParameterName = "input";

    private const string PemBeginMarker = "-----BEGIN ";
    private const string PemEndMarker = "-----END ";
    private const string EncryptedPkcs8Marker = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
    private const string EncryptedPemHeader = "Proc-Type: 4,ENCRYPTED";

    /// <summary>
    /// Encodes material for the <c>key</c> field of an <b>unencrypted</b> key file.
    /// </summary>
    /// <param name="algorithm">The key's algorithm, which decides the encoding.</param>
    /// <param name="material">The key material.</param>
    /// <returns>PEM text for the RSA members, base64 for the ML-DSA members.</returns>
    internal static string Encode(LicenseSignatureAlgorithm algorithm, byte[] material) =>
        CoreAlgorithms.IsRsa(algorithm)
            ? Encoding.UTF8.GetString(material, 0, material.Length)
            : Convert.ToBase64String(material);

    /// <summary>
    /// Decodes the <c>key</c> field of an <b>unencrypted</b> key file back to material.
    /// </summary>
    /// <param name="algorithm">The algorithm the document declares, which decides the encoding.</param>
    /// <param name="encoded">The field's text.</param>
    /// <returns>The key material.</returns>
    /// <exception cref="ArgumentException">The field should hold base64 and does not.</exception>
    internal static byte[] Decode(LicenseSignatureAlgorithm algorithm, string encoded) =>
        CoreAlgorithms.IsRsa(algorithm)
            ? Encoding.UTF8.GetBytes(encoded)
            : DecodeBase64(encoded);

    /// <summary>
    /// Decodes a base64 field — the ciphertext of an encrypted key.
    /// </summary>
    /// <param name="encoded">The field's text.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="ArgumentException">The text is not valid base64.</exception>
    internal static byte[] DecodeBase64(string encoded)
    {
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The key file's 'key' field is not valid base64.",
                DocumentParameterName, ex);
        }
    }

    /// <summary>
    /// Checks that recovered material looks like the kind of key the document claims it is.
    /// </summary>
    /// <param name="kind">Which half of the pair the document says this is.</param>
    /// <param name="algorithm">The algorithm the document declares.</param>
    /// <param name="material">The recovered material.</param>
    /// <remarks>
    /// <para>
    /// The check is structural, not a parse: it catches an empty field, RSA material that is not PEM at
    /// all, and — specifically — an RSA private key encrypted at the <i>PEM</i> layer, which
    /// <c>enigma-key/1</c> never produces and cannot open. It does not verify that a well-formed PEM
    /// really contains an RSA key of the declared size, nor that ML-DSA bytes are a valid FIPS 204
    /// encoding; those only become knowable when the key is used, and the generator and validator
    /// report them then.
    /// </para>
    /// <para>
    /// The point is a clear failure at the moment the file is read, rather than a confusing one much
    /// later at the moment it is signed with.
    /// </para>
    /// </remarks>
    /// <exception cref="CryptographicException">The material cannot be a key of this kind.</exception>
    internal static void Validate(KeyKind kind, LicenseSignatureAlgorithm algorithm, byte[] material)
    {
        if (material.Length == 0)
        {
            throw new CryptographicException("The key file contains no key material.");
        }

        if (!CoreAlgorithms.IsRsa(algorithm))
        {
            return;
        }

        string pem = Encoding.UTF8.GetString(material, 0, material.Length);

        if (pem.IndexOf(PemBeginMarker, StringComparison.Ordinal) < 0 ||
            pem.IndexOf(PemEndMarker, StringComparison.Ordinal) < 0)
        {
            throw new CryptographicException(
                $"The {KindText(kind)} key material is not a PEM document. The RSA algorithms store " +
                "their keys as PEM text; material that does not decrypt or decode correctly usually " +
                "looks like this.");
        }

        if (kind == KeyKind.Private &&
            (pem.IndexOf(EncryptedPkcs8Marker, StringComparison.Ordinal) >= 0 ||
             pem.IndexOf(EncryptedPemHeader, StringComparison.Ordinal) >= 0))
        {
            throw new CryptographicException(
                "The private key PEM is itself password-encrypted. An enigma-key/1 file protects a " +
                "private key at the file level, not at the PEM level, so this key cannot be opened.");
        }
    }

    private static string KindText(KeyKind kind) => kind == KeyKind.Private ? "private" : "public";
}
