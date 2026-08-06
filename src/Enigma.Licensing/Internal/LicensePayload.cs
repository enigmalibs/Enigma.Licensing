using System;
using System.Text.Json;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Converts a <see cref="License"/> to and from the UTF-8 JSON bytes that a
/// <see cref="LicenseEnvelope"/> carries as its payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>These bytes are the signed artefact, and there is no canonical form of them.</b> The generator
/// signs whatever <see cref="Serialize"/> produced and stores those same bytes in the envelope; the
/// validator verifies the signature against the bytes it found there and only then hands them to
/// <see cref="Deserialize"/>. Nothing re-serializes a license in order to check it.
/// </para>
/// <para>
/// That is the whole point of the design. The predecessor library built a string out of the license
/// fields and signed that, which meant signing and verifying each had their own idea of what the
/// license "was" — and a v1.0.0 bug where the two disagreed. Signing the stored bytes removes the
/// second idea entirely, so this codec is free to change how it formats JSON without invalidating a
/// single already-signed license.
/// </para>
/// <para>
/// Failures surface as <see cref="JsonException"/>. This is an internal seam, so it does not apply
/// the public error contract itself: <see cref="LicenseEnvelope"/> translates the exception for
/// callers who parse a document directly, and the validator maps it to a failure reason.
/// </para>
/// </remarks>
internal static class LicensePayload
{
    /// <summary>
    /// Serializes <paramref name="license"/> to the UTF-8 JSON bytes to be signed and stored.
    /// </summary>
    /// <param name="license">The license to encode.</param>
    /// <returns>The payload bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is <see langword="null"/>.</exception>
    internal static byte[] Serialize(License license)
    {
        if (license is null)
        {
            throw new ArgumentNullException(nameof(license));
        }

        return JsonSerializer.SerializeToUtf8Bytes(license, LicenseJson.Payload);
    }

    /// <summary>
    /// Decodes payload bytes recovered from an envelope back into a <see cref="License"/>.
    /// </summary>
    /// <param name="payload">The UTF-8 JSON bytes the envelope carried.</param>
    /// <returns>The decoded license.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">
    /// The bytes are not valid JSON, are the literal <c>null</c>, or describe an object missing one of
    /// the license's required properties.
    /// </exception>
    internal static License Deserialize(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        License? license = JsonSerializer.Deserialize<License>(payload, LicenseJson.Payload);

        return license ?? throw new JsonException("The license payload is the JSON literal 'null'.");
    }
}
