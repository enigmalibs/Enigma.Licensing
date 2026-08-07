using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The source-generated serialization metadata for the signed license payload.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="EnvelopeJsonContext"/> only because the payload is written compact while
/// the documents a human opens are written indented, and a <see cref="JsonSerializerContext"/> cannot
/// serve two different <see cref="JsonSerializerOptions"/> instances. Everything else about the two is
/// identical, and deliberately so — the payload's JSON settings are the envelope's.
/// </para>
/// <para>
/// Indentation cannot affect verification either way: the signature covers the payload bytes exactly
/// as they are stored, so whatever this context produced is what is checked. Switching to source
/// generation does not change a single already-signed license for the same reason — nothing
/// re-serializes a license in order to verify one.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(License))]
internal sealed partial class PayloadJsonContext : JsonSerializerContext;
