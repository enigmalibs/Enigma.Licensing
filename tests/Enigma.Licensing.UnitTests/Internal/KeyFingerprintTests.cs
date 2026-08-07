using System;
using System.Text;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers <see cref="KeyFingerprint"/> — the construction behind a key pair's <c>keyId</c>.
/// </summary>
/// <remarks>
/// The fingerprint is written into every license envelope the generator produces, so changing how it
/// is computed would silently orphan every key an issuer already has: their stored public key would
/// announce one identifier and their licenses another. These tests pin the construction, not just its
/// shape.
/// </remarks>
public sealed class KeyFingerprintTests
{
    private static readonly byte[] Material = Encoding.UTF8.GetBytes("public-key-material");

    /// <summary>
    /// The known answer: uppercase hex of the first eight bytes of
    /// <c>SHA-256("RSA-SHA256" ‖ "public-key-material")</c>, computed independently of this library.
    /// </summary>
    [Fact]
    public void Compute_MatchesTheDocumentedConstruction() =>
        Assert.Equal("57CDB5BFDD19AD1A", KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha256, Material));

    /// <summary>The algorithm identifier is part of the hashed input, not decoration.</summary>
    [Fact]
    public void Compute_IncludesTheAlgorithmIdentifier() =>
        Assert.Equal("7B47910750F735A9", KeyFingerprint.Compute(LicenseSignatureAlgorithm.MLDsa65, Material));

    [Fact]
    public void Compute_IsDeterministic()
    {
        string first = KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha384, Material);
        string second = KeyFingerprint.Compute(
            LicenseSignatureAlgorithm.RsaSha384, Encoding.UTF8.GetBytes("public-key-material"));

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Two schemes over identical material must not collide — an application picking a key by
    /// identifier would otherwise reach for an RSA key to check an ML-DSA signature.
    /// </summary>
    [Fact]
    public void Compute_DiffersBetweenAlgorithms()
    {
        string rsa256 = KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha256, Material);
        string rsa512 = KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha512, Material);
        string mlDsa = KeyFingerprint.Compute(LicenseSignatureAlgorithm.MLDsa44, Material);

        Assert.NotEqual(rsa256, rsa512);
        Assert.NotEqual(rsa256, mlDsa);
    }

    [Fact]
    public void Compute_DiffersWhenTheMaterialDiffers()
    {
        byte[] altered = (byte[])Material.Clone();
        altered[0] ^= 0x01;

        Assert.NotEqual(
            KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha256, Material),
            KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha256, altered));
    }

    [Fact]
    public void Compute_ReturnsSixteenUppercaseHexDigits()
    {
        string fingerprint = KeyFingerprint.Compute(LicenseSignatureAlgorithm.MLDsa87, Material);

        Assert.Equal(16, fingerprint.Length);
        Assert.All(fingerprint, character => Assert.Contains(character, "0123456789ABCDEF"));
    }

    [Fact]
    public void Compute_AcceptsEmptyMaterialRatherThanGuessing() =>
        Assert.Equal(16, KeyFingerprint.Compute(LicenseSignatureAlgorithm.RsaSha256, []).Length);

    [Fact]
    public void Compute_RejectsAnUndefinedAlgorithm() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => KeyFingerprint.Compute((LicenseSignatureAlgorithm)99, Material));
}
