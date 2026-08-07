using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// Turns a <see cref="LicenseRequest"/> into a signed <see cref="LicenseEnvelope"/> — the issuing half
/// of the library.
/// </summary>
/// <remarks>
/// <para>
/// This runs where the private key lives: on the issuer's machine, in a build step, or behind a
/// licensing service. It never runs in the application that checks licenses, which only ever needs the
/// public half and <see cref="ILicenseValidator"/>.
/// </para>
/// <para>
/// <b>What the generator signs is what it stores.</b> The license is serialized to UTF-8 JSON once;
/// those bytes are signed, and those same bytes go into the envelope. Nothing recomputes them, so
/// there is no second opinion about what the license "was" for the signature to disagree with.
/// </para>
/// <para>
/// <b>Error contract.</b> <see cref="ArgumentNullException"/> and <see cref="ArgumentException"/>
/// report caller mistakes — a missing product identifier, a public key where a private one belongs, a
/// validity window that ends before it starts. <see cref="CryptographicException"/> reports key
/// material that cannot sign. I/O failures propagate untouched.
/// </para>
/// </remarks>
public interface ILicenseGenerator
{
    /// <summary>
    /// Signs <paramref name="request"/> into an envelope, in memory.
    /// </summary>
    /// <param name="request">What the license should say.</param>
    /// <param name="privateKey">
    /// The private half of the issuing pair. Its algorithm decides the signature scheme, and its
    /// <see cref="LicensingKey.KeyId"/> is stamped into the envelope so a validator can tell which
    /// public key to reach for.
    /// </param>
    /// <returns>The signed envelope, ready to be written or handed on.</returns>
    /// <remarks>
    /// Anything <paramref name="request"/> leaves unset that the license requires is filled in here:
    /// <see cref="LicenseRequest.Id"/> becomes a fresh ULID and <see cref="LicenseRequest.CreatedAt"/>
    /// becomes <see cref="DateTimeOffset.UtcNow"/>. Both are stamped once, at this call, so signing the
    /// same request twice produces two licenses with different identifiers — which is what reissuing
    /// means. Supply them on the request to reproduce a specific license instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> or <paramref name="privateKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="LicenseRequest.ProductId"/> is empty or blank, <see cref="LicenseRequest.Id"/> was
    /// supplied but is blank, <see cref="LicenseRequest.ValidFrom"/> is after
    /// <see cref="LicenseRequest.ValidUntil"/>, or <paramref name="privateKey"/> is the public half of
    /// a pair.
    /// </exception>
    /// <exception cref="CryptographicException">The key material cannot produce a signature.</exception>
    LicenseEnvelope Generate(LicenseRequest request, LicensingKey privateKey);

    /// <summary>
    /// Signs <paramref name="request"/> and writes the envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="request">What the license should say.</param>
    /// <param name="privateKey">The private half of the issuing pair.</param>
    /// <param name="output">The destination stream. It is flushed but never closed, so the caller keeps ownership.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <returns>The envelope that was written, so the caller can read back the license identifier it issued.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="Generate"/>.</exception>
    /// <exception cref="CryptographicException">The key material cannot produce a signature.</exception>
    /// <exception cref="IOException">The stream failed. I/O failures propagate untouched.</exception>
    Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        LicensingKey privateKey,
        Stream output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a private key from a file, signs <paramref name="request"/> with it and writes the
    /// envelope to <paramref name="outputPath"/>, creating any missing parent directories.
    /// </summary>
    /// <param name="request">What the license should say.</param>
    /// <param name="privateKeyPath">The <c>enigma-key/1</c> private key file to sign with.</param>
    /// <param name="password">
    /// The password the key file is encrypted with, or <see langword="null"/> for a key that is not
    /// encrypted. The array is neither modified nor cleared — the caller owns it.
    /// </param>
    /// <param name="outputPath">Where to write the license. An existing file is overwritten.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The envelope that was written.</returns>
    /// <remarks>
    /// The whole issue-a-license flow in one call, including the parent-directory creation that the
    /// predecessor library made every caller remember for itself.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Any argument other than <paramref name="password"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, <paramref name="password"/> is empty, the key file is not a well-formed
    /// <c>enigma-key/1</c> document, or the request is invalid as described on <see cref="Generate"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// The key is encrypted and the password is missing or wrong, the file has been modified, or the
    /// material cannot sign.
    /// </exception>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="privateKeyPath"/>.</exception>
    /// <exception cref="IOException">A file could not be read or written. I/O failures propagate untouched.</exception>
    Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        string privateKeyPath,
        char[]? password,
        string outputPath,
        CancellationToken cancellationToken = default);
}
