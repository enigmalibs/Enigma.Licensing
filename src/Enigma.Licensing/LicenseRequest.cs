using System;

namespace Enigma.Licensing;

/// <summary>
/// What the issuer wants a license to say — the input a license generator turns into a signed
/// <see cref="LicenseEnvelope"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the mutable-intent counterpart to <see cref="License"/>: the same fields, but only
/// <see cref="ProductId"/> is required, and everything the generator can decide for itself is
/// optional. Leaving <see cref="Id"/> and <see cref="CreatedAt"/> unset is the normal case — the
/// generator stamps a fresh ULID and the current instant. Supplying them is for the cases where the
/// caller owns those values already, such as reissuing a license under its original identifier or
/// reproducing a known license in a test.
/// </para>
/// <para>
/// A request is never signed, never serialized and never leaves the issuing process; it exists only
/// to carry arguments in one shape. It is a record for the same reason <see cref="License"/> is —
/// build one, vary it with <c>with</c>, and it cannot change under a generator that is reading it.
/// </para>
/// </remarks>
public sealed record LicenseRequest
{
    /// <summary>
    /// The product the license is for — the only field with no sensible default. May contain
    /// <c>*</c> wildcards; see <see cref="License.ProductId"/> for how they are matched.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>Who the license is issued to, or <see langword="null"/> to leave it unattributed.</summary>
    public string? Owner { get; init; }

    /// <summary>
    /// The device to lock the license to, or <see langword="null"/> for a license that runs anywhere.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// When the license starts being usable, or <see langword="null"/> for immediately.
    /// </summary>
    public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>
    /// When the license stops being usable, or <see langword="null"/> for a perpetual license.
    /// </summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>
    /// The identifier to give the license. Leave <see langword="null"/> — the normal case — and the
    /// generator issues a fresh ULID.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The issue timestamp to record. Leave <see langword="null"/> — the normal case — and the
    /// generator stamps <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }
}
