using System;

namespace Enigma.Licensing.UnitTests.Model;

/// <summary>
/// Covers <see cref="License"/> — the immutability the signed model depends on, and the way its dates
/// carry an offset.
/// </summary>
public sealed class LicenseTests
{
    /// <summary>
    /// <c>with</c> produces a new instance and leaves the source untouched — the property that makes
    /// it safe to hold a validated license and hand it around.
    /// </summary>
    [Fact]
    public void With_ProducesANewInstanceAndLeavesTheOriginalIntact()
    {
        License original = LicenseTestData.FullLicense();

        License altered = original with { ProductId = "OtherApp", Owner = null };

        Assert.NotSame(original, altered);
        Assert.Equal("MyApp 2.*", original.ProductId);
        Assert.Equal("Contoso Ltd", original.Owner);
        Assert.Equal("OtherApp", altered.ProductId);
        Assert.Null(altered.Owner);
        Assert.Equal(original.Id, altered.Id);
    }

    [Fact]
    public void Equality_ComparesByValueAcrossTheScalarMembers()
    {
        License first = LicenseTestData.MinimalLicense() with { Owner = "Contoso Ltd" };
        License second = LicenseTestData.MinimalLicense() with { Owner = "Contoso Ltd" };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, first with { ValidUntil = LicenseTestData.ValidUntil });
    }

    /// <summary>
    /// Dates are <see cref="DateTimeOffset"/>, so a license issued at the same instant in two zones is
    /// the same instant — the offset is carried, not discarded.
    /// </summary>
    [Fact]
    public void Dates_KeepTheirOffset()
    {
        License license = LicenseTestData.FullLicense();

        Assert.Equal(TimeSpan.FromHours(2), license.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, license.ValidFrom!.Value.Offset);
        Assert.Equal(license.CreatedAt.UtcDateTime, new DateTimeOffset(2026, 8, 6, 10, 34, 56, TimeSpan.Zero).UtcDateTime);
    }
}
