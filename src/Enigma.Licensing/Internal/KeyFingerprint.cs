using System;
using System.IO;
using System.Text;
using Enigma.Core.Encoding;
using Enigma.Core.Hashing.Hash;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Computes the <c>keyId</c> that identifies a key pair: the uppercase hex of the first
/// <see cref="KeyFormat.KeyIdSizeBytes"/> bytes of SHA-256 over the algorithm identifier followed by
/// the <b>public</b> key material.
/// </summary>
/// <remarks>
/// <para>
/// Computing it from the public half is what makes both files of a pair carry the same value — a
/// private key file cannot derive its own public material through this library's API, so the value is
/// calculated once at generation and written into both files.
/// </para>
/// <para>
/// The algorithm identifier is included so that two different schemes cannot produce the same
/// fingerprint from coincidentally equal material. The material is hashed <b>exactly as stored</b>,
/// with no normalization: a PEM's line endings are whatever the platform that generated it wrote, and
/// they survive the JSON round-trip unchanged, so the fingerprint of a given key file is stable
/// wherever it is read. The same refusal to canonicalize as the license payload, for the same reason —
/// a second opinion about what the bytes "really are" is a bug waiting to happen.
/// </para>
/// </remarks>
internal static class KeyFingerprint
{
    // Both services are stateless: HashService builds a fresh digest per call and rents its own
    // buffer, and the hex encoder holds nothing at all. Sharing one instance each is therefore safe
    // across threads and avoids re-creating them per key.
    private static readonly IHashService Sha256 = new HashServiceFactory().CreateSha256Service();
    private static readonly IEncodingService Hex = new EncodingServiceFactory().CreateHexService();

    /// <summary>
    /// Computes the fingerprint of a key pair from its public material.
    /// </summary>
    /// <param name="algorithm">The pair's algorithm. Must be a defined member.</param>
    /// <param name="publicMaterial">The public key material, exactly as it will be stored.</param>
    /// <returns>Sixteen uppercase hex digits.</returns>
    internal static string Compute(LicenseSignatureAlgorithm algorithm, byte[] publicMaterial)
    {
        string identifier = AlgorithmIds.ToIdentifier(algorithm)
            ?? throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm,
                "The algorithm is not a defined LicenseSignatureAlgorithm member.");

        byte[] identifierBytes = Encoding.UTF8.GetBytes(identifier);
        byte[] input = new byte[identifierBytes.Length + publicMaterial.Length];
        Buffer.BlockCopy(identifierBytes, 0, input, 0, identifierBytes.Length);
        Buffer.BlockCopy(publicMaterial, 0, input, identifierBytes.Length, publicMaterial.Length);

        byte[] digest = ComputeSha256(input);

        byte[] head = new byte[KeyFormat.KeyIdSizeBytes];
        Buffer.BlockCopy(digest, 0, head, 0, head.Length);

        // Uppercase because the field is read by humans comparing it against a license envelope's
        // keyId; Enigma.Core's hex encoder emits lowercase.
        return Hex.Encode(head).ToUpperInvariant();
    }

    // Enigma.Core hashes streams, and this one is a MemoryStream over a buffer that is already in
    // memory: every ReadAsync it performs completes synchronously, so the state machine runs to the
    // end without ever yielding and GetResult() returns an already-finished task's value rather than
    // blocking a thread. Worth the one sync-over-async seam — the alternative is either a second
    // SHA-256 implementation outside Enigma.Core or an async GenerateKeyPair, and neither is an
    // improvement.
    private static byte[] ComputeSha256(byte[] input)
    {
        using MemoryStream stream = new(input, writable: false);

        return Sha256.ComputeHashAsync(stream).GetAwaiter().GetResult();
    }
}
