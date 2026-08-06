using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Covers the <c>enigma-key/1</c> document itself: the shape written, what survives a round-trip, and
/// every way a document can be refused.
/// </summary>
[Collection(GeneratedKeyCollection.Name)]
public sealed class KeyStorageTests(GeneratedKeyFixture keys)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly KeyService _service = new();

    [Fact]
    public void CurrentFormat_IsTheDocumentedVersionString() =>
        Assert.Equal("enigma-key/1", LicensingKey.CurrentFormat);

    /// <summary>
    /// The documented field set, in the documented order, under camelCase names — with
    /// <c>encryption</c> absent, because nothing here has a password.
    /// </summary>
    [Fact]
    public async Task SaveKeyAsync_WritesTheDocumentedShape()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.Rsa.PublicKey, password: null, Token);

        Assert.Equal(["format", "kind", "algorithm", "keyId", "key"], KeyTestData.PropertyNames(document));
        Assert.Equal("enigma-key/1", KeyTestData.Field(document, "format"));
        Assert.Equal("public", KeyTestData.Field(document, "kind"));
        Assert.Equal("RSA-SHA256", KeyTestData.Field(document, "algorithm"));
        Assert.Equal(keys.Rsa.PublicKey.KeyId, KeyTestData.Field(document, "keyId"));
    }

    [Fact]
    public async Task SaveKeyAsync_MarksAPrivateKeyAsPrivate()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.Rsa.PrivateKey, password: null, Token);

        Assert.Equal("private", KeyTestData.Field(document, "kind"));
    }

    /// <summary>A key file is something a human opens, so it is written indented like the license envelope.</summary>
    [Fact]
    public async Task SaveKeyAsync_WritesIndentedJson() =>
        Assert.Contains(
            "\n  \"format\"",
            await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token),
            StringComparison.Ordinal);

    /// <summary>RSA material stays PEM text in the file, recognizable for what it is rather than base64.</summary>
    [Fact]
    public async Task SaveKeyAsync_StoresRsaMaterialAsPemText()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.Rsa.PrivateKey, password: null, Token);

        Assert.Equal(keys.Rsa.PrivateKey.Pem, KeyTestData.Field(document, "key"));
    }

    /// <summary>ML-DSA material has no text form, so it is base64 of the raw FIPS 204 encoding.</summary>
    [Fact]
    public async Task SaveKeyAsync_StoresMLDsaMaterialAsBase64()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);

        Assert.Equal(
            Convert.ToBase64String(keys.MLDsa.PublicKey.Material),
            KeyTestData.Field(document, "key"));
    }

    /// <summary>
    /// The material comes back byte for byte. It has to: a key that survives storage <i>almost</i>
    /// intact verifies nothing.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task RoundTrip_PreservesTheMaterialExactly(bool rsa, bool publicHalf)
    {
        LicensingKeyPair pair = rsa ? keys.Rsa : keys.MLDsa;
        LicensingKey original = publicHalf ? pair.PublicKey : pair.PrivateKey;

        LicensingKey restored = await KeyTestData.RoundTripAsync(_service, original, password: null, Token);

        Assert.Equal(original.Material, restored.Material);
        Assert.Equal(original.Kind, restored.Kind);
        Assert.Equal(original.Algorithm, restored.Algorithm);
        Assert.Equal(original.KeyId, restored.KeyId);
    }

    [Fact]
    public async Task SaveKeyAsync_LeavesTheStreamOpen()
    {
        using MemoryStream output = new();

        await _service.SaveKeyAsync(keys.MLDsa.PublicKey, output, password: null, Token);

        Assert.True(output.CanWrite);
        Assert.True(output.Length > 0);
    }

    [Fact]
    public async Task LoadKeyAsync_LeavesTheStreamOpen()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document), writable: false);

        _ = await _service.LoadKeyAsync(input, password: null, Token);

        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData("enigma-key/2")]
    [InlineData("enigma-key/0")]
    [InlineData("ENIGMA-KEY/1")]
    [InlineData("enigma-license/1")]
    [InlineData("")]
    public async Task LoadKeyAsync_RejectsAnUnknownFormat(string format)
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["format"] = format);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));

        Assert.Equal("input", exception.ParamName);
        Assert.Contains(format, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Public")]
    [InlineData("secret")]
    [InlineData("")]
    public async Task LoadKeyAsync_RejectsAnUnknownKind(string kind)
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["kind"] = kind);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));

        Assert.Equal("input", exception.ParamName);
    }

    /// <summary>
    /// An unknown algorithm is fatal <i>here</i>, unlike in a license envelope. A license naming a
    /// scheme this library does not implement still parses, so the validator can report it as a
    /// verdict; there is no verdict to report about a key, only a key that cannot be used.
    /// </summary>
    [Theory]
    [InlineData("RSA-SHA1")]
    [InlineData("rsa-sha256")]
    [InlineData("ML-DSA-99")]
    public async Task LoadKeyAsync_RejectsAnUnknownAlgorithm(string algorithm)
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["algorithm"] = algorithm);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));

        Assert.Equal("input", exception.ParamName);
        Assert.Contains(algorithm, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "empty input")]
    [InlineData("null", "the JSON null literal")]
    [InlineData("not json at all", "not JSON")]
    [InlineData("{", "truncated JSON")]
    [InlineData("[]", "an array rather than an object")]
    [InlineData("{\"format\":\"enigma-key/1\"}", "missing required fields")]
    [InlineData("{\"format\":\"enigma-key/1\",\"kind\":\"public\",\"algorithm\":\"ML-DSA-65\",\"keyId\":\"A\"}", "missing key material")]
    [InlineData("{\"Format\":\"enigma-key/1\",\"Kind\":\"public\",\"Algorithm\":\"ML-DSA-65\",\"KeyId\":\"A\",\"Key\":\"AA==\"}", "PascalCase field names")]
    [InlineData("{\"format\":\"enigma-key/1\",\"kind\":\"public\",\"algorithm\":42,\"keyId\":\"A\",\"key\":\"AA==\"}", "algorithm is not a string")]
    public async Task LoadKeyAsync_ThrowsForADocumentItCannotParse(string json, string reason)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, json, password: null, Token));

        Assert.True(
            exception.ParamName == "input",
            $"{reason}: expected ParamName 'input', got '{exception.ParamName}'.");
    }

    [Fact]
    public async Task LoadKeyAsync_KeepsTheUnderlyingParseFailureAsTheInnerException()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, "{\"format\":", password: null, Token));

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    /// <summary>Base64 that is not base64 is a malformed <i>document</i>, not a cryptographic failure.</summary>
    [Fact]
    public async Task LoadKeyAsync_RejectsMalformedBase64Material()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.MLDsa.PublicKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["key"] = "not base64!!");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));

        Assert.Equal("input", exception.ParamName);
    }

    /// <summary>
    /// Material that decodes but is not a key is a cryptographic failure — the same exception a wrong
    /// password produces, because from the caller's side it is the same situation.
    /// </summary>
    [Fact]
    public async Task LoadKeyAsync_RejectsRsaMaterialThatIsNotPem()
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.Rsa.PublicKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["key"] = "this is not a PEM document");

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadKeyAsync_RejectsEmptyMaterial(bool rsa)
    {
        LicensingKey key = rsa ? keys.Rsa.PublicKey : keys.MLDsa.PublicKey;
        string document = await KeyTestData.SaveToStringAsync(_service, key, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["key"] = "");

        await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));
    }

    /// <summary>
    /// A PEM encrypted at the PEM layer is something this format never writes and cannot open — the
    /// password protection lives at the file level. Rejecting it at load beats a baffling failure later.
    /// </summary>
    [Theory]
    [InlineData("-----BEGIN ENCRYPTED PRIVATE KEY-----\nAAAA\n-----END ENCRYPTED PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nAAAA\n-----END RSA PRIVATE KEY-----")]
    public async Task LoadKeyAsync_RejectsAnRsaPemThatIsItselfEncrypted(string pem)
    {
        string document = await KeyTestData.SaveToStringAsync(_service, keys.Rsa.PrivateKey, password: null, Token);
        string edited = KeyTestData.Edit(document, root => root["key"] = pem);

        CryptographicException exception = await Assert.ThrowsAsync<CryptographicException>(
            () => KeyTestData.LoadFromStringAsync(_service, edited, password: null, Token));

        Assert.Contains("PEM level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveKeyAsync_ThrowsForANullKey()
    {
        using MemoryStream output = new();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveKeyAsync(null!, output, password: null, Token));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public async Task SaveKeyAsync_ThrowsForANullStream()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveKeyAsync(keys.MLDsa.PublicKey, null!, password: null, Token));

        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public async Task LoadKeyAsync_ThrowsForANullStream()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.LoadKeyAsync((Stream)null!, password: null, Token));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public async Task LoadKeyAsync_ThrowsForANullPath()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.LoadKeyAsync((string)null!, password: null, Token));

        Assert.Equal("path", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadKeyAsync_ThrowsForAnEmptyPath(string path)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.LoadKeyAsync(path, password: null, Token));

        Assert.Equal("path", exception.ParamName);
    }

    /// <summary>I/O failures propagate untouched — a missing file is not a validation verdict.</summary>
    [Fact]
    public async Task LoadKeyAsync_PropagatesAMissingFile()
    {
        using TemporaryDirectory directory = new();
        Directory.CreateDirectory(directory.Path);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.LoadKeyAsync(directory.File("absent.key"), password: null, Token));
    }

    /// <summary>Likewise for a missing directory — the load overloads never create one, only the save ones do.</summary>
    [Fact]
    public async Task LoadKeyAsync_PropagatesAMissingDirectory()
    {
        using TemporaryDirectory directory = new();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _service.LoadKeyAsync(directory.File("absent.key"), password: null, Token));
    }

    /// <summary>
    /// The predecessor made every caller create the directory first, and forgetting produced a
    /// <see cref="DirectoryNotFoundException"/> at the end of a key-generation flow. The path overloads
    /// exist to fix exactly that.
    /// </summary>
    [Fact]
    public async Task SaveKeyPairAsync_CreatesMissingParentDirectories()
    {
        using TemporaryDirectory directory = new();
        string publicPath = directory.File("nested", "deeper", "app.public.key");
        string privatePath = directory.File("nested", "deeper", "app.private.key");

        await _service.SaveKeyPairAsync(keys.MLDsa, publicPath, privatePath, password: null, Token);

        Assert.True(File.Exists(publicPath));
        Assert.True(File.Exists(privatePath));
    }

    [Fact]
    public async Task SaveKeyPairAsync_WritesBothHalvesReadableByPath()
    {
        using TemporaryDirectory directory = new();
        string publicPath = directory.File("app.public.key");
        string privatePath = directory.File("app.private.key");

        await _service.SaveKeyPairAsync(keys.Rsa, publicPath, privatePath, password: null, Token);

        LicensingKey publicKey = await _service.LoadKeyAsync(publicPath, password: null, Token);
        LicensingKey privateKey = await _service.LoadKeyAsync(privatePath, password: null, Token);

        Assert.Equal(KeyKind.Public, publicKey.Kind);
        Assert.Equal(KeyKind.Private, privateKey.Kind);
        Assert.Equal(keys.Rsa.PublicKey.Material, publicKey.Material);
        Assert.Equal(keys.Rsa.PrivateKey.Material, privateKey.Material);
        Assert.Equal(publicKey.KeyId, privateKey.KeyId);
    }

    /// <summary>Writing both halves to one path would leave one of them — silently, and possibly the wrong one.</summary>
    [Fact]
    public async Task SaveKeyPairAsync_RejectsWritingBothHalvesToOneFile()
    {
        using TemporaryDirectory directory = new();
        string path = directory.File("both.key");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveKeyPairAsync(keys.MLDsa, path, path, password: null, Token));

        Assert.Equal("privateKeyPath", exception.ParamName);
    }

    [Fact]
    public async Task SaveKeyPairAsync_RejectsSwappedHalves()
    {
        using TemporaryDirectory directory = new();
        LicensingKeyPair swapped = new(keys.MLDsa.PrivateKey, keys.MLDsa.PublicKey);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveKeyPairAsync(
                swapped, directory.File("a.key"), directory.File("b.key"), password: null, Token));

        Assert.Equal("pair", exception.ParamName);
    }

    [Fact]
    public async Task SaveKeyPairAsync_ThrowsForANullPair()
    {
        using TemporaryDirectory directory = new();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveKeyPairAsync(
                null!, directory.File("a.key"), directory.File("b.key"), password: null, Token));

        Assert.Equal("pair", exception.ParamName);
    }

    [Fact]
    public async Task SaveKeyPairAsync_ThrowsForANullPath()
    {
        using TemporaryDirectory directory = new();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveKeyPairAsync(keys.MLDsa, null!, directory.File("b.key"), password: null, Token));

        Assert.Equal("publicKeyPath", exception.ParamName);
    }
}
