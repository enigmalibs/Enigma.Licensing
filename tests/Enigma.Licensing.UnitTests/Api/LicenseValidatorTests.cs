using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;
using Enigma.Licensing.UnitTests.Keys;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// The validation matrix: every verdict the validator can reach, and the guarantee that it reaches a
/// verdict rather than throwing whenever the license is what went wrong.
/// </summary>
[Collection(SignedLicenseCollection.Name)]
public sealed class LicenseValidatorTests(SignedLicenseFixture keys)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly LicenseGenerator _generator = new();
    private readonly LicenseValidator _validator = new();

    // ---- The round trip, for every scheme ------------------------------------------------------

    /// <summary>
    /// The item's headline claim: a license signed with any of the six schemes survives save → load →
    /// validate.
    /// </summary>
    [Theory]
    [MemberData(nameof(SignedLicenseFixture.AllAlgorithms), MemberType = typeof(SignedLicenseFixture))]
    public async Task Validate_AcceptsALicenseRoundTrippedThroughAFile(LicenseSignatureAlgorithm algorithm)
    {
        LicensingKeyPair pair = keys.Pair(algorithm);

        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.InWindow(), pair.PrivateKey);
        string document = await SignedLicenseTestData.SaveToStringAsync(signed, Token);

        using MemoryStream input = SignedLicenseTestData.StreamOf(document);
        LicenseEnvelope reloaded = await LicenseEnvelope.LoadAsync(input, Token);

        LicenseValidationResult result = _validator.Validate(
            reloaded, pair.PublicKey, SignedLicenseTestData.ProductId);

        Assert.True(result.IsValid, result.Message);
        Assert.Null(result.Failure);
        Assert.NotNull(result.License);
        Assert.Equal(SignedLicenseTestData.ProductId, result.License.ProductId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_AcceptsAPerpetualLicense(bool rsa)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Request(), pair.PrivateKey), pair.PublicKey);

        Assert.True(result.IsValid, result.Message);
    }

    // ---- The validity window -------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_ReportsAnExpiredLicense(bool rsa)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Expired(), pair.PrivateKey), pair.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.Expired, result.Failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_ReportsALicenseThatIsNotYetValid(bool rsa)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.NotYetValid(), pair.PrivateKey), pair.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.NotYetValid, result.Failure);
    }

    /// <summary>A license bounded on only one side is judged on that side alone.</summary>
    [Fact]
    public void Validate_AcceptsAWindowOpenAtOneEnd()
    {
        LicenseRequest openEnded = SignedLicenseTestData.Request() with
        {
            ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
        };
        LicenseRequest openStarted = SignedLicenseTestData.Request() with
        {
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
        };

        Assert.True(_validator.Validate(
            _generator.Generate(openEnded, keys.Rsa.PrivateKey), keys.Rsa.PublicKey).IsValid);
        Assert.True(_validator.Validate(
            _generator.Generate(openStarted, keys.Rsa.PrivateKey), keys.Rsa.PublicKey).IsValid);
    }

    /// <summary>
    /// A failed verdict still carries the license, so an application can say what expired and when
    /// instead of "invalid licence".
    /// </summary>
    [Fact]
    public void Validate_CarriesTheLicenseOnAWindowFailure()
    {
        LicenseRequest request = SignedLicenseTestData.Expired() with { Owner = "Contoso Ltd" };

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);

        Assert.NotNull(result.License);
        Assert.Equal("Contoso Ltd", result.License.Owner);
    }

    /// <summary>
    /// <c>CreatedAt</c> is informational — a license issued "in the future" is still valid, because the
    /// window is the only thing that gates.
    /// </summary>
    [Fact]
    public void Validate_IgnoresCreatedAt()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddYears(5),
        };

        Assert.True(_validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey).IsValid);
    }

    // ---- Product matching ----------------------------------------------------------------------

    [Fact]
    public void Validate_ReportsAProductMismatch()
    {
        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey,
            productId: "SomethingElse");

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.ProductMismatch, result.Failure);
        Assert.NotNull(result.License);
    }

    /// <summary>Passing no product identifier asks only whether the license is authentic and in date.</summary>
    [Fact]
    public void Validate_SkipsTheProductCheckWhenNoProductIsAsked()
    {
        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey), keys.Rsa.PublicKey);

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("MyApp 2.*", "MyApp 2.0", true)]
    [InlineData("MyApp 2.*", "MyApp 2.17", true)]
    [InlineData("MyApp 2.*", "MyApp 2.", true)]
    [InlineData("MyApp 2.*", "MyApp 3.0", false)]
    [InlineData("MyApp 2.*", "MyApp 2", false)]
    [InlineData("*", "anything at all", true)]
    // The dot is a literal: 'MyApp 2x0' would match if the pattern were built without escaping first,
    // which is exactly the predecessor's bug.
    [InlineData("MyApp 2.0", "MyApp 2x0", false)]
    [InlineData("MyApp (Pro)", "MyApp (Pro)", true)]
    [InlineData("MyApp+", "MyApp+", true)]
    // '+' is a literal on the license's side, so it does not repeat the preceding character.
    [InlineData("MyApp+", "MyAppp", false)]
    public void Validate_MatchesTheProductIdentifier(string licensed, string requested, bool expected)
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { ProductId = licensed };

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey, requested);

        Assert.Equal(expected, result.IsValid);
    }

    /// <summary>The wildcard is only ever honoured on the license's side.</summary>
    [Fact]
    public void Validate_TreatsTheRequestedProductAsALiteral()
    {
        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey,
            productId: "*");

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.ProductMismatch, result.Failure);
    }

    // ---- Device matching -----------------------------------------------------------------------

    [Fact]
    public void Validate_AcceptsTheDeviceItIsLockedTo()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { DeviceId = "DESKTOP-ABC123" };

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey,
            SignedLicenseTestData.ProductId,
            "DESKTOP-ABC123");

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("DESKTOP-XYZ789")]
    [InlineData("desktop-abc123")]
    public void Validate_ReportsADeviceMismatch(string deviceId)
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { DeviceId = "DESKTOP-ABC123" };

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey,
            SignedLicenseTestData.ProductId,
            deviceId);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.DeviceMismatch, result.Failure);
        Assert.NotNull(result.License);
    }

    /// <summary>An unlocked license runs anywhere, whatever device the application names.</summary>
    [Fact]
    public void Validate_AcceptsAnyDeviceForAnUnlockedLicense()
    {
        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey),
            keys.Rsa.PublicKey,
            SignedLicenseTestData.ProductId,
            "SOME-OTHER-MACHINE");

        Assert.True(result.IsValid, result.Message);
    }

    /// <summary>
    /// Skipping the device check means passing it. Documented, and worth pinning: an application that
    /// locks licenses must pass the identifier on every call.
    /// </summary>
    [Fact]
    public void Validate_SkipsTheDeviceCheckWhenNoDeviceIsAsked()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { DeviceId = "DESKTOP-ABC123" };

        LicenseValidationResult result = _validator.Validate(
            _generator.Generate(request, keys.Rsa.PrivateKey), keys.Rsa.PublicKey);

        Assert.True(result.IsValid, result.Message);
    }

    // ---- Tampering -----------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_RejectsATamperedPayload(bool rsa)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), pair.PrivateKey);

        LicenseEnvelope tampered = signed with
        {
            Payload = SignedLicenseTestData.FlipLastBit(signed.Payload),
        };

        LicenseValidationResult result = _validator.Validate(tampered, pair.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidSignature, result.Failure);
        // Nothing past the signature check runs, so there is no license to report — the bytes were
        // never trusted enough to read.
        Assert.Null(result.License);
    }

    /// <summary>
    /// The realistic attack: edit the license text in the file, leaving the signature alone. The
    /// signature covers the payload bytes exactly as stored, so it stops matching.
    /// </summary>
    [Fact]
    public async Task Validate_RejectsAPayloadEditedInTheFile()
    {
        LicenseEnvelope signed = _generator.Generate(
            SignedLicenseTestData.Expired(), keys.Rsa.PrivateKey);

        string document = await SignedLicenseTestData.SaveToStringAsync(signed, Token);

        // Extend the licence by a decade, the way somebody with a text editor would.
        License original = LicensePayload.Deserialize(signed.Payload);
        byte[] rewritten = LicensePayload.Serialize(
            original with { ValidUntil = DateTimeOffset.UtcNow.AddYears(10) });

        string edited = SignedLicenseTestData.Edit(
            document, root => root["payload"] = Convert.ToBase64String(rewritten));

        using MemoryStream input = SignedLicenseTestData.StreamOf(edited);
        LicenseEnvelope reloaded = await LicenseEnvelope.LoadAsync(input, Token);

        LicenseValidationResult result = _validator.Validate(reloaded, keys.Rsa.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidSignature, result.Failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_RejectsATamperedSignature(bool rsa)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), pair.PrivateKey);

        LicenseEnvelope tampered = signed with
        {
            Signature = SignedLicenseTestData.FlipLastBit(signed.Signature),
        };

        Assert.Equal(
            LicenseFailure.InvalidSignature,
            _validator.Validate(tampered, pair.PublicKey).Failure);
    }

    /// <summary>
    /// The load-bearing test for the design. Neither <c>format</c> nor <c>algorithm</c> is inside the
    /// signed bytes, and this is why that is safe: the algorithm <i>selects the verifier</i>, so
    /// rewriting it means checking the signature under a scheme it was not produced with.
    /// </summary>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256, "RSA-SHA384")]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256, "RSA-SHA512")]
    [InlineData(LicenseSignatureAlgorithm.RsaSha512, "RSA-SHA256")]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65, "ML-DSA-44")]
    [InlineData(LicenseSignatureAlgorithm.MLDsa65, "ML-DSA-87")]
    public void Validate_RejectsASubstitutedAlgorithm(LicenseSignatureAlgorithm signedWith, string substituted)
    {
        LicensingKeyPair pair = keys.Pair(signedWith);
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), pair.PrivateKey);

        LicenseValidationResult result = _validator.Validate(
            signed with { Algorithm = substituted }, pair.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidSignature, result.Failure);
    }

    /// <summary>Swapping an RSA identifier for an ML-DSA one is the same story across families.</summary>
    [Fact]
    public void Validate_RejectsAnAlgorithmSubstitutedAcrossFamilies()
    {
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        Assert.Equal(
            LicenseFailure.InvalidSignature,
            _validator.Validate(signed with { Algorithm = "ML-DSA-65" }, keys.Rsa.PublicKey).Failure);
    }

    // ---- Wrong keys ----------------------------------------------------------------------------

    [Fact]
    public void Validate_RejectsALicenseSignedByADifferentKey()
    {
        LicensingKeyPair other = new KeyService().GenerateKeyPair(
            LicenseSignatureAlgorithm.RsaSha256, rsaKeySizeBits: 2048);

        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        Assert.Equal(
            LicenseFailure.InvalidSignature,
            _validator.Validate(signed, other.PublicKey).Failure);
    }

    /// <summary>
    /// A key from the other family cannot even be fed to the primitive. That must be a verdict, not
    /// whatever exception the provider would raise several layers down.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_RejectsAKeyFromTheOtherAlgorithmFamily(bool signWithRsa)
    {
        LicensingKeyPair signing = signWithRsa ? keys.Rsa : keys.MLDsa;
        LicensingKeyPair verifying = signWithRsa ? keys.MLDsa : keys.Rsa;

        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), signing.PrivateKey);

        LicenseValidationResult result = _validator.Validate(signed, verifying.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidSignature, result.Failure);
    }

    /// <summary>An ML-DSA key of the wrong parameter set is the same situation within one family.</summary>
    [Fact]
    public void Validate_RejectsAnMLDsaKeyOfTheWrongParameterSet()
    {
        LicenseEnvelope signed = _generator.Generate(
            SignedLicenseTestData.Request(), keys.Pair(LicenseSignatureAlgorithm.MLDsa65).PrivateKey);

        LicenseValidationResult result = _validator.Validate(
            signed, keys.Pair(LicenseSignatureAlgorithm.MLDsa87).PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidSignature, result.Failure);
    }

    // ---- Malformed documents -------------------------------------------------------------------

    [Theory]
    [InlineData("enigma-license/2")]
    [InlineData("ENIGMA-LICENSE/1")]
    [InlineData("")]
    public void Validate_ReportsAnUnknownFormat(string format)
    {
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        LicenseValidationResult result = _validator.Validate(signed with { Format = format }, keys.Rsa.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidFormat, result.Failure);
    }

    /// <summary>
    /// An unknown algorithm is a verdict here, unlike in a key file: the document is well-formed, there
    /// is simply nothing that can check it.
    /// </summary>
    [Theory]
    [InlineData("RSA-SHA1")]
    [InlineData("rsa-sha256")]
    [InlineData("ML-DSA-99")]
    [InlineData("")]
    public void Validate_ReportsAnUnknownAlgorithm(string algorithm)
    {
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        LicenseValidationResult result = _validator.Validate(
            signed with { Algorithm = algorithm }, keys.Rsa.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.UnknownAlgorithm, result.Failure);
        Assert.Null(result.License);
    }

    /// <summary>
    /// Authentic and still unreadable — a license from a future version of this library. The signature
    /// verified, so the bytes are genuine; they are just not a license this build understands.
    /// </summary>
    [Fact]
    public void Validate_ReportsAPayloadThatIsNotALicense()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"somethingElse\":true}");

        LicenseEnvelope envelope = new()
        {
            Format = LicenseEnvelope.CurrentFormat,
            Algorithm = "ML-DSA-65",
            KeyId = keys.MLDsa.PublicKey.KeyId,
            Payload = payload,
            Signature = new LicenseSignatures().Sign(keys.MLDsa.PrivateKey, payload),
        };

        LicenseValidationResult result = _validator.Validate(envelope, keys.MLDsa.PublicKey);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidFormat, result.Failure);
        Assert.Null(result.License);
    }

    // ---- The stream and path overloads ---------------------------------------------------------

    [Fact]
    public async Task ValidateAsync_ByStream_ValidatesAWholeRoundTrip()
    {
        using MemoryStream license = new();
        using MemoryStream publicKey = new();

        await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.InWindow(), keys.Rsa.PrivateKey, license, Token);
        await new KeyService().SaveKeyAsync(keys.Rsa.PublicKey, publicKey, password: null, Token);

        license.Position = 0;
        publicKey.Position = 0;

        LicenseValidationResult result = await _validator.ValidateAsync(
            license, publicKey, SignedLicenseTestData.ProductId, deviceId: null, Token);

        Assert.True(result.IsValid, result.Message);
    }

    /// <summary>
    /// The contract that matters most: a license is untrusted input, so nothing it can be makes this
    /// throw.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"format\":\"enigma-license/1\"}")]
    [InlineData("{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\",\"payload\":\"not base64!!\",\"signature\":\"AA==\"}")]
    [InlineData("{\"format\":\"enigma-license/9\",\"algorithm\":\"RSA-SHA256\",\"payload\":\"AA==\",\"signature\":\"AA==\"}")]
    public async Task ValidateAsync_ByStream_ReturnsAVerdictForAnUnreadableLicense(string json)
    {
        using MemoryStream license = SignedLicenseTestData.StreamOf(json);
        using MemoryStream publicKey = new();

        await new KeyService().SaveKeyAsync(keys.Rsa.PublicKey, publicKey, password: null, Token);
        publicKey.Position = 0;

        LicenseValidationResult result = await _validator.ValidateAsync(
            license, publicKey, cancellationToken: Token);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseFailure.InvalidFormat, result.Failure);
        Assert.NotNull(result.Message);
    }

    /// <summary>
    /// The key is the other way round: it ships inside the application, so a bad one is a deployment
    /// fault and must not be reported as a verdict about the customer's license.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ByStream_PropagatesAMalformedKeyDocument()
    {
        using MemoryStream license = new();
        using MemoryStream publicKey = SignedLicenseTestData.StreamOf("{\"format\":\"nonsense\"}");

        await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.Request(), keys.Rsa.PrivateKey, license, Token);
        license.Position = 0;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _validator.ValidateAsync(license, publicKey, cancellationToken: Token));
    }

    [Fact]
    public async Task ValidateAsync_ByStream_ThrowsForANullStream()
    {
        using MemoryStream stream = new();

        Assert.Equal(
            "licenseInput",
            (await Assert.ThrowsAsync<ArgumentNullException>(
                () => _validator.ValidateAsync(null!, stream, cancellationToken: Token))).ParamName);

        Assert.Equal(
            "publicKeyInput",
            (await Assert.ThrowsAsync<ArgumentNullException>(
                () => _validator.ValidateAsync(stream, null!, cancellationToken: Token))).ParamName);
    }

    [Fact]
    public async Task ValidateAsync_ByPath_ValidatesAWholeRoundTrip()
    {
        using TemporaryDirectory directory = new();
        string publicKeyPath = directory.File("app.public.key");
        string licensePath = directory.File("licenses", "app.license");

        await new KeyService().SaveKeyPairAsync(
            keys.MLDsa, publicKeyPath, directory.File("app.private.key"), password: null, Token);
        await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.InWindow(), directory.File("app.private.key"), password: null,
            licensePath, Token);

        LicenseValidationResult result = await _validator.ValidateAsync(
            licensePath, publicKeyPath, SignedLicenseTestData.ProductId, deviceId: null, Token);

        Assert.True(result.IsValid, result.Message);
    }

    /// <summary>
    /// A missing license file is an I/O failure, not a verdict: the license was never read, so there is
    /// nothing to judge.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ByPath_PropagatesAMissingLicenseFile()
    {
        using TemporaryDirectory directory = new();
        string publicKeyPath = directory.File("app.public.key");

        await new KeyService().SaveKeyPairAsync(
            keys.Rsa, publicKeyPath, directory.File("app.private.key"), password: null, Token);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _validator.ValidateAsync(
                directory.File("absent.license"), publicKeyPath, cancellationToken: Token));
    }

    [Fact]
    public async Task ValidateAsync_ByPath_ThrowsForAnUnusablePath()
    {
        Assert.Equal(
            "licensePath",
            (await Assert.ThrowsAsync<ArgumentNullException>(
                () => _validator.ValidateAsync(null!, "key", cancellationToken: Token))).ParamName);

        Assert.Equal(
            "publicKeyPath",
            (await Assert.ThrowsAsync<ArgumentException>(
                () => _validator.ValidateAsync("license", "  ", cancellationToken: Token))).ParamName);
    }

    // ---- Argument contract ---------------------------------------------------------------------

    [Fact]
    public void Validate_ThrowsForANullEnvelope()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(null!, keys.Rsa.PublicKey));

        Assert.Equal("envelope", exception.ParamName);
    }

    [Fact]
    public void Validate_ThrowsForANullKey()
    {
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(signed, null!));

        Assert.Equal("publicKey", exception.ParamName);
    }

    /// <summary>
    /// Verifying with a private key would "work", and would mean an application shipping the ability to
    /// mint its own licenses. That is a programming mistake, not a verdict.
    /// </summary>
    [Fact]
    public void Validate_ThrowsForAPrivateKey()
    {
        LicenseEnvelope signed = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _validator.Validate(signed, keys.Rsa.PrivateKey));

        Assert.Equal("publicKey", exception.ParamName);
    }

    [Fact]
    public async Task ValidateAsync_ByStream_ThrowsWhenHandedAPrivateKeyFile()
    {
        using MemoryStream license = new();
        using MemoryStream privateKey = new();

        await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.Request(), keys.Rsa.PrivateKey, license, Token);
        await new KeyService().SaveKeyAsync(keys.Rsa.PrivateKey, privateKey, password: null, Token);

        license.Position = 0;
        privateKey.Position = 0;

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _validator.ValidateAsync(license, privateKey, cancellationToken: Token));

        Assert.Equal("publicKeyInput", exception.ParamName);
    }
}
