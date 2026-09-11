namespace Enigma.Licensing.UnitTests.Model;

/// <summary>
/// Covers <see cref="LicenseRequest"/>. There is little behaviour to test — it carries arguments —
/// so what is pinned is the shape the generator will rely on in PHASE03: one required field, and
/// everything the generator defaults left explicitly null.
/// </summary>
public sealed class LicenseRequestTests
{
    [Fact]
    public void OnlyProductId_IsRequired()
    {
        LicenseRequest request = new() { ProductId = "MyApp" };

        Assert.Equal("MyApp", request.ProductId);
        Assert.Null(request.Id);
        Assert.Null(request.CreatedAt);
        Assert.Null(request.Owner);
        Assert.Null(request.DeviceId);
        Assert.Null(request.ValidFrom);
        Assert.Null(request.ValidUntil);
    }

    [Fact]
    public void With_ProducesANewInstanceAndLeavesTheOriginalIntact()
    {
        LicenseRequest original = new()
        {
            ProductId = "MyApp",
            Owner = "Contoso Ltd",
            ValidUntil = LicenseTestData.ValidUntil,
        };

        LicenseRequest altered = original with { ValidUntil = null, DeviceId = "DESKTOP-ABC123" };

        Assert.NotSame(original, altered);
        Assert.Equal(LicenseTestData.ValidUntil, original.ValidUntil);
        Assert.Null(original.DeviceId);
        Assert.Null(altered.ValidUntil);
        Assert.Equal("DESKTOP-ABC123", altered.DeviceId);
        Assert.Equal("Contoso Ltd", altered.Owner);
    }

    [Fact]
    public void Equality_ComparesByValue()
    {
        LicenseRequest first = new() { ProductId = "MyApp", ValidFrom = LicenseTestData.ValidFrom };
        LicenseRequest second = new() { ProductId = "MyApp", ValidFrom = LicenseTestData.ValidFrom };

        Assert.Equal(first, second);
        Assert.NotEqual(first, first with { ValidFrom = LicenseTestData.ValidFrom.AddTicks(1) });
    }
}
