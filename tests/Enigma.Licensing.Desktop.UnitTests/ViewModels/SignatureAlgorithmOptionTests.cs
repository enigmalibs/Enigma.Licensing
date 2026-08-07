using System;
using System.Linq;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The signing-algorithm picker's entries: one per scheme the library supports, labelled with the identifier
/// the format itself uses, and knowing which of them bring a key size with them.
/// </summary>
/// <remarks>
/// The completeness test is the one that earns its keep: a scheme added to the library and not to this list
/// is a scheme the application cannot generate a key for, and nothing else in the build would say so.
/// </remarks>
public sealed class SignatureAlgorithmOptionTests
{
    [Fact]
    public void All_CoversEverySchemeTheLibrarySupports_Once()
    {
        Assert.Equal(
            Enum.GetValues<LicenseSignatureAlgorithm>().Order(),
            SignatureAlgorithmOption.All.Select(option => option.Algorithm).Order());
    }

    [Fact]
    public void Default_IsTheBalancedPostQuantumSet()
    {
        Assert.Equal(LicenseSignatureAlgorithm.MLDsa65, SignatureAlgorithmOption.Default.Algorithm);
        Assert.False(SignatureAlgorithmOption.Default.IsRsa);
    }

    /// <summary>
    /// The labels are the format's own wire identifiers, so what the picker shows is what the key file will
    /// record and what an error message will quote.
    /// </summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44, "ML-DSA-44", false)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65, "ML-DSA-65", false)]
    [InlineData(LicenseSignatureAlgorithm.MLDsa87, "ML-DSA-87", false)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256, "RSA-SHA256", true)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha384, "RSA-SHA384", true)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha512, "RSA-SHA512", true)]
    public void For_ReturnsTheOptionWithItsLabelAndItsFamily(
        LicenseSignatureAlgorithm algorithm,
        string label,
        bool isRsa)
    {
        SignatureAlgorithmOption option = SignatureAlgorithmOption.For(algorithm);

        Assert.Equal(label, option.Label);
        Assert.Equal(label, option.ToString());
        Assert.Equal(isRsa, option.IsRsa);
        Assert.False(string.IsNullOrWhiteSpace(option.Description));
    }

    [Fact]
    public void For_ReturnsTheSameInstanceAsTheList_SoASelectorMatchesOnReference()
    {
        Assert.Same(
            SignatureAlgorithmOption.All[1],
            SignatureAlgorithmOption.For(LicenseSignatureAlgorithm.MLDsa65));
    }

    [Fact]
    public void For_RejectsSomethingThatIsNotAScheme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SignatureAlgorithmOption.For((LicenseSignatureAlgorithm)99));
    }
}
