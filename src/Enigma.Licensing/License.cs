using System;

namespace Enigma.Licensing;

/// <summary>
/// The license data itself — everything a signature protects and everything validation decides on.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="License"/> never travels on its own. It is serialized to UTF-8 JSON, and those exact
/// bytes become the <see cref="LicenseEnvelope.Payload"/> that gets signed. Reading a license back
/// therefore always means going through an envelope: an instance you hold is either one you built
/// yourself or one a validator recovered from a payload, and only the latter says anything about
/// authenticity.
/// </para>
/// <para>
/// The type is an immutable record: every member is <c>init</c>-only, and a variation is produced
/// with a <c>with</c> expression rather than by assignment. That matters because a license is a
/// signed artefact — a mutable one invites code that edits a validated license after the fact and
/// leaves the signature describing something else.
/// </para>
/// <para>
/// Dates are <see cref="DateTimeOffset"/> and serialize as ISO-8601 with an explicit offset, so a
/// license issued in one time zone means the same instant everywhere. Validity is judged against
/// <see cref="DateTimeOffset.UtcNow"/> at the moment of validation; there is deliberately no clock
/// abstraction to substitute.
/// </para>
/// </remarks>
public sealed record License
{
    /// <summary>
    /// The license's unique identifier. Issued as a ULID by the generator — lexicographically
    /// sortable by creation time — but any non-empty string a caller supplies is accepted and stored
    /// verbatim.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// When the license was issued. Informational: it is <b>not</b> part of the validity window and
    /// never causes validation to fail, so a license whose <see cref="CreatedAt"/> lies in the future
    /// is still valid unless <see cref="ValidFrom"/> says otherwise.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The product this license is for. Matched against the product identifier the application asks
    /// about at validation time.
    /// </summary>
    /// <remarks>
    /// A <c>*</c> anywhere in this value turns the comparison into an anchored wildcard match, which
    /// is how one license covers a range of versions — <c>"MyApp 2.*"</c> matches <c>"MyApp 2.0"</c>
    /// and <c>"MyApp 2.17"</c>. Only the wildcard is special; every other character, including regular
    /// expression metacharacters, is matched literally. The wildcard is only ever honoured on the
    /// <b>license's</b> side: the identifier the application passes in is always a literal.
    /// </remarks>
    public required string ProductId { get; init; }

    /// <summary>
    /// The device this license is locked to, or <see langword="null"/> for a license that runs
    /// anywhere. Compared ordinally against the identifier the application supplies, so any
    /// device-identity scheme works — see <c>DeviceIdentifier</c> for the built-in one.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// The instant the license becomes usable, or <see langword="null"/> to be usable immediately.
    /// A license validated before this instant fails as not yet valid.
    /// </summary>
    public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>
    /// The instant the license stops being usable, or <see langword="null"/> for a perpetual license.
    /// A license validated at or after this instant fails as expired.
    /// </summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>
    /// Who the license was issued to. Purely descriptive — validation never looks at it.
    /// </summary>
    public string? Owner { get; init; }
}
