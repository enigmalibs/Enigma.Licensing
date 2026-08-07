using System;
using System.Collections.Generic;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// One generate-once key pair per <see cref="LicenseSignatureAlgorithm"/> member, shared by the
/// generator, validator and store suites.
/// </summary>
/// <remarks>
/// <para>
/// All six, because signing and verifying is what this phase adds and a scheme that is never exercised
/// is a scheme nobody knows works. The RSA pairs are 2048-bit — the smallest size the library accepts,
/// and therefore the cheapest that is still a real key. Nothing about signing depends on the modulus
/// size; <c>KeyGenerationTests</c> is where the accepted sizes are covered.
/// </para>
/// <para>
/// Generated once for the whole collection: three prime searches and three ML-DSA key generations is
/// already the most expensive fixture in the suite, and every test below only needs <i>a</i> key.
/// </para>
/// </remarks>
public sealed class SignedLicenseFixture
{
    private readonly Dictionary<LicenseSignatureAlgorithm, LicensingKeyPair> _pairs = [];

    public SignedLicenseFixture()
    {
        KeyService service = new();

        foreach (LicenseSignatureAlgorithm algorithm in All)
        {
            _pairs[algorithm] = service.GenerateKeyPair(algorithm, rsaKeySizeBits: 2048);
        }
    }

    /// <summary>Every defined member, in declaration order — the theory data for the round-trip matrix.</summary>
    public static IEnumerable<LicenseSignatureAlgorithm> All =>
    [
        LicenseSignatureAlgorithm.RsaSha256,
        LicenseSignatureAlgorithm.RsaSha384,
        LicenseSignatureAlgorithm.RsaSha512,
        LicenseSignatureAlgorithm.MLDsa44,
        LicenseSignatureAlgorithm.MLDsa65,
        LicenseSignatureAlgorithm.MLDsa87,
    ];

    /// <summary>Every defined member, as xUnit theory rows.</summary>
    public static TheoryData<LicenseSignatureAlgorithm> AllAlgorithms => [.. All];

    /// <summary>The RSA-SHA256 pair — the default choice wherever a test just needs a PEM-backed key.</summary>
    public LicensingKeyPair Rsa => Pair(LicenseSignatureAlgorithm.RsaSha256);

    /// <summary>The ML-DSA-65 pair — the default choice wherever a test just needs a raw-bytes key.</summary>
    public LicensingKeyPair MLDsa => Pair(LicenseSignatureAlgorithm.MLDsa65);

    /// <summary>The pair generated for <paramref name="algorithm"/>.</summary>
    public LicensingKeyPair Pair(LicenseSignatureAlgorithm algorithm) =>
        _pairs.TryGetValue(algorithm, out LicensingKeyPair? pair)
            ? pair
            : throw new InvalidOperationException($"No pair was generated for {algorithm}.");
}

/// <summary>Binds <see cref="SignedLicenseFixture"/> to the signing test collection.</summary>
[CollectionDefinition(Name)]
public sealed class SignedLicenseCollection : ICollectionFixture<SignedLicenseFixture>
{
    public const string Name = "signed-licenses";
}
