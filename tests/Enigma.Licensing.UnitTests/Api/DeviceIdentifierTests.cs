using System;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// Covers the one guarantee <see cref="DeviceIdentifier"/> actually makes: two calls on the same
/// machine agree.
/// </summary>
/// <remarks>
/// The value itself is deliberately not asserted. It is a hash of components whose encoding belongs to
/// the underlying package, and pinning it here would turn a dependency bump into a failing test without
/// anything having gone wrong.
/// </remarks>
public sealed class DeviceIdentifierTests
{
    [Fact]
    public void Generate_ReturnsANonEmptyIdentifier() =>
        Assert.False(string.IsNullOrWhiteSpace(DeviceIdentifier.Generate()));

    [Fact]
    public void Generate_IsStableAcrossCalls() =>
        Assert.Equal(DeviceIdentifier.Generate(), DeviceIdentifier.Generate());

    /// <summary>
    /// It is a token to store and compare, not a label: the machine name must not be readable in it.
    /// </summary>
    [Fact]
    public void Generate_DoesNotDiscloseTheMachineName() =>
        Assert.DoesNotContain(
            Environment.MachineName, DeviceIdentifier.Generate(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The library never interprets a device identifier, so the built-in one is just a string that
    /// round-trips through a license like any other.
    /// </summary>
    [Fact]
    public void Generate_ProducesAValueTheModelCarriesVerbatim()
    {
        string deviceId = DeviceIdentifier.Generate();

        License license = new()
        {
            Id = "01JYQ7ZK3M8X2N4P6R8T0V2W4Y",
            CreatedAt = LicenseTestData.CreatedAt,
            ProductId = "MyApp",
            DeviceId = deviceId,
        };

        Assert.Equal(deviceId, license.DeviceId);
    }
}
