using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Core.Asymmetric.PublicKey;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Covers <see cref="LicensingKey.RsaHandle"/> — the parsed Enigma.Core handle a key holds on behalf
/// of every RSA operation performed with it.
/// </summary>
/// <remarks>
/// <para>
/// Enigma.Core 2.0 takes an <see cref="RsaKey"/> rather than a PEM string precisely so that the parse
/// happens once: constructing a private key validates its CRT components, which is the expensive part
/// of signing. The cache is therefore not an optimisation detail but the reason the dependency's API
/// looks the way it does — so it is asserted here, because a refactor that imported a handle per call
/// would still compile, still pass every other test, and still be wrong.
/// </para>
/// <para>
/// These tests share the generated-key fixture: they are about what a key does with its material, not
/// about generating one.
/// </para>
/// </remarks>
[Collection(GeneratedKeyCollection.Name)]
public sealed class KeyHandleTests(GeneratedKeyFixture keys)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Read twice, parsed once — the whole point of holding a handle.</summary>
    [Theory]
    [InlineData(KeyKind.Public)]
    [InlineData(KeyKind.Private)]
    public void RsaHandle_IsImportedOnceAndReused(KeyKind kind)
    {
        LicensingKey key = kind == KeyKind.Public ? keys.Rsa.PublicKey : keys.Rsa.PrivateKey;

        Assert.Same(key.RsaHandle, key.RsaHandle);
    }

    /// <summary>
    /// The two halves import their own material, so a public key is genuinely public: it cannot sign,
    /// whatever the pair it came from.
    /// </summary>
    [Fact]
    public void RsaHandle_CarriesOnlyTheHalfTheKeyIs()
    {
        Assert.False(keys.Rsa.PublicKey.RsaHandle.HasPrivateKey);
        Assert.True(keys.Rsa.PrivateKey.RsaHandle.HasPrivateKey);
        Assert.NotSame(keys.Rsa.PublicKey.RsaHandle, keys.Rsa.PrivateKey.RsaHandle);
    }

    /// <summary>The handle describes the key that was generated, not a default.</summary>
    [Fact]
    public void RsaHandle_ReportsTheGeneratedModulusSize()
    {
        Assert.Equal(2048, keys.Rsa.PublicKey.RsaHandle.KeySizeBits);
        Assert.Equal(2048, keys.Rsa.PrivateKey.RsaHandle.KeySizeBits);
    }

    /// <summary>
    /// Several threads reaching for the handle at once still end up on one instance. This is not
    /// hypothetical: <see cref="ILicenseStore"/> snapshots its registry under a lock and then verifies
    /// <i>outside</i> it, so one public key is used concurrently by design.
    /// </summary>
    [Fact]
    public async Task RsaHandle_IsTheSameInstanceForEveryThreadThatRacesForIt()
    {
        // A key of its own, so the first read really is a first read: the fixture's pair may already
        // have been imported by another test in this collection.
        LicensingKey key = new KeyService()
            .GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, rsaKeySizeBits: 2048)
            .PrivateKey;

        IEnumerable<Task<RsaKey>> readers = Enumerable
            .Range(0, 8)
            .Select(_ => Task.Run(() => key.RsaHandle, Token));

        RsaKey[] handles = await Task.WhenAll(readers);

        Assert.All(handles, handle => Assert.Same(handles[0], handle));
    }
}
