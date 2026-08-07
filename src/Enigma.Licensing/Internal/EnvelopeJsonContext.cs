using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The source-generated serialization metadata for the two documents that are files in their own
/// right — the <c>enigma-license/1</c> envelope and the <c>enigma-key/1</c> key file.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes the library trim- and AOT-safe.</b> Without it, System.Text.Json builds its
/// metadata by reflecting over the model at runtime, which a consumer publishing with
/// <c>PublishAot</c> — or with <c>JsonSerializerIsReflectionEnabledByDefault=false</c> — does not have.
/// The failure mode was not a degraded document but an <see cref="System.InvalidOperationException"/>
/// on the first serialize, and it would have surfaced in the consumer's app rather than here.
/// </para>
/// <para>
/// <b>There is one context per options set, not one context with two option instances.</b> A
/// <see cref="JsonSerializerContext"/> is bound to the <see cref="JsonSerializerOptions"/> it was
/// created with and rejects being used as a resolver for any other instance, so the indented and
/// compact settings cannot share one. That is why the settings themselves live on
/// <see cref="JsonSourceGenerationOptionsAttribute"/> here and on
/// <see cref="PayloadJsonContext"/> rather than in <see cref="LicenseJson"/> — which now simply hands
/// out <c>Default.Options</c> from each. Matching options exactly is also what lets the generated
/// fast-path writer run instead of the metadata path.
/// </para>
/// <para>
/// Registering the two roots is enough: the generator walks their properties, so
/// <see cref="KeyEncryption"/> and the primitive and collection types beneath them come along without
/// being named. A type that is <i>not</i> reachable from a root has no metadata and throws when
/// serialized — which is the point. The library's whole serialization test suite runs through these
/// contexts, so an unregistered document would fail the build's test run rather than a consumer's.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(LicenseEnvelope))]
[JsonSerializable(typeof(KeyEnvelope))]
internal sealed partial class EnvelopeJsonContext : JsonSerializerContext;
