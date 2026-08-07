using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;
using Enigma.Licensing.UnitTests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Licensing.UnitTests.DependencyInjection;

/// <summary>
/// Registration tests for <c>AddEnigmaLicensing()</c>, driven through a real
/// <see cref="ServiceCollection"/> and <see cref="ServiceProvider"/>: resolution, lifetime,
/// <c>TryAdd</c> semantics, and a signing round-trip through the instances a consumer would actually
/// get.
/// </summary>
/// <remarks>
/// The round-trip at the end is what makes this more than a registration checklist. The store's
/// registration is the only one that composes two services, so it is the only one that can be wired
/// wrongly and still resolve — a license generated, added and queried through resolved instances is
/// what proves it was not.
/// </remarks>
[Collection(SignedLicenseCollection.Name)]
public sealed class EnigmaLicensingServiceCollectionExtensionsTests(SignedLicenseFixture keys)
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddEnigmaLicensing().BuildServiceProvider();

    [Theory]
    [InlineData(typeof(IKeyService))]
    [InlineData(typeof(ILicenseGenerator))]
    [InlineData(typeof(ILicenseValidator))]
    [InlineData(typeof(ILicenseStore))]
    public void AddEnigmaLicensing_ResolvesEveryService(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Theory]
    [InlineData(typeof(IKeyService), typeof(KeyService))]
    [InlineData(typeof(ILicenseGenerator), typeof(LicenseGenerator))]
    [InlineData(typeof(ILicenseValidator), typeof(LicenseValidator))]
    [InlineData(typeof(ILicenseStore), typeof(LicenseStore))]
    public void AddEnigmaLicensing_ResolvesTheLibrarysOwnImplementations(Type serviceType, Type expected)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType(expected, provider.GetRequiredService(serviceType));
    }

    [Theory]
    [InlineData(typeof(IKeyService))]
    [InlineData(typeof(ILicenseGenerator))]
    [InlineData(typeof(ILicenseValidator))]
    [InlineData(typeof(ILicenseStore))]
    public void AddEnigmaLicensing_RegistersEveryServiceAsASingleton(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.Same(provider.GetRequiredService(serviceType), provider.GetRequiredService(serviceType));
    }

    /// <summary>
    /// The store is the one service with state, so a singleton is not a convenience here — it is the
    /// difference between a registry and four unrelated empty ones.
    /// </summary>
    [Fact]
    public void TheStoreIsSharedAcrossResolutions()
    {
        using ServiceProvider provider = BuildProvider();

        ILicenseStore first = provider.GetRequiredService<ILicenseStore>();
        ILicenseStore second = provider.GetRequiredService<ILicenseStore>();

        first.Add(
            provider.GetRequiredService<ILicenseGenerator>()
                .Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        Assert.True(second.HasValidLicense(SignedLicenseTestData.ProductId));
    }

    [Fact]
    public void AddEnigmaLicensing_ReturnsTheSameCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddEnigmaLicensing();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddEnigmaLicensing_Throws_WhenServicesIsNull()
    {
        // The extension method's own guard: a null `this` reaches the body rather than throwing at the
        // call site, so the null-forgiveness here is what the test is about.
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddEnigmaLicensing());
    }

    /// <summary>
    /// What <c>TryAdd</c> buys: calling the method twice leaves one registration per service, so a
    /// library that calls it and an application that also calls it do not end up with two stores.
    /// </summary>
    [Fact]
    public void AddEnigmaLicensing_IsIdempotent()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddEnigmaLicensing()
            .AddEnigmaLicensing()
            .BuildServiceProvider();

        Assert.Single(provider.GetServices<IKeyService>());
        Assert.Single(provider.GetServices<ILicenseGenerator>());
        Assert.Single(provider.GetServices<ILicenseValidator>());
        Assert.Single(provider.GetServices<ILicenseStore>());
    }

    /// <summary>
    /// A consumer's own registration wins — the other half of <c>TryAdd</c>.
    /// </summary>
    [Fact]
    public void AddEnigmaLicensing_KeepsAPreRegisteredValidator()
    {
        AlwaysInvalidValidator custom = new();
        ServiceCollection services = new();
        services.AddSingleton<ILicenseValidator>(custom);

        using ServiceProvider provider = services.AddEnigmaLicensing().BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<ILicenseValidator>());
    }

    /// <summary>
    /// A pre-registered validator is not merely kept in the collection — the store resolved afterwards
    /// validates <i>through</i> it. Asserting only the validator's identity would miss a store
    /// registration that quietly newed up its own.
    /// </summary>
    [Fact]
    public void APreRegisteredValidatorIsWhatTheStoreValidatesWith()
    {
        AlwaysInvalidValidator custom = new();
        ServiceCollection services = new();
        services.AddSingleton<ILicenseValidator>(custom);

        using ServiceProvider provider = services.AddEnigmaLicensing().BuildServiceProvider();
        ILicenseStore store = provider.GetRequiredService<ILicenseStore>();

        store.Add(
            provider.GetRequiredService<ILicenseGenerator>()
                .Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey);

        // The stub refuses everything, so a false here can only come from the store having used it.
        Assert.False(store.HasValidLicense(SignedLicenseTestData.ProductId));
        Assert.True(custom.WasAsked);
    }

    /// <summary>
    /// The whole registration end to end: generate, save, load and validate a license using nothing but
    /// instances resolved from the container.
    /// </summary>
    [Fact]
    public async Task TheResolvedServicesRoundTripALicense()
    {
        using ServiceProvider provider = BuildProvider();

        ILicenseGenerator generator = provider.GetRequiredService<ILicenseGenerator>();
        ILicenseValidator validator = provider.GetRequiredService<ILicenseValidator>();
        ILicenseStore store = provider.GetRequiredService<ILicenseStore>();

        LicenseEnvelope issued = generator.Generate(
            SignedLicenseTestData.InWindow() with { Features = ["export"] },
            keys.MLDsa.PrivateKey);

        string document = await SignedLicenseTestData.SaveToStringAsync(issued, Token);

        using MemoryStream input = SignedLicenseTestData.StreamOf(document);
        LicenseEnvelope reloaded = await LicenseEnvelope.LoadAsync(input, Token);

        LicenseValidationResult result = validator.Validate(
            reloaded, keys.MLDsa.PublicKey, SignedLicenseTestData.ProductId);

        Assert.True(result.IsValid);

        store.Add(reloaded, keys.MLDsa.PublicKey);

        License? found = store.FindValidLicense(SignedLicenseTestData.ProductId);

        Assert.NotNull(found);
        Assert.True(found.HasFeature("export"));
    }

    /// <summary>
    /// The key service resolved from the container generates a usable pair — the one service whose
    /// registration nothing else in this class exercises.
    /// </summary>
    [Fact]
    public void TheResolvedKeyServiceGeneratesAUsablePair()
    {
        using ServiceProvider provider = BuildProvider();

        LicensingKeyPair pair = provider.GetRequiredService<IKeyService>()
            .GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);

        LicenseEnvelope envelope = provider.GetRequiredService<ILicenseGenerator>()
            .Generate(SignedLicenseTestData.Request(), pair.PrivateKey);

        Assert.True(provider.GetRequiredService<ILicenseValidator>()
            .Validate(envelope, pair.PublicKey)
            .IsValid);
    }

    // xUnit1051: every call taking a CancellationToken gets the test's own.
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A stand-in validator that refuses everything and records that it was consulted — enough to prove
    /// which instance the store was handed, and nothing more.
    /// </summary>
    private sealed class AlwaysInvalidValidator : ILicenseValidator
    {
        public bool WasAsked { get; private set; }

        public LicenseValidationResult Validate(
            LicenseEnvelope envelope,
            LicensingKey publicKey,
            string? productId = null,
            string? deviceId = null)
        {
            WasAsked = true;

            return new LicenseValidationResult(false, LicenseFailure.InvalidSignature, "Stub.", null);
        }

        public Task<LicenseValidationResult> ValidateAsync(
            Stream licenseInput,
            Stream publicKeyInput,
            string? productId = null,
            string? deviceId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LicenseValidationResult> ValidateAsync(
            string licensePath,
            string publicKeyPath,
            string? productId = null,
            string? deviceId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
