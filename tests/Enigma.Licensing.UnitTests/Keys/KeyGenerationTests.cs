using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Covers <see cref="IKeyService.GenerateKeyPair"/> — that every algorithm produces a usable pair, and
/// that the arguments deciding what gets generated are checked.
/// </summary>
/// <remarks>
/// These tests generate their own keys rather than sharing the fixture: generation is what they are
/// about. That makes this the slowest suite in the repo, and RSA at 4096 bits the slowest test in it —
/// a prime search, not a defect.
/// </remarks>
public sealed class KeyGenerationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly KeyService _service = new();

    /// <summary>
    /// Every algorithm, end to end: generate, write both halves, read them back, and confirm what came
    /// back describes the same pair. RSA runs at the smallest accepted modulus to keep it quick — the
    /// size is exercised separately.
    /// </summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha384)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha512)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa87)]
    public async Task GenerateKeyPair_ProducesAPairThatRoundTrips(LicenseSignatureAlgorithm algorithm)
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(algorithm, rsaKeySizeBits: 2048);

        LicensingKey publicKey = await KeyTestData.RoundTripAsync(_service, pair.PublicKey, password: null, Token);
        LicensingKey privateKey = await KeyTestData.RoundTripAsync(_service, pair.PrivateKey, password: null, Token);

        Assert.Equal(KeyKind.Public, publicKey.Kind);
        Assert.Equal(KeyKind.Private, privateKey.Kind);
        Assert.Equal(algorithm, publicKey.Algorithm);
        Assert.Equal(algorithm, privateKey.Algorithm);
        Assert.Equal(pair.PublicKey.KeyId, publicKey.KeyId);
        Assert.Equal(pair.PrivateKey.KeyId, privateKey.KeyId);
    }

    /// <summary>
    /// The fingerprint is computed from the public half, so both files of a pair announce the same one.
    /// This is what lets an application match a license envelope's <c>keyId</c> against a public key it
    /// ships.
    /// </summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44)]
    public void GenerateKeyPair_GivesBothHalvesTheSameKeyId(LicenseSignatureAlgorithm algorithm)
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(algorithm, rsaKeySizeBits: 2048);

        Assert.Equal(pair.PublicKey.KeyId, pair.PrivateKey.KeyId);
        Assert.Equal(16, pair.PublicKey.KeyId.Length);
    }

    /// <summary>Two pairs are two keys — the fingerprint follows the material, not the algorithm.</summary>
    [Fact]
    public void GenerateKeyPair_GivesDifferentPairsDifferentKeyIds()
    {
        LicensingKeyPair first = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);
        LicensingKeyPair second = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);

        Assert.NotEqual(first.PublicKey.KeyId, second.PublicKey.KeyId);
    }

    /// <summary>The two halves must actually differ — a pair whose private key is its public key signs nothing.</summary>
    [Fact]
    public void GenerateKeyPair_ProducesTwoDistinctHalves()
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);

        Assert.NotEqual(pair.PublicKey.Material, pair.PrivateKey.Material);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4096)]
    public async Task GenerateKeyPair_AcceptsTheSupportedRsaKeySizes(int keySizeBits)
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, keySizeBits);

        string document = await KeyTestData.SaveToStringAsync(_service, pair.PublicKey, password: null, Token);

        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", KeyTestData.Field(document, "key"), StringComparison.Ordinal);
    }

    /// <summary>RSA material is PEM text, which is why the file keeps it as text rather than base64.</summary>
    [Fact]
    public void GenerateKeyPair_ProducesPemMaterialForTheRsaAlgorithms()
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha512, rsaKeySizeBits: 2048);

        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", pair.PublicKey.Pem, StringComparison.Ordinal);
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", pair.PrivateKey.Pem, StringComparison.Ordinal);
    }

    /// <summary>
    /// The private PEM is unencrypted even though the format supports passwords: protection happens at
    /// the file level, uniformly for both algorithm families, never at the PEM level.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_LeavesTheRsaPrivatePemUnencrypted()
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, rsaKeySizeBits: 2048);

        Assert.DoesNotContain("ENCRYPTED", pair.PrivateKey.Pem, StringComparison.Ordinal);
    }

    /// <summary>ML-DSA material is raw FIPS 204 bytes — no PEM, and the parameter set fixes the sizes.</summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa87)]
    public void GenerateKeyPair_ProducesRawMaterialForTheMLDsaAlgorithms(LicenseSignatureAlgorithm algorithm)
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(algorithm);

        Assert.DoesNotContain("-----BEGIN", Encoding.UTF8.GetString(pair.PublicKey.Material), StringComparison.Ordinal);
        Assert.True(pair.PublicKey.Material.Length > 0);
        Assert.True(pair.PrivateKey.Material.Length > pair.PublicKey.Material.Length);
    }

    /// <summary>A larger parameter set means larger keys — proof the set is actually reaching the generator.</summary>
    [Fact]
    public void GenerateKeyPair_HonoursTheMLDsaParameterSet()
    {
        int small = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44).PublicKey.Material.Length;
        int medium = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65).PublicKey.Material.Length;
        int large = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa87).PublicKey.Material.Length;

        Assert.True(small < medium, $"ML-DSA-44 ({small}) should be smaller than ML-DSA-65 ({medium}).");
        Assert.True(medium < large, $"ML-DSA-65 ({medium}) should be smaller than ML-DSA-87 ({large}).");
    }

    /// <summary>The key size belongs to RSA; passing one alongside an ML-DSA member changes nothing.</summary>
    [Fact]
    public void GenerateKeyPair_IgnoresTheRsaKeySizeForMLDsa()
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44, rsaKeySizeBits: 1);

        Assert.Equal(LicenseSignatureAlgorithm.MLDsa44, pair.PublicKey.Algorithm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(2047)]
    [InlineData(8193)]
    [InlineData(-1)]
    public void GenerateKeyPair_RejectsAnRsaKeySizeOutsideTheAcceptedRange(int keySizeBits)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, keySizeBits));

        Assert.Equal("rsaKeySizeBits", exception.ParamName);
    }

    [Fact]
    public void GenerateKeyPair_RejectsAnUndefinedAlgorithm()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.GenerateKeyPair((LicenseSignatureAlgorithm)42));

        Assert.Equal("algorithm", exception.ParamName);
    }

    /// <summary>The documented default, asserted where a caller would notice it changing.</summary>
    [Fact]
    public void GenerateKeyPair_DefaultsToA3072BitModulus()
    {
        LicensingKeyPair pair = _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256);
        LicensingKeyPair explicitly = _service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, 3072);

        // Two different keys of the same size produce PEM documents of the same length.
        Assert.Equal(explicitly.PublicKey.Material.Length, pair.PublicKey.Material.Length);
    }
}
