namespace Enigma.Licensing;

/// <summary>
/// The verdict on one license: whether it is valid, and if not, why.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type exists so that validation never throws.</b> A license arrives from outside the
/// application — from a customer, a support inbox, a file someone edited — so a bad one is an expected
/// outcome rather than an exceptional one, and exceptions would push every caller into a
/// <c>try</c>/<c>catch</c> that they would eventually get wrong. Every failure the validator can reach
/// a conclusion about becomes one of these.
/// </para>
/// <para>
/// <see cref="License"/> is populated whenever the payload was readable at all, <b>including on
/// failure</b>. That is what lets an application say "your licence for Contoso Ltd expired on 3 March"
/// rather than "invalid licence". It is also why the property must be treated with care: on a failed
/// result the license has, by definition, not been vouched for — it is safe to display and unsafe to
/// act on.
/// </para>
/// </remarks>
/// <param name="IsValid">
/// Whether the license passed every check. When <see langword="true"/>, <see cref="Failure"/> is
/// <see langword="null"/> and <see cref="License"/> is set.
/// </param>
/// <param name="Failure">
/// The reason validation failed, or <see langword="null"/> when it succeeded.
/// </param>
/// <param name="Message">
/// A human-readable explanation suitable for a log or a diagnostic message, or <see langword="null"/>
/// on success. Its wording is not part of the API — branch on <see cref="Failure"/>, not on this.
/// </param>
/// <param name="License">
/// The license the payload contained, or <see langword="null"/> when the payload could not be read at
/// all — which is the case for every <see cref="LicenseFailure.InvalidFormat"/>,
/// <see cref="LicenseFailure.UnknownAlgorithm"/> and <see cref="LicenseFailure.InvalidSignature"/>
/// result, since none of those get as far as decoding it. Present but <b>unverified</b> on failure.
/// </param>
public sealed record LicenseValidationResult(
    bool IsValid,
    LicenseFailure? Failure,
    string? Message,
    License? License)
{
    /// <summary>Builds the verdict for a license that passed every check.</summary>
    /// <param name="license">The verified license.</param>
    /// <returns>A successful result.</returns>
    internal static LicenseValidationResult Valid(License license) =>
        new(IsValid: true, Failure: null, Message: null, License: license);

    /// <summary>Builds a failure verdict.</summary>
    /// <param name="failure">Why validation failed.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <param name="license">The license, when the payload was readable; otherwise <see langword="null"/>.</param>
    /// <returns>A failed result.</returns>
    internal static LicenseValidationResult Invalid(
        LicenseFailure failure,
        string message,
        License? license = null) =>
        new(IsValid: false, Failure: failure, Message: message, License: license);
}
