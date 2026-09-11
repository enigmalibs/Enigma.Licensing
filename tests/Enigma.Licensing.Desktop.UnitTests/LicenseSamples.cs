using System;

namespace Enigma.Licensing.Desktop.UnitTests;

/// <summary>
/// Licences and verdicts built by hand, for the tests that are about how the application <i>displays</i> a
/// result rather than about how the library reaches one.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here signs or verifies anything: <see cref="LicenseValidationResult"/> has a public constructor and
/// <see cref="License"/> is a public record, so a verdict the validator would need real cryptography to produce
/// costs one object here. The suites that need the real thing — the round-trip ones — build it the real way.
/// </para>
/// <para>
/// <see cref="License.CreatedAt"/> is a fixed instant rather than <c>UtcNow</c>, so nothing in these fixtures
/// changes between two runs. The validity window is deliberately left to the caller: a test about an expired
/// licence has to say when it expired.
/// </para>
/// </remarks>
internal static class LicenseSamples
{
    /// <summary>The instant every sample licence claims to have been issued at.</summary>
    internal static readonly DateTimeOffset Issued =
        new(2026, 8, 7, 9, 30, 0, TimeSpan.FromHours(2));

    /// <summary>Builds a licence.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="productId">The product it is for.</param>
    /// <param name="owner">Who it was issued to, or <see langword="null"/> for a licence that does not say.</param>
    /// <param name="deviceId">The machine it is locked to, or <see langword="null"/> for one that runs anywhere.</param>
    /// <param name="validFrom">When it starts, or <see langword="null"/> for immediately.</param>
    /// <param name="validUntil">When it stops, or <see langword="null"/> for never.</param>
    /// <returns>The licence.</returns>
    internal static License Licence(
        string id = "01JYQ7ZK3MEXAMPLEEXAMPLE00",
        string productId = "Acme Suite",
        string? owner = "Contoso Ltd",
        string? deviceId = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null) => new()
        {
            Id = id,
            CreatedAt = Issued,
            ProductId = productId,
            Owner = owner,
            DeviceId = deviceId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
        };

    /// <summary>Builds the verdict on a licence that passed every check.</summary>
    /// <param name="license">The licence, or <see langword="null"/> for a default one.</param>
    /// <returns>A successful verdict.</returns>
    internal static LicenseValidationResult Valid(License? license = null) =>
        new(IsValid: true, Failure: null, Message: null, License: license ?? Licence());

    /// <summary>Builds a failure verdict.</summary>
    /// <param name="failure">Why it failed.</param>
    /// <param name="message">The library's message about it.</param>
    /// <param name="license">The licence, when the payload was readable; otherwise <see langword="null"/>.</param>
    /// <returns>A failed verdict.</returns>
    internal static LicenseValidationResult Invalid(
        LicenseFailure failure,
        string? message = "The library's own words about this failure.",
        License? license = null) =>
        new(IsValid: false, Failure: failure, Message: message, License: license);
}
