using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Covers the password protection of a private key: Argon2id into AES-256-GCM, at the file level, the
/// same way for both algorithm families.
/// </summary>
[Collection(GeneratedKeyCollection.Name)]
public sealed class KeyEncryptionTests(GeneratedKeyFixture keys)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly KeyService _service = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RoundTrip_RecoversTheMaterialWithTheRightPassword(bool rsa)
    {
        LicensingKey original = (rsa ? keys.Rsa : keys.MLDsa).PrivateKey;

        LicensingKey restored = await KeyTestData.RoundTripAsync(
            _service, original, KeyTestData.Password(), Token);

        Assert.Equal(original.Material, restored.Material);
        Assert.Equal(original.KeyId, restored.KeyId);
        Assert.Equal(original.Algorithm, restored.Algorithm);
        Assert.Equal(KeyKind.Private, restored.Kind);
    }

    /// <summary>The documented <c>encryption</c> object: every parameter a reader needs, and nothing secret.</summary>
    [Fact]
    public async Task SaveKeyAsync_RecordsTheKeyDerivationParameters()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        Assert.Equal(
            ["format", "kind", "algorithm", "keyId", "encryption", "key"],
            KeyTestData.PropertyNames(document));
        Assert.Equal(
            ["kdf", "iterations", "memoryKb", "parallelism", "salt", "cipher", "nonce", "tagBits"],
            KeyTestData.PropertyNames(document, "encryption"));

        JsonObject encryption = KeyTestData.Encryption(document);
        Assert.Equal("argon2id", encryption["kdf"]!.GetValue<string>());
        Assert.Equal(3, encryption["iterations"]!.GetValue<int>());
        Assert.Equal(65_536, encryption["memoryKb"]!.GetValue<int>());
        Assert.Equal(4, encryption["parallelism"]!.GetValue<int>());
        Assert.Equal("AES-256-GCM", encryption["cipher"]!.GetValue<string>());
        Assert.Equal(128, encryption["tagBits"]!.GetValue<int>());
        Assert.Equal(16, Convert.FromBase64String(encryption["salt"]!.GetValue<string>()).Length);
        Assert.Equal(12, Convert.FromBase64String(encryption["nonce"]!.GetValue<string>()).Length);
    }

    /// <summary>
    /// Encrypted RSA material is base64 ciphertext, not PEM — the <c>encryption</c> object, never the
    /// content, is what tells a reader which encoding it is looking at.
    /// </summary>
    [Fact]
    public async Task SaveKeyAsync_ReplacesThePemWithCiphertext()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.Rsa.PrivateKey, KeyTestData.Password(), Token);

        string key = KeyTestData.Field(document, "key");
        Assert.DoesNotContain("-----BEGIN", key, StringComparison.Ordinal);
        Assert.Equal(
            keys.Rsa.PrivateKey.Material.Length + 16,
            Convert.FromBase64String(key).Length);
    }

    /// <summary>
    /// A fresh salt and nonce every time, so encrypting one key twice never produces the same file —
    /// and never reuses a nonce under a derived key.
    /// </summary>
    [Fact]
    public async Task SaveKeyAsync_UsesFreshRandomnessEachTime()
    {
        string first = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);
        string second = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        Assert.NotEqual(
            KeyTestData.Encryption(first)["salt"]!.GetValue<string>(),
            KeyTestData.Encryption(second)["salt"]!.GetValue<string>());
        Assert.NotEqual(
            KeyTestData.Encryption(first)["nonce"]!.GetValue<string>(),
            KeyTestData.Encryption(second)["nonce"]!.GetValue<string>());
        Assert.NotEqual(KeyTestData.Field(first, "key"), KeyTestData.Field(second, "key"));
    }

    [Fact]
    public async Task LoadKeyAsync_RejectsTheWrongPassword()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, document, KeyTestData.WrongPassword(), Token));
    }

    [Fact]
    public async Task LoadKeyAsync_RejectsAMissingPassword()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        CryptographicException exception = await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, document, password: null, Token));

        Assert.Contains("password is required", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// GCM authenticates the material, so a modified file fails to open rather than yielding subtly
    /// wrong key material. Each of these edits leaves the document perfectly well-formed.
    /// </summary>
    [Theory]
    [InlineData("key")]
    [InlineData("salt")]
    [InlineData("nonce")]
    public async Task LoadKeyAsync_RejectsATamperedFile(string field)
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string tampered = KeyTestData.Edit(document, root =>
        {
            JsonObject target = field == "key" ? root : (JsonObject)root["encryption"]!;
            target[field] = KeyTestData.FlipLastByte(target[field]!.GetValue<string>());
        });

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, tampered, KeyTestData.Password(), Token));
    }

    [Fact]
    public async Task LoadKeyAsync_RejectsTruncatedCiphertext()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string truncated = KeyTestData.Edit(document, root =>
        {
            byte[] ciphertext = Convert.FromBase64String(root["key"]!.GetValue<string>());
            root["key"] = Convert.ToBase64String(ciphertext, 0, ciphertext.Length - 8);
        });

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, truncated, KeyTestData.Password(), Token));
    }

    [Theory]
    [InlineData("kdf", "pbkdf2")]
    [InlineData("kdf", "argon2i")]
    [InlineData("cipher", "AES-256-CBC")]
    [InlineData("cipher", "ChaCha20-Poly1305")]
    public async Task LoadKeyAsync_RejectsAPrimitiveTheFormatDoesNotDefine(string field, string value)
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string edited = KeyTestData.Edit(document, root => ((JsonObject)root["encryption"]!)[field] = value);

        CryptographicException exception = await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, KeyTestData.Password(), Token));

        Assert.Contains(value, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The costs are read from the document before the password is checked, so an absurd one is a
    /// denial-of-service dressed as a setting. They are bounded, and the bound is what this asserts —
    /// a test that took a minute to fail would be a test that found the bug too late.
    /// </summary>
    [Theory]
    [InlineData("iterations", 0)]
    [InlineData("iterations", -1)]
    [InlineData("iterations", 65)]
    [InlineData("memoryKb", 0)]
    [InlineData("memoryKb", 1_048_577)]
    [InlineData("memoryKb", int.MaxValue)]
    [InlineData("parallelism", 0)]
    [InlineData("parallelism", 65)]
    public async Task LoadKeyAsync_RejectsKeyDerivationCostsOutsideTheAcceptedRange(string field, int value)
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string edited = KeyTestData.Edit(document, root => ((JsonObject)root["encryption"]!)[field] = value);

        CryptographicException exception = await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, KeyTestData.Password(), Token));

        Assert.Contains("outside the accepted range", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(129)]
    [InlineData(100)]
    public async Task LoadKeyAsync_RejectsAnInvalidTagSize(int tagBits)
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string edited = KeyTestData.Edit(document, root => ((JsonObject)root["encryption"]!)["tagBits"] = tagBits);

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, KeyTestData.Password(), Token));
    }

    /// <summary>An empty salt or nonce means the file is not carrying real parameters.</summary>
    [Theory]
    [InlineData("salt")]
    [InlineData("nonce")]
    public async Task LoadKeyAsync_RejectsEmptyDerivationInputs(string field)
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        string edited = KeyTestData.Edit(document, root => ((JsonObject)root["encryption"]!)[field] = "");

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, KeyTestData.Password(), Token));
    }

    /// <summary>A public key is meant to be distributed. Encrypting one protects nothing and hides a mistake.</summary>
    [Fact]
    public async Task SaveKeyAsync_RejectsAPasswordForAPublicKey()
    {
        using MemoryStream output = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveKeyAsync(keys.Rsa.PublicKey, output, KeyTestData.Password(), Token));

        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public async Task LoadKeyAsync_RejectsAPasswordForAPublicKey()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.Rsa.PublicKey, password: null, Token);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, document, KeyTestData.Password(), Token));

        Assert.Equal("password", exception.ParamName);
    }

    /// <summary>
    /// A public key file carrying an <c>encryption</c> object is malformed whatever it contains — the
    /// format has no way to produce one, so the document is not an <c>enigma-key/1</c> public key.
    /// </summary>
    [Fact]
    public async Task LoadKeyAsync_RejectsAPublicKeyCarryingEncryption()
    {
        string privateDocument = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);
        string disguised = KeyTestData.Edit(privateDocument, root => root["kind"] = "public");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, disguised, password: null, Token));

        Assert.Equal("input", exception.ParamName);
    }

    /// <summary>
    /// A password for an unencrypted <i>private</i> key is ignored rather than rejected: a caller
    /// holding one cannot know in advance whether a given file needs it.
    /// </summary>
    [Fact]
    public async Task LoadKeyAsync_IgnoresAPasswordForAnUnencryptedPrivateKey()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, password: null, Token);

        LicensingKey restored = await KeyTestData.LoadFromStringAsync(
            _service, document, KeyTestData.Password(), Token);

        Assert.Equal(keys.MLDsa.PrivateKey.Material, restored.Material);
    }

    [Fact]
    public async Task SaveKeyAsync_RejectsAnEmptyPassword()
    {
        using MemoryStream output = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveKeyAsync(keys.MLDsa.PrivateKey, output, [], Token));

        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public async Task LoadKeyAsync_RejectsAnEmptyPassword()
    {
        string document = await KeyTestData.SaveToStringAsync(
            _service, keys.MLDsa.PrivateKey, KeyTestData.Password(), Token);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, document, [], Token));

        Assert.Equal("password", exception.ParamName);
    }

    /// <summary>
    /// The caller owns the password array: the service reads it and clears its own copies, but never
    /// touches the characters it was handed. A UI that reuses a password buffer depends on this.
    /// </summary>
    [Fact]
    public async Task SaveAndLoad_LeaveTheCallersPasswordArrayIntact()
    {
        char[] password = KeyTestData.Password();
        char[] expected = KeyTestData.Password();

        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PrivateKey, password, Token);
        Assert.Equal(expected, password);

        _ = await KeyTestData.LoadFromStringAsync(_service, document, password, Token);
        Assert.Equal(expected, password);
    }

    /// <summary>The password applies to the private file only; the public one is written in the clear.</summary>
    [Fact]
    public async Task SaveKeyPairAsync_EncryptsOnlyThePrivateHalf()
    {
        using TemporaryDirectory directory = new();
        string publicPath = directory.File("app.public.key");
        string privatePath = directory.File("app.private.key");

        await _service.SaveKeyPairAsync(keys.MLDsa, publicPath, privatePath, KeyTestData.Password(), Token);

        string publicDocument = await File.ReadAllTextAsync(publicPath, Token);
        string privateDocument = await File.ReadAllTextAsync(privatePath, Token);

        Assert.Null(KeyTestData.Parse(publicDocument)["encryption"]);
        Assert.NotNull(KeyTestData.Parse(privateDocument)["encryption"]);

        LicensingKey restored = await _service.LoadKeyAsync(privatePath, KeyTestData.Password(), Token);
        Assert.Equal(keys.MLDsa.PrivateKey.Material, restored.Material);
    }
}
