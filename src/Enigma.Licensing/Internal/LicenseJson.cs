using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The System.Text.Json configuration behind every document this library reads or writes.
/// </summary>
/// <remarks>
/// <para>
/// One place, three entries, drawn from two source-generated contexts. The settings themselves are
/// what the <c>enigma-license/1</c> and <c>enigma-key/1</c> formats specify — they share them, so the
/// two file types read and write alike: camelCase property names, and optional fields omitted rather
/// than written as <c>null</c>, so an absent value and an explicit null are the same document.
/// </para>
/// <para>
/// <b>Everything else is left strict on purpose.</b> Case-insensitive property matching, comments and
/// trailing commas all stay off: these documents are machine-written and signature-protected, so
/// leniency buys nothing and quietly widens what counts as a conforming file.
/// </para>
/// <para>
/// <see cref="Payload"/> writes compact and <see cref="Envelope"/> and <see cref="KeyFile"/> write
/// indented. Indentation is cosmetic in every case and can never affect verification — the signature
/// covers the payload bytes exactly as they are stored in the envelope, so whatever those bytes are is
/// what is checked. The payload is compact because it is base64-embedded and nobody reads it in place;
/// the other two are indented because they are the files a human opens.
/// </para>
/// <para>
/// <b>These are <see cref="JsonTypeInfo{T}"/> instances rather than
/// <see cref="JsonSerializerOptions"/>, and that is what makes the library trim- and AOT-safe.</b>
/// Handing <see cref="JsonSerializer"/> a bare options object is not statically analyzable even when
/// the options carry a source-generated resolver, so those overloads are attributed
/// <c>[RequiresUnreferencedCode]</c> and <c>[RequiresDynamicCode]</c>; the typed overloads below carry
/// neither. With <c>IsAotCompatible</c> set on the modern target frameworks, reaching for
/// an options overload anywhere in this library is a build error rather than a consumer's runtime
/// failure. The settings live on <see cref="EnvelopeJsonContext"/> and <see cref="PayloadJsonContext"/>
/// — one context per options set, because a context is bound to the options it was built with.
/// </para>
/// </remarks>
internal static class LicenseJson
{
    /// <summary>The license envelope — a file in its own right, so indented, because a human opens it.</summary>
    internal static readonly JsonTypeInfo<LicenseEnvelope> Envelope = EnvelopeJsonContext.Default.LicenseEnvelope;

    /// <summary>The key file, on exactly the envelope's terms and from the same context.</summary>
    internal static readonly JsonTypeInfo<KeyEnvelope> KeyFile = EnvelopeJsonContext.Default.KeyEnvelope;

    /// <summary>The signed license payload — compact, since it is stored as base64.</summary>
    internal static readonly JsonTypeInfo<License> Payload = PayloadJsonContext.Default.License;
}
