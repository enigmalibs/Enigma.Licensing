using System;
using System.Text;

namespace Enigma.Licensing.UnitTests;

/// <summary>
/// Fixed inputs shared by the model, codec and envelope suites.
/// </summary>
/// <remarks>
/// Everything here is deterministic — no <c>UtcNow</c>, no random bytes — because several tests pin
/// serialized output byte-for-byte. A moving value would turn those into a different assertion every
/// run.
/// </remarks>
internal static class LicenseTestData
{
    /// <summary>A fixed issue timestamp with a non-zero offset, so the offset itself is exercised.</summary>
    internal static readonly DateTimeOffset CreatedAt = new(2026, 8, 6, 12, 34, 56, TimeSpan.FromHours(2));

    /// <summary>A fixed window start, in UTC.</summary>
    internal static readonly DateTimeOffset ValidFrom = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>A fixed window end, in UTC.</summary>
    internal static readonly DateTimeOffset ValidUntil = new(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>A license with every optional field populated.</summary>
    internal static License FullLicense() => new()
    {
        Id = "01JYQ7ZK3M8X2N4P6R8T0V2W4Y",
        CreatedAt = CreatedAt,
        ProductId = "MyApp 2.*",
        DeviceId = "DESKTOP-ABC123",
        ValidFrom = ValidFrom,
        ValidUntil = ValidUntil,
        Owner = "Contoso Ltd",
    };

    /// <summary>A license with only the three required fields set.</summary>
    internal static License MinimalLicense() => new()
    {
        Id = "01JYQ7ZK3M8X2N4P6R8T0V2W4Y",
        CreatedAt = CreatedAt,
        ProductId = "MyApp",
    };

    /// <summary>
    /// A well-formed envelope whose payload is real license JSON and whose signature is a fixed
    /// stand-in — nothing in this phase signs or verifies, so the bytes only have to survive intact.
    /// </summary>
    internal static LicenseEnvelope FullEnvelope() => new()
    {
        Format = LicenseEnvelope.CurrentFormat,
        Algorithm = "RSA-SHA256",
        KeyId = "3F9A1C40D2B87E56",
        Payload = Encoding.UTF8.GetBytes("{\"id\":\"01JYQ7ZK3M8X2N4P6R8T0V2W4Y\"}"),
        Signature = [0x00, 0x01, 0xFE, 0xFF, 0x7F, 0x80],
    };
}
