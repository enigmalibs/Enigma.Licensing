using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing;

/// <summary>
/// A signed license document in the <c>enigma-license/1</c> format — the thing that is actually
/// written to a <c>.license</c> file, shipped to a customer and read back by an application.
/// </summary>
/// <remarks>
/// <para>
/// The document is a small JSON object wrapping two opaque blobs:
/// </para>
/// <code language="json">
/// {
///   "format": "enigma-license/1",
///   "algorithm": "RSA-SHA256",
///   "keyId": "3F9A1C40D2B87E56",
///   "payload": "eyJpZCI6IjAxSlk...",
///   "signature": "T2xGdVpXNXBaMjFo..."
/// }
/// </code>
/// <para>
/// <b>The signature covers the payload bytes exactly as stored, and nothing recomputes them.</b>
/// <see cref="Payload"/> is the UTF-8 JSON of a <see cref="License"/>, base64 in the file;
/// <see cref="Signature"/> is the signature over precisely those bytes. Verification decodes the
/// base64 and checks the signature against the result — it never re-serializes the license to work
/// out what "should" have been signed. There is deliberately no canonicalization step to get wrong.
/// </para>
/// <para>
/// <b>Algorithm substitution is defeated by validating, not by canonicalizing.</b> Neither
/// <see cref="Algorithm"/> nor <see cref="Format"/> is inside the signed bytes, which invites the
/// obvious question of what stops an attacker rewriting them. The answer is that
/// <see cref="Algorithm"/> is what <i>selects the verifier</i>: change it and the signature is checked
/// under a different scheme, which fails. Downgrading <c>"RSA-SHA512"</c> to <c>"RSA-SHA256"</c> means
/// verifying a SHA-512 PKCS#1 signature as a SHA-256 one, and the padded digest does not match;
/// swapping an RSA identifier for an ML-DSA one means the key that would be needed is not the key the
/// verifier was given. For the ML-DSA members the parameter set is bound to the key material itself,
/// so even the identifier is not load-bearing there. <see cref="Format"/> is checked against the one
/// version this library understands before anything else happens.
/// </para>
/// <para>
/// <see cref="LoadAsync"/> <b>throws</b> on a document it cannot parse — it is a parser, and a caller
/// who asks for an envelope and gets one back is entitled to assume it is structurally sound. That is
/// the opposite of the validator's contract, which never throws for a bad license and returns a
/// failure reason instead; a validator handed a file runs this parse inside its own try/catch.
/// </para>
/// </remarks>
public sealed record LicenseEnvelope
{
    /// <summary>
    /// The only license format version this library reads or writes: <c>"enigma-license/1"</c>.
    /// </summary>
    /// <remarks>
    /// A clean break from Enigma.LicenseManager, which had no format field at all. There is
    /// deliberately no reader for that predecessor's licenses and none will be added — the version
    /// string exists so a <i>future</i> Enigma.Licensing format can be introduced without ambiguity,
    /// not to support the past.
    /// </remarks>
    public const string CurrentFormat = "enigma-license/1";

    /// <summary>
    /// The format version of this document. Always <see cref="CurrentFormat"/> for envelopes this
    /// library produces; anything else is rejected by <see cref="LoadAsync"/>.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// The wire identifier of the signature scheme — <c>"RSA-SHA256"</c>, <c>"ML-DSA-65"</c> and so on.
    /// It is a <see cref="string"/> rather than a <see cref="LicenseSignatureAlgorithm"/> because a
    /// document read from disk may name a scheme this library does not know, and that has to be
    /// representable in order to be reported as a validation failure.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// A fingerprint of the key pair that signed this license, letting an application pick the right
    /// public key when it ships more than one. <see langword="null"/> when the envelope was built
    /// without one.
    /// </summary>
    /// <remarks>
    /// It is an identifier, not a security control: it says which key <i>claims</i> to have signed the
    /// license, and only verifying the signature says whether one actually did.
    /// </remarks>
    public string? KeyId { get; init; }

    /// <summary>
    /// The signed bytes: the UTF-8 JSON of the <see cref="License"/>, stored base64 in the file.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// The signature over <see cref="Payload"/>, stored base64 in the file.
    /// </summary>
    public required byte[] Signature { get; init; }

    /// <summary>
    /// Writes this envelope to <paramref name="output"/> as indented UTF-8 JSON.
    /// </summary>
    /// <param name="output">The destination stream. It is flushed but never closed, so the caller keeps ownership.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <returns>A task that completes when the document has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The stream failed. I/O failures propagate untouched.</exception>
    public async Task SaveAsync(Stream output, CancellationToken cancellationToken = default)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        await JsonSerializer.SerializeAsync(output, this, LicenseJson.Envelope, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads an <c>enigma-license/1</c> envelope from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The source stream. Read to the end but never closed, so the caller keeps ownership.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The parsed envelope.</returns>
    /// <remarks>
    /// <para>
    /// Parsing proves only that the document is well-formed and of a known format version — that the
    /// JSON is valid, the five fields are present and typed correctly, the two base64 blobs decode,
    /// and <c>format</c> is <see cref="CurrentFormat"/>. It says nothing about whether the signature
    /// is good, whether the algorithm is one this library implements, or whether the license inside is
    /// in date. Those are the validator's questions.
    /// </para>
    /// <para>
    /// Properties the format does not define are ignored rather than rejected. They fall outside the
    /// signature either way, so refusing them would protect nothing while ruling out a future optional
    /// field within this same format version.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The stream does not contain a well-formed envelope of a known format version. The underlying
    /// parse failure is preserved as the inner exception.
    /// </exception>
    /// <exception cref="IOException">The stream failed. I/O failures propagate untouched.</exception>
    public static async Task<LicenseEnvelope> LoadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        LicenseEnvelope? envelope;
        try
        {
            envelope = await JsonSerializer
                .DeserializeAsync<LicenseEnvelope>(input, LicenseJson.Envelope, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The stream does not contain a well-formed license envelope.",
                nameof(input), ex);
        }

        if (envelope is null)
        {
            throw new ArgumentException("The stream contains the JSON literal 'null', not a license envelope.",
                nameof(input));
        }

        if (!string.Equals(envelope.Format, CurrentFormat, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported license format '{envelope.Format}'; this library reads '{CurrentFormat}'.",
                nameof(input));
        }

        return envelope;
    }
}
