using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Generate-once key pairs shared by the key suites: one RSA pair and one ML-DSA pair, enough to
/// exercise both material shapes.
/// </summary>
/// <remarks>
/// <para>
/// RSA key generation is a prime search and costs real time, so the tests that only need <i>a</i> key
/// share these rather than making their own. The tests that are specifically about generation — every
/// algorithm, every accepted RSA size — still generate their own, because that is what they are
/// testing.
/// </para>
/// <para>
/// The RSA pair is 2048-bit: the smallest size the library accepts, and therefore the cheapest one
/// that is still a real key. Nothing in the format depends on the modulus size.
/// </para>
/// </remarks>
public sealed class GeneratedKeyFixture
{
    public GeneratedKeyFixture()
    {
        KeyService service = new();

        Rsa = service.GenerateKeyPair(LicenseSignatureAlgorithm.RsaSha256, rsaKeySizeBits: 2048);
        MLDsa = service.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);
    }

    /// <summary>An RSA-SHA256 pair with a 2048-bit modulus. Its material is PEM text.</summary>
    public LicensingKeyPair Rsa { get; }

    /// <summary>An ML-DSA-65 pair. Its material is the raw FIPS 204 encoding.</summary>
    public LicensingKeyPair MLDsa { get; }
}

/// <summary>Binds <see cref="GeneratedKeyFixture"/> to the shared key test collection.</summary>
[CollectionDefinition(Name)]
public sealed class GeneratedKeyCollection : ICollectionFixture<GeneratedKeyFixture>
{
    public const string Name = "generated-keys";
}
