namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// One key/value claim on the generate page's claims list.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and deliberately so. A claim is added through the page's key and value boxes — where the key
/// is checked against the ones already there — and removed with its own button; there is no editing a row in
/// place. That is what makes a duplicate key impossible rather than merely unlikely: two rows sharing a key
/// would collapse into one claim on the way into the licence, and the user would have typed something the
/// licence does not carry.
/// </para>
/// <para>
/// The cost is that changing a claim means removing it and adding it again, which is a fair price for never
/// silently dropping one.
/// </para>
/// </remarks>
/// <param name="Key">The claim's name, unique within the list.</param>
/// <param name="Value">What the claim says. May be empty; a claim that is merely present is a legitimate one.</param>
public sealed record LicenseClaimEntry(string Key, string Value);
