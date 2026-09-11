using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// Covers the registry: what it accepts, what it answers, and that it keeps answering correctly under
/// concurrent use.
/// </summary>
[Collection(SignedLicenseCollection.Name)]
public sealed class LicenseStoreTests(SignedLicenseFixture keys)
{
    private readonly LicenseGenerator _generator = new();

    [Fact]
    public void HasValidLicense_FindsAnAddedLicense()
    {
        LicenseStore store = new();

        store.Add(
            _generator.Generate(SignedLicenseTestData.InWindow(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        Assert.True(store.HasValidLicense(SignedLicenseTestData.ProductId));
    }

    [Fact]
    public void HasValidLicense_IsFalseForAnEmptyStore() =>
        Assert.False(new LicenseStore().HasValidLicense(SignedLicenseTestData.ProductId));

    [Fact]
    public void FindValidLicense_ReturnsTheLicenseItself()
    {
        LicenseStore store = new();
        LicenseRequest request = SignedLicenseTestData.Request() with
        {
            Owner = "Contoso Ltd",
        };

        store.Add(_generator.Generate(request, keys.MLDsa.PrivateKey), keys.MLDsa.PublicKey);

        License? found = store.FindValidLicense(SignedLicenseTestData.ProductId);

        Assert.NotNull(found);
        Assert.Equal("Contoso Ltd", found.Owner);
        Assert.Equal(SignedLicenseTestData.ProductId, found.ProductId);
    }

    [Fact]
    public void FindValidLicense_IsNullForAnotherProduct()
    {
        LicenseStore store = new();

        store.Add(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        Assert.Null(store.FindValidLicense("SomethingElse"));
    }

    /// <summary>Wildcards are the license's, so the store answers about a version it never heard of.</summary>
    [Fact]
    public void FindValidLicense_AppliesTheLicenseWildcard()
    {
        LicenseStore store = new();
        LicenseRequest request = SignedLicenseTestData.Request() with { ProductId = "MyApp 2.*" };

        store.Add(_generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);

        Assert.True(store.HasValidLicense("MyApp 2.17"));
        Assert.False(store.HasValidLicense("MyApp 3.0"));
    }

    /// <summary>
    /// The reason nothing is cached: an expired license must answer no, however it got into the store.
    /// </summary>
    [Fact]
    public void HasValidLicense_IsFalseForAnExpiredLicense()
    {
        LicenseStore store = new();

        store.Add(
            _generator.Generate(SignedLicenseTestData.Expired(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId));
    }

    /// <summary>Adding asserts nothing — a license signed by the wrong key is accepted, then never matches.</summary>
    [Fact]
    public void Add_AcceptsALicenseThatWillNeverValidate()
    {
        LicenseStore store = new();

        store.Add(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.MLDsa.PublicKey);

        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId));
    }

    [Fact]
    public void FindValidLicense_HonoursTheDeviceLock()
    {
        LicenseStore store = new();
        LicenseRequest request = SignedLicenseTestData.Request() with { DeviceId = "DESKTOP-ABC123" };

        store.Add(_generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);

        Assert.True(store.HasValidLicense(SignedLicenseTestData.ProductId, "DESKTOP-ABC123"));
        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId, "DESKTOP-XYZ789"));
    }

    /// <summary>Reissuing a license under its original identifier replaces it rather than duplicating it.</summary>
    [Fact]
    public void Add_ReplacesALicenseWithTheSameIdentifier()
    {
        LicenseStore store = new();
        LicenseRequest expired = SignedLicenseTestData.Expired() with { Id = "SAME-LICENSE" };
        LicenseRequest renewed = SignedLicenseTestData.InWindow() with { Id = "SAME-LICENSE" };

        store.Add(_generator.Generate(expired, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);
        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId));

        store.Add(_generator.Generate(renewed, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);
        Assert.True(store.HasValidLicense(SignedLicenseTestData.ProductId));

        Assert.True(store.Remove("SAME-LICENSE"));
        Assert.False(store.Remove("SAME-LICENSE"));
    }

    /// <summary>"First" is insertion order, so a store with overlapping licenses answers the same way twice.</summary>
    [Fact]
    public void FindValidLicense_ReturnsTheFirstMatchInInsertionOrder()
    {
        LicenseStore store = new();

        foreach (string owner in new[] { "First", "Second", "Third" })
        {
            LicenseRequest request = SignedLicenseTestData.Request() with { Id = owner, Owner = owner };

            store.Add(_generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);
        }

        Assert.Equal("First", store.FindValidLicense(SignedLicenseTestData.ProductId)?.Owner);

        Assert.True(store.Remove("First"));
        Assert.Equal("Second", store.FindValidLicense(SignedLicenseTestData.ProductId)?.Owner);
    }

    /// <summary>An invalid license does not shadow a valid one added after it.</summary>
    [Fact]
    public void FindValidLicense_SkipsPastInvalidEntries()
    {
        LicenseStore store = new();

        store.Add(
            _generator.Generate(SignedLicenseTestData.Expired() with { Id = "OLD" }, keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);
        store.Add(
            _generator.Generate(
                SignedLicenseTestData.InWindow() with { Id = "NEW", Owner = "Contoso Ltd" },
                keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        Assert.Equal("Contoso Ltd", store.FindValidLicense(SignedLicenseTestData.ProductId)?.Owner);
    }

    [Fact]
    public void Remove_IsFalseForAnUnknownIdentifier() =>
        Assert.False(new LicenseStore().Remove("NOT-IN-THE-STORE"));

    [Fact]
    public void Add_ThrowsForANullArgument()
    {
        LicenseStore store = new();
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        Assert.Equal(
            "envelope",
            Assert.Throws<ArgumentNullException>(() => store.Add(null!, keys.Rsa.PublicKey)).ParamName);
        Assert.Equal(
            "publicKey",
            Assert.Throws<ArgumentNullException>(() => store.Add(signed, null!)).ParamName);
    }

    [Fact]
    public void Add_ThrowsForAPrivateKey()
    {
        LicenseStore store = new();
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => store.Add(signed, keys.Rsa.PrivateKey));

        Assert.Equal("publicKey", exception.ParamName);
    }

    /// <summary>Without a readable payload there is no identifier to file the license under.</summary>
    [Fact]
    public void Add_ThrowsForAPayloadWithNoLicenseInIt()
    {
        LicenseStore store = new();
        LicenseEnvelope envelope = new()
        {
            Format = LicenseEnvelope.CurrentFormat,
            Algorithm = "RSA-SHA256",
            Payload = Encoding.UTF8.GetBytes("{\"somethingElse\":true}"),
            Signature = [0x00],
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => store.Add(envelope, keys.Rsa.PublicKey));

        Assert.Equal("envelope", exception.ParamName);
    }

    [Fact]
    public void Remove_ThrowsForANullIdentifier() =>
        Assert.Equal(
            "licenseId",
            Assert.Throws<ArgumentNullException>(() => new LicenseStore().Remove(null!)).ParamName);

    [Fact]
    public void FindValidLicense_ThrowsForANullProductId() =>
        Assert.Equal(
            "productId",
            Assert.Throws<ArgumentNullException>(
                () => new LicenseStore().FindValidLicense(null!)).ParamName);

    [Fact]
    public void Constructor_ThrowsForANullValidator() =>
        Assert.Equal(
            "validator",
            Assert.Throws<ArgumentNullException>(() => new LicenseStore(null!)).ParamName);

    /// <summary>
    /// Reads and writes from many threads at once. ML-DSA verification is fast enough to make this a
    /// real race rather than a formality, and the assertion is simply that nothing corrupts and nothing
    /// throws.
    /// </summary>
    [Fact]
    public async Task Store_SurvivesConcurrentReadsAndWrites()
    {
        LicenseStore store = new();
        LicenseEnvelope resident = _generator.Generate(
            SignedLicenseTestData.Request() with { Id = "RESIDENT" }, keys.MLDsa.PrivateKey);

        store.Add(resident, keys.MLDsa.PublicKey);

        List<LicenseEnvelope> churn = [.. Enumerable.Range(0, 16).Select(index =>
            _generator.Generate(
                SignedLicenseTestData.Request() with { Id = $"CHURN-{index}" }, keys.MLDsa.PrivateKey))];

        Task[] writers = [.. churn.Select(envelope => Task.Run(() =>
        {
            for (int round = 0; round < 20; round++)
            {
                store.Add(envelope, keys.MLDsa.PublicKey);
                store.Remove(LicensePayload.Deserialize(envelope.Payload).Id);
            }
        }))];

        Task[] readers = [.. Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int round = 0; round < 20; round++)
            {
                Assert.True(store.HasValidLicense(SignedLicenseTestData.ProductId));
            }
        }))];

        await Task.WhenAll([.. writers, .. readers]);

        Assert.NotNull(store.FindValidLicense(SignedLicenseTestData.ProductId));
        Assert.True(store.Remove("RESIDENT"));
        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId));
    }
}
