using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// Shared plumbing for the signing suites: building requests with a validity window relative to now,
/// writing an envelope to text, reading one back, and editing the document in between.
/// </summary>
/// <remarks>
/// Windows are built from <see cref="DateTimeOffset.UtcNow"/> on purpose — unlike the model and codec
/// suites, which pin serialized output and therefore need fixed dates. Validation judges against the
/// real clock and there is no clock to substitute, so a test that wants an expired license has to craft
/// one that really has expired.
/// </remarks>
internal static class SignedLicenseTestData
{
    /// <summary>The product identifier used wherever the test is not about product matching.</summary>
    internal const string ProductId = "MyApp";

    /// <summary>A request for a perpetual license with no device lock.</summary>
    internal static LicenseRequest Request() => new() { ProductId = ProductId };

    /// <summary>A request whose window has already closed.</summary>
    internal static LicenseRequest Expired() => Request() with
    {
        ValidFrom = DateTimeOffset.UtcNow.AddDays(-10),
        ValidUntil = DateTimeOffset.UtcNow.AddDays(-1),
    };

    /// <summary>A request whose window has not opened yet.</summary>
    internal static LicenseRequest NotYetValid() => Request() with
    {
        ValidFrom = DateTimeOffset.UtcNow.AddDays(1),
        ValidUntil = DateTimeOffset.UtcNow.AddDays(10),
    };

    /// <summary>A request whose window is open right now, with both bounds set.</summary>
    internal static LicenseRequest InWindow() => Request() with
    {
        ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
        ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
    };

    /// <summary>Writes an envelope with <see cref="LicenseEnvelope.SaveAsync"/> and returns the document text.</summary>
    internal static async Task<string> SaveToStringAsync(
        LicenseEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using MemoryStream output = new();

        await envelope.SaveAsync(output, cancellationToken);

        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>A readable stream over document text.</summary>
    internal static MemoryStream StreamOf(string text) =>
        new(Encoding.UTF8.GetBytes(text), writable: false);

    /// <summary>Parses an envelope document so a test can inspect or rewrite one of its fields.</summary>
    internal static JsonObject Parse(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new InvalidOperationException("The license document is not a JSON object.");

    /// <summary>Applies an edit to an envelope document and returns the modified text.</summary>
    internal static string Edit(string json, Action<JsonObject> edit)
    {
        JsonObject document = Parse(json);

        edit(document);

        return document.ToJsonString();
    }

    /// <summary>Reads a string field of an envelope document.</summary>
    internal static string Field(string json, string name) =>
        Parse(json)[name]?.GetValue<string>()
        ?? throw new InvalidOperationException($"The license document has no '{name}' field.");

    /// <summary>Flips the last bit of a byte array, leaving its length alone.</summary>
    internal static byte[] FlipLastBit(byte[] bytes)
    {
        byte[] copy = (byte[])bytes.Clone();

        copy[copy.Length - 1] ^= 0x01;

        return copy;
    }
}
