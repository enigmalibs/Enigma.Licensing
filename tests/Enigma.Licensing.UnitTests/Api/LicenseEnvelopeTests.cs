using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// Covers <see cref="LicenseEnvelope"/> — the on-disk shape of the <c>enigma-license/1</c> format,
/// its stream contract, and the parse failures it is required to throw for.
/// </summary>
public sealed class LicenseEnvelopeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> SaveToStringAsync(LicenseEnvelope envelope)
    {
        using MemoryStream output = new();

        await envelope.SaveAsync(output, Token);

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static Task<LicenseEnvelope> LoadFromStringAsync(string json)
    {
        MemoryStream input = new(Encoding.UTF8.GetBytes(json), writable: false);

        return LicenseEnvelope.LoadAsync(input, Token);
    }

    [Fact]
    public void CurrentFormat_IsTheDocumentedVersionString() =>
        Assert.Equal("enigma-license/1", LicenseEnvelope.CurrentFormat);

    /// <summary>
    /// The five fields, in the documented order, under their camelCase names, with the two blobs
    /// base64-encoded.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WritesTheDocumentedEnvelopeShape()
    {
        LicenseEnvelope envelope = LicenseTestData.FullEnvelope();

        using JsonDocument document = JsonDocument.Parse(await SaveToStringAsync(envelope));

        List<string> names = [];
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            names.Add(property.Name);
        }

        string[] expectedNames = ["format", "algorithm", "keyId", "payload", "signature"];
        Assert.Equal(expectedNames, names);
        Assert.Equal("enigma-license/1", document.RootElement.GetProperty("format").GetString());
        Assert.Equal("RSA-SHA256", document.RootElement.GetProperty("algorithm").GetString());
        Assert.Equal("3F9A1C40D2B87E56", document.RootElement.GetProperty("keyId").GetString());
        Assert.Equal(
            Convert.ToBase64String(envelope.Payload),
            document.RootElement.GetProperty("payload").GetString());
        Assert.Equal(
            Convert.ToBase64String(envelope.Signature),
            document.RootElement.GetProperty("signature").GetString());
    }

    /// <summary>
    /// A license file is something a human opens, so the envelope is written indented.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WritesIndentedJson() =>
        Assert.Contains("\n  \"format\"", await SaveToStringAsync(LicenseTestData.FullEnvelope()), StringComparison.Ordinal);

    [Fact]
    public async Task SaveAsync_OmitsAnAbsentKeyId()
    {
        LicenseEnvelope envelope = LicenseTestData.FullEnvelope() with { KeyId = null };

        using JsonDocument document = JsonDocument.Parse(await SaveToStringAsync(envelope));

        Assert.False(document.RootElement.TryGetProperty("keyId", out _));
    }

    /// <summary>
    /// <b>The invariant the whole format rests on.</b> Whatever bytes went into the envelope come back
    /// out of it unchanged, so the signature made over them can be checked against them. Any
    /// re-encoding here — a stray normalization, a lossy round-trip — would break every license the
    /// library ever signs.
    /// </summary>
    [Fact]
    public async Task RoundTrip_ReturnsThePayloadAndSignatureBytesUnchanged()
    {
        LicenseEnvelope original = LicenseTestData.FullEnvelope() with
        {
            Payload = LicensePayload.Serialize(LicenseTestData.FullLicense()),
            Signature = [0x00, 0xFF, 0x10, 0x7F, 0x80, 0x81, 0x00],
        };

        LicenseEnvelope restored = await LoadFromStringAsync(await SaveToStringAsync(original));

        Assert.Equal(original.Payload, restored.Payload);
        Assert.Equal(original.Signature, restored.Signature);
        Assert.Equal(original.Format, restored.Format);
        Assert.Equal(original.Algorithm, restored.Algorithm);
        Assert.Equal(original.KeyId, restored.KeyId);
    }

    [Fact]
    public async Task RoundTrip_PreservesAnAbsentKeyIdAsNull()
    {
        LicenseEnvelope original = LicenseTestData.FullEnvelope() with { KeyId = null };

        LicenseEnvelope restored = await LoadFromStringAsync(await SaveToStringAsync(original));

        Assert.Null(restored.KeyId);
    }

    /// <summary>
    /// The payload is opaque to the envelope: it stores and returns bytes, including empty ones, and
    /// never inspects them.
    /// </summary>
    [Fact]
    public async Task RoundTrip_HandlesEmptyBlobs()
    {
        LicenseEnvelope original = LicenseTestData.FullEnvelope() with { Payload = [], Signature = [] };

        LicenseEnvelope restored = await LoadFromStringAsync(await SaveToStringAsync(original));

        Assert.Empty(restored.Payload);
        Assert.Empty(restored.Signature);
    }

    [Fact]
    public async Task SaveAsync_LeavesTheStreamOpen()
    {
        using MemoryStream output = new();

        await LicenseTestData.FullEnvelope().SaveAsync(output, Token);

        Assert.True(output.CanWrite);
        Assert.True(output.Length > 0);
    }

    [Fact]
    public async Task LoadAsync_LeavesTheStreamOpen()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(await SaveToStringAsync(LicenseTestData.FullEnvelope())));

        _ = await LicenseEnvelope.LoadAsync(input, Token);

        Assert.True(input.CanRead);
    }

    /// <summary>
    /// A document naming a format version this library does not implement is rejected at the parse,
    /// before anything tries to interpret its fields.
    /// </summary>
    [Theory]
    [InlineData("enigma-license/2")]
    [InlineData("enigma-license/0")]
    [InlineData("ENIGMA-LICENSE/1")]
    [InlineData("enigma-license")]
    [InlineData("")]
    public async Task LoadAsync_RejectsAnUnknownFormatVersion(string format)
    {
        string json = await SaveToStringAsync(LicenseTestData.FullEnvelope() with { Format = format });

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => LoadFromStringAsync(json));

        Assert.Equal("input", exception.ParamName);
        Assert.Contains(format, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The algorithm field, by contrast, is <i>not</i> checked here. A license naming a scheme this
    /// library does not implement still parses; deciding what an unknown algorithm means is the
    /// validator's job, and it cannot report a failure for a document it was never handed.
    /// </summary>
    [Fact]
    public async Task LoadAsync_AcceptsAnUnknownAlgorithm()
    {
        string json = await SaveToStringAsync(LicenseTestData.FullEnvelope() with { Algorithm = "SOMETHING-ELSE" });

        LicenseEnvelope envelope = await LoadFromStringAsync(json);

        Assert.Equal("SOMETHING-ELSE", envelope.Algorithm);
    }

    /// <summary>
    /// Fields the format does not define are ignored rather than rejected — they are outside the
    /// signature either way, so refusing them would buy nothing and would close the door on a future
    /// optional field within the same format version.
    /// </summary>
    [Fact]
    public async Task LoadAsync_IgnoresUnknownFields()
    {
        LicenseEnvelope envelope = await LoadFromStringAsync(
            "{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\",\"issuer\":\"who?\"," +
            "\"payload\":\"e30=\",\"signature\":\"AAE=\"}");

        Assert.Equal("RSA-SHA256", envelope.Algorithm);
    }

    /// <summary>
    /// <see cref="LicenseEnvelope.LoadAsync"/> throws where the validator returns a verdict: a caller
    /// asking a parser for an envelope is entitled to get a structurally sound one or an exception.
    /// The underlying parse failure is kept as the inner exception rather than discarded.
    /// </summary>
    [Theory]
    [InlineData("", "empty input")]
    [InlineData("null", "the JSON null literal")]
    [InlineData("not json at all", "not JSON")]
    [InlineData("{", "truncated JSON")]
    [InlineData("[]", "an array rather than an object")]
    [InlineData("{\"format\":\"enigma-license/1\"}", "missing required fields")]
    [InlineData("{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\",\"payload\":\"e30=\"}", "missing signature")]
    [InlineData("{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\",\"payload\":\"not base64!!\",\"signature\":\"AAE=\"}", "malformed base64 payload")]
    [InlineData("{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\",\"payload\":\"e30=\",\"signature\":\"@@@\"}", "malformed base64 signature")]
    [InlineData("{\"format\":\"enigma-license/1\",\"algorithm\":42,\"payload\":\"e30=\",\"signature\":\"AAE=\"}", "algorithm is not a string")]
    [InlineData("{\"Format\":\"enigma-license/1\",\"Algorithm\":\"RSA-SHA256\",\"Payload\":\"e30=\",\"Signature\":\"AAE=\"}", "PascalCase field names")]
    public async Task LoadAsync_ThrowsForADocumentItCannotParse(string json, string reason)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => LoadFromStringAsync(json));

        Assert.True(
            exception.ParamName == "input",
            $"{reason}: expected ParamName 'input', got '{exception.ParamName}'.");
    }

    /// <summary>
    /// The base64 blobs specifically: a payload that does not decode is a parse failure, and the
    /// <see cref="JsonException"/> that detected it survives as the inner exception.
    /// </summary>
    [Fact]
    public async Task LoadAsync_KeepsTheUnderlyingParseFailureAsTheInnerException()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => LoadFromStringAsync(
                "{\"format\":\"enigma-license/1\",\"algorithm\":\"RSA-SHA256\"," +
                "\"payload\":\"not base64!!\",\"signature\":\"AAE=\"}"));

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsync_ThrowsForANullStream()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => LicenseTestData.FullEnvelope().SaveAsync(null!, Token));

        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public async Task LoadAsync_ThrowsForANullStream()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => LicenseEnvelope.LoadAsync(null!, Token));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void With_ProducesANewInstanceAndLeavesTheOriginalIntact()
    {
        LicenseEnvelope original = LicenseTestData.FullEnvelope();

        LicenseEnvelope altered = original with { Algorithm = "ML-DSA-65", KeyId = null };

        Assert.NotSame(original, altered);
        Assert.Equal("RSA-SHA256", original.Algorithm);
        Assert.Equal("3F9A1C40D2B87E56", original.KeyId);
        Assert.Equal("ML-DSA-65", altered.Algorithm);
        Assert.Null(altered.KeyId);
        Assert.Same(original.Payload, altered.Payload);
    }
}
