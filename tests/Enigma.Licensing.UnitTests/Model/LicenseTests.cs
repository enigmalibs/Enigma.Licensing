using System;
using System.Collections.Generic;

namespace Enigma.Licensing.UnitTests.Model;

/// <summary>
/// Covers <see cref="License"/> — the immutability the signed model depends on, the never-null
/// guarantee on its two collections, and <see cref="License.HasFeature"/>.
/// </summary>
public sealed class LicenseTests
{
    [Fact]
    public void Collections_DefaultToEmptyRatherThanNull()
    {
        License license = LicenseTestData.MinimalLicense();

        Assert.Empty(license.Features);
        Assert.Empty(license.Claims);
    }

    /// <summary>
    /// A payload carrying <c>"features": null</c> assigns null through the initializer, and code
    /// downstream of an untrusted document must not have to null-check what the type declares as
    /// non-nullable.
    /// </summary>
    [Fact]
    public void Collections_CoerceAnAssignedNullToEmpty()
    {
        License license = LicenseTestData.MinimalLicense() with
        {
            Features = null!,
            Claims = null!,
        };

        Assert.Empty(license.Features);
        Assert.Empty(license.Claims);
        Assert.False(license.HasFeature("export"));
    }

    [Fact]
    public void HasFeature_FindsAGrantedFeature()
    {
        License license = LicenseTestData.FullLicense();

        Assert.True(license.HasFeature("export"));
        Assert.True(license.HasFeature("sync"));
    }

    /// <summary>
    /// Comparison is ordinal and case-sensitive: feature flags are identifiers the application and
    /// the issuer agree on, so <c>"Export"</c> is a different flag, not a sloppy spelling of one.
    /// </summary>
    [Theory]
    [InlineData("Export")]
    [InlineData("EXPORT")]
    [InlineData("export ")]
    [InlineData("expor")]
    [InlineData("")]
    [InlineData("print")]
    public void HasFeature_RejectsAnythingButAnExactMatch(string feature) =>
        Assert.False(LicenseTestData.FullLicense().HasFeature(feature));

    [Fact]
    public void HasFeature_ThrowsForANullFeature()
    {
        License license = LicenseTestData.FullLicense();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => license.HasFeature(null!));

        Assert.Equal("feature", exception.ParamName);
    }

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
        Assert.Same(original.Features, altered.Features);
    }

    [Fact]
    public void Equality_ComparesByValueAcrossTheScalarMembers()
    {
        IReadOnlyCollection<string> features = ["export"];

        License first = LicenseTestData.MinimalLicense() with { Features = features };
        License second = LicenseTestData.MinimalLicense() with { Features = features };

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
