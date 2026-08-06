using System;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers <see cref="AlgorithmIds"/> — the one table mapping <see cref="LicenseSignatureAlgorithm"/>
/// to the identifier strings that appear in <c>enigma-license/1</c> and <c>enigma-key/1</c> documents.
/// </summary>
/// <remarks>
/// The identifiers are asserted as literals rather than through the constants they come from. A test
/// that reads <c>AlgorithmIds.RsaSha256</c> to check <c>ToIdentifier</c> proves only that the method
/// returns the constant, which is exactly the mistake that lets a typo ship; spelling the wire value
/// out here means changing one requires changing the other on purpose.
/// </remarks>
public sealed class AlgorithmIdsTests
{
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256, "RSA-SHA256")]
    [InlineData(LicenseSignatureAlgorithm.RsaSha384, "RSA-SHA384")]
    [InlineData(LicenseSignatureAlgorithm.RsaSha512, "RSA-SHA512")]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44, "ML-DSA-44")]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65, "ML-DSA-65")]
    [InlineData(LicenseSignatureAlgorithm.MLDsa87, "ML-DSA-87")]
    public void ToIdentifier_ProducesTheDocumentedWireString(LicenseSignatureAlgorithm algorithm, string expected) =>
        Assert.Equal(expected, AlgorithmIds.ToIdentifier(algorithm));

    [Theory]
    [InlineData("RSA-SHA256", LicenseSignatureAlgorithm.RsaSha256)]
    [InlineData("RSA-SHA384", LicenseSignatureAlgorithm.RsaSha384)]
    [InlineData("RSA-SHA512", LicenseSignatureAlgorithm.RsaSha512)]
    [InlineData("ML-DSA-44", LicenseSignatureAlgorithm.MLDsa44)]
    [InlineData("ML-DSA-65", LicenseSignatureAlgorithm.MLDsa65)]
    [InlineData("ML-DSA-87", LicenseSignatureAlgorithm.MLDsa87)]
    public void TryParse_ResolvesTheDocumentedWireString(string identifier, LicenseSignatureAlgorithm expected)
    {
        Assert.True(AlgorithmIds.TryParse(identifier, out LicenseSignatureAlgorithm algorithm));
        Assert.Equal(expected, algorithm);
    }

    /// <summary>
    /// Every declared member survives a there-and-back trip, so no member can be added to the enum
    /// without also being added to the table.
    /// </summary>
    [Fact]
    public void EveryDeclaredMember_RoundTripsThroughItsIdentifier()
    {
        foreach (LicenseSignatureAlgorithm algorithm in Enum.GetValues<LicenseSignatureAlgorithm>())
        {
            string? identifier = AlgorithmIds.ToIdentifier(algorithm);

            Assert.NotNull(identifier);
            Assert.True(AlgorithmIds.TryParse(identifier, out LicenseSignatureAlgorithm parsed));
            Assert.Equal(algorithm, parsed);
        }
    }

    /// <summary>
    /// Pins the size of the signing surface. The library's whole point is covering all six schemes, so
    /// a seventh appearing — or one quietly disappearing — should be a deliberate act with a plan
    /// behind it, not a silent enum edit.
    /// </summary>
    [Fact]
    public void TheEnum_DeclaresExactlySixAlgorithms() =>
        Assert.Equal(6, Enum.GetValues<LicenseSignatureAlgorithm>().Length);

    [Fact]
    public void ToIdentifier_ReturnsNullForAnUndefinedEnumValue() =>
        Assert.Null(AlgorithmIds.ToIdentifier((LicenseSignatureAlgorithm)99));

    /// <summary>
    /// Identifiers are format constants, so resolution is ordinal: a differently-cased or
    /// differently-punctuated spelling is a document this library does not read, not a near miss to
    /// be forgiven. <see langword="null"/> stands in for the field being absent altogether.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("rsa-sha256")]
    [InlineData("RSA-SHA256 ")]
    [InlineData("RSASHA256")]
    [InlineData("RsaSha256")]
    [InlineData("ML-DSA-66")]
    [InlineData("mldsa65")]
    [InlineData("HMAC-SHA256")]
    public void TryParse_RejectsAnythingElse(string? identifier)
    {
        Assert.False(AlgorithmIds.TryParse(identifier, out LicenseSignatureAlgorithm algorithm));
        Assert.Equal(default(LicenseSignatureAlgorithm), algorithm);
    }
}
