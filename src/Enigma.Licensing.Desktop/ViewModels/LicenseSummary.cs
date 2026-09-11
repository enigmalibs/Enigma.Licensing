using System;
using System.Globalization;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// What a licence says, in the strings the validate page's read-only card shows.
/// </summary>
/// <remarks>
/// <para>
/// A projection rather than a binding straight onto <see cref="License"/>, for two reasons the view cannot
/// solve on its own: the dates are instants that have to be formatted, and the optional fields need words
/// for being absent rather than an empty row.
/// </para>
/// <para>
/// <b>The licence this describes may be unverified.</b> The validator fills
/// <see cref="LicenseValidationResult.License"/> in whenever the payload was readable, including on failure,
/// which is what lets this page say <i>what</i> expired. The consequence is the library's: safe to display,
/// unsafe to act on. Displaying is all this type is for.
/// </para>
/// <para>
/// <b>Dates are shown as the instants they are, in local time, with the offset.</b> A licence's
/// <see cref="License.ValidUntil"/> is the first instant it is <i>no longer</i> usable, so one issued
/// "through the 31st" reads here as midnight on the 1st — the truth, and not what a reader guesses. The card
/// carries a line saying so; rounding the instant back into a friendlier day would mean this page and the
/// generate page disagreeing about the same licence.
/// </para>
/// </remarks>
public sealed class LicenseSummary
{
    /// <summary>The format every instant on the card is shown in: sortable, unambiguous, offset included.</summary>
    private const string InstantFormat = "yyyy-MM-dd HH:mm zzz";

    /// <summary>Initializes a new instance of the <see cref="LicenseSummary"/> class.</summary>
    /// <param name="license">The licence to describe. Not stored — every field is read here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is <see langword="null"/>.</exception>
    public LicenseSummary(License license)
    {
        ArgumentNullException.ThrowIfNull(license);

        Id = license.Id;
        ProductId = license.ProductId;
        Owner = Stated(license.Owner, "Not stated");
        DeviceId = Stated(license.DeviceId, "Any machine");
        CreatedAt = Instant(license.CreatedAt);
        ValidFrom = license.ValidFrom is { } from ? Instant(from) : "Immediately";
        ValidUntil = license.ValidUntil is { } until ? Instant(until) : "Never expires";
    }

    /// <summary>Gets the licence's identifier — a ULID when this library issued it.</summary>
    public string Id { get; }

    /// <summary>Gets the product the licence is for, wildcard included if it carries one.</summary>
    public string ProductId { get; }

    /// <summary>Gets who the licence was issued to, or the words for a licence that does not say.</summary>
    public string Owner { get; }

    /// <summary>Gets the machine the licence is locked to, or the words for one that runs anywhere.</summary>
    public string DeviceId { get; }

    /// <summary>Gets when the licence was issued.</summary>
    public string CreatedAt { get; }

    /// <summary>Gets when the licence starts being usable, or the words for one usable straight away.</summary>
    public string ValidFrom { get; }

    /// <summary>Gets the instant the licence stops being usable, or the words for a perpetual one.</summary>
    public string ValidUntil { get; }

    private static string Instant(DateTimeOffset value) =>
        value.ToLocalTime().ToString(InstantFormat, CultureInfo.InvariantCulture);

    private static string Stated(string? value, string absent) =>
        string.IsNullOrWhiteSpace(value) ? absent : value;
}
