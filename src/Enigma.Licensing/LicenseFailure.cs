namespace Enigma.Licensing;

/// <summary>
/// Why a license did not validate — the single reason a <see cref="LicenseValidationResult"/> carries
/// when <see cref="LicenseValidationResult.IsValid"/> is <see langword="false"/>.
/// </summary>
/// <remarks>
/// <para>
/// The members are listed in the order the validator tests them, and validation stops at the first
/// failure. A license that is both expired and for the wrong product reports <see cref="Expired"/>,
/// because the checks run cheapest-and-most-fundamental first: there is no point asking what product
/// an unverified document claims to be for.
/// </para>
/// <para>
/// Deliberately coarse. A reason is something an application shows a user or writes to a log, and
/// finer-grained values would tempt callers into branching on details that are not stable — the
/// <see cref="LicenseValidationResult.Message"/> carries the human-readable specifics instead.
/// </para>
/// </remarks>
public enum LicenseFailure
{
    /// <summary>
    /// The document could not be read as an <c>enigma-license/1</c> envelope, or the payload inside it
    /// was not a license. Covers malformed JSON, a format version this library does not know, base64
    /// that does not decode, and a payload missing a required field.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// The envelope names a signature scheme this library does not implement. The document is
    /// well-formed; there is simply nothing here that can check it.
    /// </summary>
    UnknownAlgorithm,

    /// <summary>
    /// The signature does not verify over the payload bytes with the public key supplied. Either the
    /// license was modified after signing, or it was signed by a different key than the one checking
    /// it.
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// The signature is good, but the license's <see cref="License.ValidFrom"/> is still in the future.
    /// </summary>
    NotYetValid,

    /// <summary>
    /// The signature is good, but the license's <see cref="License.ValidUntil"/> has passed.
    /// </summary>
    Expired,

    /// <summary>
    /// The license is authentic and in date, but it is for a different product than the one asked
    /// about. Wildcards in the license's <see cref="License.ProductId"/> have already been applied.
    /// </summary>
    ProductMismatch,

    /// <summary>
    /// The license is authentic and in date, but it is locked to a different device than the one asked
    /// about.
    /// </summary>
    DeviceMismatch,
}
