using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;
using Enigma.Licensing.UnitTests.Keys;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// Covers what the generator puts in an envelope, what it fills in for the caller, and every request
/// it refuses.
/// </summary>
[Collection(SignedLicenseCollection.Name)]
public sealed class LicenseGeneratorTests(SignedLicenseFixture keys)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly LicenseGenerator _generator = new();

    [Fact]
    public void Generate_StampsTheCurrentFormat()
    {
        LicenseEnvelope envelope = _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey);

        Assert.Equal(LicenseEnvelope.CurrentFormat, envelope.Format);
    }

    /// <summary>The identifier comes from the one table that is allowed to spell these strings.</summary>
    [Theory]
    [MemberData(nameof(SignedLicenseFixture.AllAlgorithms), MemberType = typeof(SignedLicenseFixture))]
    public void Generate_WritesTheAlgorithmIdentifier(LicenseSignatureAlgorithm algorithm)
    {
        LicenseEnvelope envelope = _generator.Generate(
            SignedLicenseTestData.Request(), keys.Pair(algorithm).PrivateKey);

        Assert.Equal(AlgorithmIds.ToIdentifier(algorithm), envelope.Algorithm);
    }

    /// <summary>
    /// The pair's fingerprint is stamped into every license it signs — that is what lets an application
    /// shipping several public keys pick the right one before verifying.
    /// </summary>
    [Fact]
    public void Generate_StampsTheKeyId()
    {
        LicenseEnvelope envelope = _generator.Generate(SignedLicenseTestData.Request(), keys.MLDsa.PrivateKey);

        Assert.Equal(keys.MLDsa.PrivateKey.KeyId, envelope.KeyId);
        Assert.Equal(keys.MLDsa.PublicKey.KeyId, envelope.KeyId);
    }

    /// <summary>The stored bytes are the signed bytes: what comes back out of the payload is the license.</summary>
    [Fact]
    public void Generate_CarriesTheRequestedFieldsInThePayload()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with
        {
            Owner = "Contoso Ltd",
            DeviceId = "DESKTOP-ABC123",
            ValidFrom = LicenseTestData.ValidFrom,
            ValidUntil = LicenseTestData.ValidUntil,
        };

        License license = LicensePayload.Deserialize(
            _generator.Generate(request, keys.Rsa.PrivateKey).Payload);

        Assert.Equal("MyApp", license.ProductId);
        Assert.Equal("Contoso Ltd", license.Owner);
        Assert.Equal("DESKTOP-ABC123", license.DeviceId);
        Assert.Equal(LicenseTestData.ValidFrom, license.ValidFrom);
        Assert.Equal(LicenseTestData.ValidUntil, license.ValidUntil);
    }

    /// <summary>A ULID: 26 characters of Crockford base32, and sortable by the instant it was minted.</summary>
    [Fact]
    public void Generate_IssuesAUlidWhenTheRequestHasNoId()
    {
        License license = LicensePayload.Deserialize(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey).Payload);

        Assert.Equal(26, license.Id.Length);
        Assert.True(Ulid.TryParse(license.Id, out _), $"'{license.Id}' is not a ULID.");
    }

    /// <summary>
    /// Two licenses from one request are two licenses, not the same one twice — which is what makes
    /// reissuing meaningful.
    /// </summary>
    [Fact]
    public void Generate_IssuesADistinctIdEachTime()
    {
        LicenseRequest request = SignedLicenseTestData.Request();

        string first = LicensePayload.Deserialize(_generator.Generate(request, keys.Rsa.PrivateKey).Payload).Id;
        string second = LicensePayload.Deserialize(_generator.Generate(request, keys.Rsa.PrivateKey).Payload).Id;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_KeepsAnIdTheRequestSupplies()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { Id = "REISSUE-OF-AN-OLD-ONE" };

        License license = LicensePayload.Deserialize(
            _generator.Generate(request, keys.Rsa.PrivateKey).Payload);

        Assert.Equal("REISSUE-OF-AN-OLD-ONE", license.Id);
    }

    [Fact]
    public void Generate_StampsUtcNowWhenTheRequestHasNoCreatedAt()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        License license = LicensePayload.Deserialize(
            _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PrivateKey).Payload);

        Assert.InRange(license.CreatedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Generate_KeepsACreatedAtTheRequestSupplies()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { CreatedAt = LicenseTestData.CreatedAt };

        License license = LicensePayload.Deserialize(
            _generator.Generate(request, keys.Rsa.PrivateKey).Payload);

        Assert.Equal(LicenseTestData.CreatedAt, license.CreatedAt);
    }

    /// <summary>
    /// Hedged ML-DSA signing mixes in fresh randomness per call, so two signatures over the same bytes
    /// differ. Both are valid FIPS 204 signatures — this pins that the library does not depend on them
    /// being identical.
    /// </summary>
    [Fact]
    public void Generate_DoesNotRequireDeterministicSignatures()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { Id = "FIXED", CreatedAt = LicenseTestData.CreatedAt };

        LicenseEnvelope first = _generator.Generate(request, keys.MLDsa.PrivateKey);
        LicenseEnvelope second = _generator.Generate(request, keys.MLDsa.PrivateKey);

        Assert.Equal(first.Payload, second.Payload);
        Assert.True(
            new LicenseValidator().Validate(first, keys.MLDsa.PublicKey).IsValid &&
            new LicenseValidator().Validate(second, keys.MLDsa.PublicKey).IsValid,
            "Both signatures over identical payload bytes must verify.");
    }

    [Fact]
    public void Generate_ThrowsForANullRequest()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => _generator.Generate(null!, keys.Rsa.PrivateKey));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Generate_ThrowsForANullKey()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => _generator.Generate(SignedLicenseTestData.Request(), null!));

        Assert.Equal("privateKey", exception.ParamName);
    }

    /// <summary>A public key cannot sign, and finding that out at the provider layer would be baffling.</summary>
    [Fact]
    public void Generate_ThrowsForAPublicKey()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _generator.Generate(SignedLicenseTestData.Request(), keys.Rsa.PublicKey));

        Assert.Equal("privateKey", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_ThrowsForABlankProductId(string productId)
    {
        LicenseRequest request = new() { ProductId = productId };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _generator.Generate(request, keys.Rsa.PrivateKey));

        Assert.Equal("request", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Generate_ThrowsForABlankId(string id)
    {
        LicenseRequest request = SignedLicenseTestData.Request() with { Id = id };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _generator.Generate(request, keys.Rsa.PrivateKey));

        Assert.Equal("request", exception.ParamName);
    }

    /// <summary>A window that closes before it opens can never be valid, so it is a mistake, not a license.</summary>
    [Fact]
    public void Generate_ThrowsForAWindowThatEndsBeforeItStarts()
    {
        LicenseRequest request = SignedLicenseTestData.Request() with
        {
            ValidFrom = LicenseTestData.ValidUntil,
            ValidUntil = LicenseTestData.ValidFrom,
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _generator.Generate(request, keys.Rsa.PrivateKey));

        Assert.Equal("request", exception.ParamName);
    }

    /// <summary>Only one of the two bounds set is a half-open window, not a broken one.</summary>
    [Fact]
    public void Generate_AcceptsAHalfOpenWindow()
    {
        LicenseRequest fromOnly = SignedLicenseTestData.Request() with { ValidFrom = LicenseTestData.ValidUntil };
        LicenseRequest untilOnly = SignedLicenseTestData.Request() with { ValidUntil = LicenseTestData.ValidFrom };

        Assert.NotNull(_generator.Generate(fromOnly, keys.Rsa.PrivateKey));
        Assert.NotNull(_generator.Generate(untilOnly, keys.Rsa.PrivateKey));
    }

    [Fact]
    public async Task GenerateAndSaveAsync_WritesTheEnvelopeAndLeavesTheStreamOpen()
    {
        using MemoryStream output = new();

        LicenseEnvelope envelope = await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.Request(), keys.Rsa.PrivateKey, output, Token);

        Assert.True(output.CanWrite);

        output.Position = 0;
        LicenseEnvelope reloaded = await LicenseEnvelope.LoadAsync(output, Token);

        Assert.Equal(envelope.Payload, reloaded.Payload);
        Assert.Equal(envelope.Signature, reloaded.Signature);
        Assert.Equal(envelope.KeyId, reloaded.KeyId);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_ThrowsForANullStream()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _generator.GenerateAndSaveAsync(
                SignedLicenseTestData.Request(), keys.Rsa.PrivateKey, (Stream)null!, Token));

        Assert.Equal("output", exception.ParamName);
    }

    /// <summary>The whole issue-a-license flow in one call, including the directory nobody remembers to create.</summary>
    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_CreatesMissingParentDirectories()
    {
        using TemporaryDirectory directory = new();
        string keyPath = directory.File("keys", "app.private.key");
        string licensePath = directory.File("out", "nested", "app.license");

        await new KeyService().SaveKeyPairAsync(
            keys.MLDsa, directory.File("keys", "app.public.key"), keyPath, password: null, Token);

        LicenseEnvelope envelope = await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.Request(), keyPath, password: null, licensePath, Token);

        Assert.True(File.Exists(licensePath));

        using FileStream input = File.OpenRead(licensePath);
        LicenseEnvelope reloaded = await LicenseEnvelope.LoadAsync(input, Token);

        Assert.Equal(envelope.Signature, reloaded.Signature);
    }

    /// <summary>An encrypted key file works the same way — the password is simply passed through.</summary>
    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_OpensAnEncryptedPrivateKey()
    {
        using TemporaryDirectory directory = new();
        string keyPath = directory.File("app.private.key");
        char[] password = KeyTestData.Password();

        await new KeyService().SaveKeyPairAsync(
            keys.MLDsa, directory.File("app.public.key"), keyPath, password, Token);

        LicenseEnvelope envelope = await _generator.GenerateAndSaveAsync(
            SignedLicenseTestData.Request(), keyPath, password, directory.File("app.license"), Token);

        Assert.Equal(keys.MLDsa.PublicKey.KeyId, envelope.KeyId);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_PropagatesAMissingKeyFile()
    {
        using TemporaryDirectory directory = new();
        Directory.CreateDirectory(directory.Path);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _generator.GenerateAndSaveAsync(
                SignedLicenseTestData.Request(),
                directory.File("absent.key"),
                password: null,
                directory.File("app.license"),
                Token));
    }

    /// <summary>A rejected request must not leave a truncated license behind.</summary>
    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_WritesNothingWhenTheRequestIsRejected()
    {
        using TemporaryDirectory directory = new();
        string keyPath = directory.File("app.private.key");
        string licensePath = directory.File("app.license");

        await new KeyService().SaveKeyPairAsync(
            keys.MLDsa, directory.File("app.public.key"), keyPath, password: null, Token);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _generator.GenerateAndSaveAsync(
                new LicenseRequest { ProductId = "  " }, keyPath, password: null, licensePath, Token));

        Assert.False(File.Exists(licensePath));
    }

    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_ThrowsForANullOutputPath()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _generator.GenerateAndSaveAsync(
                SignedLicenseTestData.Request(), "some.key", password: null, null!, Token));

        Assert.Equal("outputPath", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAndSaveAsync_ByPath_ThrowsForABlankOutputPath(string path)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _generator.GenerateAndSaveAsync(
                SignedLicenseTestData.Request(), "some.key", password: null, path, Token));

        Assert.Equal("outputPath", exception.ParamName);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_ByPath_ThrowsForANullKeyPath()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _generator.GenerateAndSaveAsync(
                SignedLicenseTestData.Request(), null!, password: null, "out.license", Token));

        Assert.Equal("privateKeyPath", exception.ParamName);
    }
}
