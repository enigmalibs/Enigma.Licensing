using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Keys;

/// <summary>
/// Shared plumbing for the key suites: writing a key to a string, reading one back, editing the
/// document in between, and a scratch directory for the path overloads.
/// </summary>
internal static class KeyTestData
{
    /// <summary>The password the encrypted-key tests use.</summary>
    /// <remarks>
    /// A fresh array every time on purpose: the service must never modify or clear what it is handed,
    /// and a shared array would hide a violation behind whichever test ran first.
    /// </remarks>
    internal static char[] Password() => "correct horse battery staple".ToCharArray();

    /// <summary>A different password of the same length, for the wrong-password tests.</summary>
    internal static char[] WrongPassword() => "incorrect horse battery stapl".ToCharArray();

    /// <summary>Writes a key with <see cref="IKeyService.SaveKeyAsync"/> and returns the document text.</summary>
    internal static async Task<string> SaveToStringAsync(
        IKeyService service,
        LicensingKey key,
        char[]? password,
        CancellationToken cancellationToken)
    {
        using MemoryStream output = new();

        await service.SaveKeyAsync(key, output, password, cancellationToken);

        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>Reads a key back from document text.</summary>
    internal static Task<LicensingKey> LoadFromStringAsync(
        IKeyService service,
        string json,
        char[]? password,
        CancellationToken cancellationToken)
    {
        MemoryStream input = new(Encoding.UTF8.GetBytes(json), writable: false);

        return service.LoadKeyAsync(input, password, cancellationToken);
    }

    /// <summary>Round-trips a key through a stream and returns what came back.</summary>
    internal static async Task<LicensingKey> RoundTripAsync(
        IKeyService service,
        LicensingKey key,
        char[]? password,
        CancellationToken cancellationToken)
    {
        string json = await SaveToStringAsync(service, key, password, cancellationToken);

        return await LoadFromStringAsync(service, json, password, cancellationToken);
    }

    /// <summary>Parses a key document so a test can inspect or rewrite one of its fields.</summary>
    internal static JsonObject Parse(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new InvalidOperationException("The key document is not a JSON object.");

    /// <summary>Applies an edit to a key document and returns the modified text.</summary>
    internal static string Edit(string json, Action<JsonObject> edit)
    {
        JsonObject document = Parse(json);

        edit(document);

        return document.ToJsonString();
    }

    /// <summary>Flips the last bit of a base64 field's decoded bytes, leaving it valid base64.</summary>
    internal static string FlipLastByte(string base64)
    {
        byte[] bytes = Convert.FromBase64String(base64);

        bytes[bytes.Length - 1] ^= 0x01;

        return Convert.ToBase64String(bytes);
    }

    /// <summary>Reads the <c>encryption</c> object of a key document.</summary>
    internal static JsonObject Encryption(string json) =>
        Parse(json)["encryption"] as JsonObject
        ?? throw new InvalidOperationException("The key document carries no 'encryption' object.");

    /// <summary>Reads a string field of a key document.</summary>
    internal static string Field(string json, string name) =>
        Parse(json)[name]?.GetValue<string>()
        ?? throw new InvalidOperationException($"The key document has no '{name}' field.");

    /// <summary>
    /// Lists the property names of a key document, in document order — of the root object, or of one
    /// nested object when <paramref name="nested"/> is given.
    /// </summary>
    internal static string[] PropertyNames(string json, string? nested = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement element = nested is null
            ? document.RootElement
            : document.RootElement.GetProperty(nested);

        List<string> names = [];

        foreach (JsonProperty property in element.EnumerateObject())
        {
            names.Add(property.Name);
        }

        return [.. names];
    }
}

/// <summary>
/// A scratch directory that deletes itself, for the tests that exercise the path overloads.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory() =>
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "enigma-licensing-tests",
            Guid.NewGuid().ToString("N"));

    /// <summary>The directory's path. It is deliberately <b>not</b> created — several tests need it absent.</summary>
    internal string Path { get; }

    /// <summary>Builds a path inside the scratch directory.</summary>
    internal string File(params string[] segments) =>
        System.IO.Path.Combine([Path, .. segments]);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
