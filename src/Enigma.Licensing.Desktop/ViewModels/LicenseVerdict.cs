using System;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// One verdict, in the words the validate page shows it in: a headline, what it means, and the library's own
/// message underneath.
/// </summary>
/// <remarks>
/// <para>
/// The mapping from <see cref="LicenseFailure"/> to those words lives here rather than in the ViewModel
/// because it is a table, and a table is worth testing on its own: there are eight outcomes, one per failure
/// plus success, and the page is only correct if every one of them says something true.
/// </para>
/// <para>
/// <b>Three texts, because they answer three different questions.</b> <see cref="Headline"/> is the verdict —
/// what a user reads from across the desk. <see cref="Explanation"/> says what it means and what to do about
/// it, in this application's words. <see cref="Detail"/> is the library's own message, which carries the
/// specifics the mapping cannot know: the instant a licence expired, the product it is actually for, the
/// fingerprint of the key the signature was checked with. Repeating what the library already says well would
/// be worse than quoting it.
/// </para>
/// <para>
/// The three <c>Is…</c> properties exist for the view: a <c>Classes.success</c> binding is how the verdict
/// card is painted, and a bound style class needs a <see cref="bool"/> rather than an enum.
/// </para>
/// </remarks>
/// <param name="Headline">The verdict in a few words.</param>
/// <param name="Explanation">What the verdict means, and what to do about it.</param>
/// <param name="Detail">
/// The library's own message about this result, or <see langword="null"/> — which is what a valid licence
/// carries, since there is nothing further to say about one.
/// </param>
/// <param name="Severity">How the verdict is painted.</param>
public sealed record LicenseVerdict(
    string Headline,
    string Explanation,
    string? Detail,
    LicenseVerdictSeverity Severity)
{
    /// <summary>Gets a value indicating whether the licence passed.</summary>
    public bool IsSuccess => Severity == LicenseVerdictSeverity.Success;

    /// <summary>Gets a value indicating whether the licence is authentic but does not apply.</summary>
    public bool IsWarning => Severity == LicenseVerdictSeverity.Warning;

    /// <summary>Gets a value indicating whether the document itself cannot be trusted.</summary>
    public bool IsError => Severity == LicenseVerdictSeverity.Error;

    /// <summary>Gets a value indicating whether there is a library message to show beneath the explanation.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>
    /// Turns a validation result into the words the page shows.
    /// </summary>
    /// <param name="result">The verdict the library reached.</param>
    /// <returns>The verdict to display.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A failure this application does not recognise — a member added to <see cref="LicenseFailure"/> by a
    /// later version of the library — falls through to the same shape as any other rejection rather than
    /// throwing. The library's message is shown regardless, so an unmapped reason still tells the user
    /// something true; a page that crashed on an unknown enum member would not.
    /// </remarks>
    public static LicenseVerdict For(LicenseValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsValid)
        {
            return new LicenseVerdict(
                "Valid",
                "The signature holds, the licence is inside its validity window, and it applies to what you "
                    + "asked about.",
                result.Message,
                LicenseVerdictSeverity.Success);
        }

        (string headline, string explanation, LicenseVerdictSeverity severity) = result.Failure switch
        {
            LicenseFailure.InvalidFormat => (
                "Not a readable licence",
                "The file is not an enigma-license/1 document, or the licence inside it could not be read. "
                    + "Check that you picked the licence itself rather than a key file or a profile.",
                LicenseVerdictSeverity.Error),

            LicenseFailure.UnknownAlgorithm => (
                "Unknown signature scheme",
                "The licence names a signature scheme this version cannot check, so there is nothing here "
                    + "that can say whether it is authentic.",
                LicenseVerdictSeverity.Error),

            LicenseFailure.InvalidSignature => (
                "The signature does not verify",
                "Either the licence was modified after it was signed, or it was signed by a different pair "
                    + "than the public key you chose. Compare the fingerprints before suspecting the licence.",
                LicenseVerdictSeverity.Error),

            LicenseFailure.NotYetValid => (
                "Not yet valid",
                "The signature holds and the licence is genuine — it simply does not start until later.",
                LicenseVerdictSeverity.Warning),

            LicenseFailure.Expired => (
                "Expired",
                "The signature holds and the licence is genuine, but its validity window has passed. It "
                    + "needs reissuing.",
                LicenseVerdictSeverity.Warning),

            LicenseFailure.ProductMismatch => (
                "Wrong product",
                "The signature holds, but the licence is for a different product than the identifier you "
                    + "entered. A licence may use * to cover a range of versions; the identifier you enter is "
                    + "always literal.",
                LicenseVerdictSeverity.Warning),

            LicenseFailure.DeviceMismatch => (
                "Wrong device",
                "The signature holds, but the licence is locked to a different machine than the identifier "
                    + "you entered.",
                LicenseVerdictSeverity.Warning),

            _ => (
                "Not valid",
                "The licence did not pass validation, for a reason this page does not have words for.",
                LicenseVerdictSeverity.Error),
        };

        return new LicenseVerdict(headline, explanation, result.Message, severity);
    }
}
