using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// Decides whether a license is authentic and currently applies — the checking half of the library,
/// and the part that runs inside the application being licensed.
/// </summary>
/// <remarks>
/// <para>
/// Stateless: every call is a fresh verdict computed from the arguments and the clock, so the same
/// instance can be shared across threads and a license that expires between two calls is valid in the
/// first and expired in the second.
/// </para>
/// <para>
/// <b>The order of the checks is fixed, and validation stops at the first failure.</b>
/// </para>
/// <list type="number">
/// <item><description>The envelope is well-formed — for the overloads that read one.</description></item>
/// <item><description>Its <c>format</c> is a version this library knows.</description></item>
/// <item><description>Its <c>algorithm</c> is a scheme this library implements.</description></item>
/// <item><description>The signature verifies over the payload bytes, under that algorithm.</description></item>
/// <item><description>The payload deserializes into a license.</description></item>
/// <item><description>The validity window contains <see cref="DateTimeOffset.UtcNow"/>.</description></item>
/// <item><description>The product identifier matches, wildcards applied.</description></item>
/// <item><description>The device identifier matches.</description></item>
/// </list>
/// <para>
/// Authenticity is settled before anything the license <i>says</i> is read, which is why an expired
/// license and a forged one are never confused: nothing past step 4 runs on unverified bytes.
/// </para>
/// <para>
/// <b>Error contract — the license is untrusted input, the key is not.</b> Nothing a license does makes
/// this throw: a malformed document, a broken signature, an unknown algorithm and an expired window all
/// come back as a <see cref="LicenseValidationResult"/> with a <see cref="LicenseFailure"/>. The public
/// key is the opposite — it ships inside the application, so a missing or malformed key file is a
/// deployment mistake and its exceptions propagate, as do I/O failures and a
/// <see cref="ArgumentNullException"/> for a null argument.
/// </para>
/// </remarks>
public interface ILicenseValidator
{
    /// <summary>
    /// Validates an envelope already in memory.
    /// </summary>
    /// <param name="envelope">The license document to check.</param>
    /// <param name="publicKey">
    /// The public half of the pair the license should have been signed with. Its
    /// <see cref="LicensingKey.KeyId"/> can be compared against <see cref="LicenseEnvelope.KeyId"/>
    /// beforehand to pick between several shipped keys — but that field is a hint, and only this call
    /// settles anything.
    /// </param>
    /// <param name="productId">
    /// The product to check the license against, or <see langword="null"/> to skip the check — which
    /// answers "is this license authentic and in date" without asking what it is for.
    /// </param>
    /// <param name="deviceId">
    /// The device to check against, or <see langword="null"/> to skip the check. A license that is not
    /// locked to a device passes either way; see <see cref="DeviceIdentifier"/> for the built-in
    /// identifier.
    /// <para>
    /// Skipping means <b>passing</b>, so an application that locks licenses to devices must pass this
    /// on every call — omitting it makes a device-locked license validate on any machine.
    /// </para>
    /// </param>
    /// <returns>The verdict. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="envelope"/> or <paramref name="publicKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="publicKey"/> is the private half of a pair. Verifying with a private key is a
    /// programming mistake rather than a verdict about the license, and letting it through would mean
    /// an application that verifies correctly while shipping the key that mints licenses.
    /// </exception>
    LicenseValidationResult Validate(
        LicenseEnvelope envelope,
        LicensingKey publicKey,
        string? productId = null,
        string? deviceId = null);

    /// <summary>
    /// Reads a license and a public key from streams and validates the one against the other.
    /// </summary>
    /// <param name="licenseInput">The license document. Read to the end but never closed.</param>
    /// <param name="publicKeyInput">The <c>enigma-key/1</c> public key document. Read to the end but never closed.</param>
    /// <param name="productId">The product to check against, or <see langword="null"/> to skip the check.</param>
    /// <param name="deviceId">The device to check against, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to cancel the reads.</param>
    /// <returns>
    /// The verdict. A license stream that cannot be parsed yields
    /// <see cref="LicenseFailure.InvalidFormat"/> rather than an exception.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either stream is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="publicKeyInput"/> is not a well-formed key document, or holds a private key.
    /// </exception>
    /// <exception cref="CryptographicException">The key material is not usable.</exception>
    /// <exception cref="IOException">A stream failed. I/O failures propagate untouched.</exception>
    Task<LicenseValidationResult> ValidateAsync(
        Stream licenseInput,
        Stream publicKeyInput,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a license and a public key from files and validates the one against the other.
    /// </summary>
    /// <param name="licensePath">The license file.</param>
    /// <param name="publicKeyPath">The <c>enigma-key/1</c> public key file.</param>
    /// <param name="productId">The product to check against, or <see langword="null"/> to skip the check.</param>
    /// <param name="deviceId">The device to check against, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to cancel the reads.</param>
    /// <returns>The verdict.</returns>
    /// <remarks>
    /// A <i>missing</i> license file is an I/O failure and throws — the license was never read, so
    /// there is nothing to return a verdict about. Only a file that exists and cannot be understood
    /// becomes <see cref="LicenseFailure.InvalidFormat"/>. Applications that treat "no license
    /// installed" as a normal state should check for the file first.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either path is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, or <paramref name="publicKeyPath"/> is not a well-formed key file, or holds a
    /// private key.
    /// </exception>
    /// <exception cref="CryptographicException">The key material is not usable.</exception>
    /// <exception cref="FileNotFoundException">Either file is missing.</exception>
    /// <exception cref="IOException">A file could not be read. I/O failures propagate untouched.</exception>
    Task<LicenseValidationResult> ValidateAsync(
        string licensePath,
        string publicKeyPath,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default);
}
