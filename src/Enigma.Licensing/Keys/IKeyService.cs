using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.Keys;

/// <summary>
/// Creates the key pairs that sign licenses, and reads and writes them as <c>enigma-key/1</c> files.
/// </summary>
/// <remarks>
/// <para>
/// An issuer generates a pair once, keeps the private half — encrypted, ideally — and ships the public
/// half inside the application that will check licenses. Everything else in this library takes keys
/// from here: the generator signs with the private half, the validator verifies with the public one.
/// </para>
/// <para>
/// <b>Passwords are <see cref="char"/> arrays, and the caller owns them.</b> Nothing here modifies or
/// clears the array it is given; the copies made along the way — the UTF-8 encoding, the derived AES
/// key — are cleared internally, but the characters you passed in remain yours to overwrite when you
/// are done. That is the Enigma.Core convention, and it exists because the caller is the only one who
/// knows when the password stops being needed.
/// </para>
/// <para>
/// <b>Error contract.</b> <see cref="ArgumentNullException"/> and <see cref="ArgumentException"/>
/// report caller mistakes, including a document that is not a well-formed key file of a known format.
/// <see cref="CryptographicException"/> reports the cryptographic failures: a wrong or missing
/// password, a modified file, key material that is not usable. I/O failures propagate untouched.
/// </para>
/// </remarks>
public interface IKeyService
{
    /// <summary>
    /// Generates a fresh key pair for <paramref name="algorithm"/>.
    /// </summary>
    /// <param name="algorithm">
    /// The signature scheme the pair is for. This is the choice that decides what the key files
    /// contain, and it cannot be changed afterwards — a key belongs to one scheme for life.
    /// </param>
    /// <param name="rsaKeySizeBits">
    /// The RSA modulus size in bits, between 2048 and 8192. Defaults to 3072 — roughly 128-bit
    /// security, and what NIST expects of keys still in use after 2030. <b>Ignored</b> for the ML-DSA
    /// members, whose key size is fixed by the parameter set the algorithm names.
    /// </param>
    /// <returns>The new pair. Both halves carry the same <see cref="LicensingKey.KeyId"/>.</returns>
    /// <remarks>
    /// RSA key generation is a search for primes and takes noticeably longer as the modulus grows —
    /// seconds at 4096 bits and up, on a UI thread long enough to matter. The ML-DSA members are fast
    /// at every parameter set.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="algorithm"/> is not a defined <see cref="LicenseSignatureAlgorithm"/> member, or
    /// <paramref name="rsaKeySizeBits"/> is outside the accepted range for an RSA algorithm.
    /// </exception>
    LicensingKeyPair GenerateKeyPair(
        LicenseSignatureAlgorithm algorithm,
        int rsaKeySizeBits = KeyFormat.DefaultRsaKeySizeBits);

    /// <summary>
    /// Writes <paramref name="key"/> to <paramref name="output"/> as an <c>enigma-key/1</c> document.
    /// </summary>
    /// <param name="key">The key to write.</param>
    /// <param name="output">The destination stream. It is flushed but never closed, so the caller keeps ownership.</param>
    /// <param name="password">
    /// A password to encrypt the key material with (Argon2id + AES-256-GCM), or <see langword="null"/>
    /// to write it in the clear. Only valid for a private key: a public key is meant to be distributed
    /// and is never encrypted.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <returns>A task that completes when the document has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="password"/> is empty, or was supplied for a public key.
    /// </exception>
    /// <exception cref="IOException">The stream failed. I/O failures propagate untouched.</exception>
    Task SaveKeyAsync(
        LicensingKey key,
        Stream output,
        char[]? password = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an <c>enigma-key/1</c> document from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The source stream. Read to the end but never closed, so the caller keeps ownership.</param>
    /// <param name="password">
    /// The password the private key was encrypted with, or <see langword="null"/> for a key that is not
    /// encrypted. Supplying one for a public key is an error; supplying one for an unencrypted private
    /// key is ignored.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The key, ready to sign or verify with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The stream does not contain a well-formed key file of a known format, kind and algorithm; or
    /// <paramref name="password"/> is empty or was supplied for a public key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// The key is encrypted and no password was given, the password is wrong, the file has been
    /// modified, or the recovered material is not usable key material.
    /// </exception>
    /// <exception cref="IOException">The stream failed. I/O failures propagate untouched.</exception>
    Task<LicensingKey> LoadKeyAsync(
        Stream input,
        char[]? password = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes both halves of <paramref name="pair"/> to their own files, creating any missing parent
    /// directories.
    /// </summary>
    /// <param name="pair">The pair to write.</param>
    /// <param name="publicKeyPath">Where to write the public half. Always written unencrypted.</param>
    /// <param name="privateKeyPath">Where to write the private half.</param>
    /// <param name="password">
    /// A password to encrypt the private half with, or <see langword="null"/> to write it in the clear.
    /// It is never applied to the public file.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <returns>A task that completes when both files have been written.</returns>
    /// <remarks>
    /// Existing files are overwritten. The public half is written first, so a failure while writing the
    /// private half leaves a public key file behind — harmless on its own, and the pair is regenerated
    /// rather than repaired.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument other than <paramref name="password"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, both paths point at the same file, <paramref name="password"/> is empty, or the
    /// pair's halves are not one public and one private key.
    /// </exception>
    /// <exception cref="IOException">A file could not be written. I/O failures propagate untouched.</exception>
    Task SaveKeyPairAsync(
        LicensingKeyPair pair,
        string publicKeyPath,
        string privateKeyPath,
        char[]? password = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an <c>enigma-key/1</c> document from a file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="password">
    /// The password the private key was encrypted with, or <see langword="null"/> for a key that is not
    /// encrypted.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The key, ready to sign or verify with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty, the file does not contain a well-formed key document, or
    /// <paramref name="password"/> is empty or was supplied for a public key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// The key is encrypted and no password was given, the password is wrong, the file has been
    /// modified, or the recovered material is not usable key material.
    /// </exception>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="path"/>.</exception>
    /// <exception cref="IOException">The file could not be read. I/O failures propagate untouched.</exception>
    Task<LicensingKey> LoadKeyAsync(
        string path,
        char[]? password = null,
        CancellationToken cancellationToken = default);
}
