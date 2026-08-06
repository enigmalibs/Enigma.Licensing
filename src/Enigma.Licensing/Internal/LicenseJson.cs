using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The System.Text.Json configuration behind every document this library reads or writes.
/// </summary>
/// <remarks>
/// <para>
/// One place, two instances, differing only in whitespace. The settings themselves are what the
/// <c>enigma-license/1</c> and <c>enigma-key/1</c> formats specify — they share them, so the two file
/// types read and write alike: camelCase property names, and optional fields omitted rather than
/// written as <c>null</c>, so an absent value and an explicit null are the same document.
/// </para>
/// <para>
/// <b>Everything else is left strict on purpose.</b> Case-insensitive property matching, comments and
/// trailing commas all stay off: these documents are machine-written and signature-protected, so
/// leniency buys nothing and quietly widens what counts as a conforming file.
/// </para>
/// <para>
/// <see cref="Payload"/> writes compact and <see cref="Envelope"/> writes indented. Indentation is
/// cosmetic in both cases and can never affect verification — the signature covers the payload bytes
/// exactly as they are stored in the envelope, so whatever those bytes are is what is checked. The
/// payload is compact because it is base64-embedded and nobody reads it in place; the envelope is
/// indented because it is the file a human opens.
/// </para>
/// </remarks>
internal static class LicenseJson
{
    /// <summary>
    /// Options for the documents that are files in their own right — the license envelope and the key
    /// file. Indented, because a human opens them.
    /// </summary>
    internal static readonly JsonSerializerOptions Envelope = CreateOptions(writeIndented: true);

    /// <summary>Options for the signed license payload — compact, since it is stored as base64.</summary>
    internal static readonly JsonSerializerOptions Payload = CreateOptions(writeIndented: false);

    private static JsonSerializerOptions CreateOptions(bool writeIndented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = writeIndented,
    };
}
