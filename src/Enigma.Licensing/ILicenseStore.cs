using System;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// A thread-safe registry of the licenses an application currently holds, answering "am I licensed for
/// this?" without the caller keeping track of envelopes and keys.
/// </summary>
/// <remarks>
/// <para>
/// The intended shape is: load whatever licenses were installed at start-up, <see cref="Add"/> each
/// with the public key it should be checked against, and from then on ask <see cref="HasValidLicense"/>
/// wherever a feature needs gating.
/// </para>
/// <para>
/// <b>Every query re-validates.</b> Nothing is cached — not the verdict, not the parsed license. That
/// costs a signature verification per query and buys the only behaviour that is actually correct: a
/// license expires while the application is running, and the very next query says so. A store that
/// validated at <see cref="Add"/> time would keep answering yes for as long as the process lived.
/// </para>
/// <para>
/// <b>Adding a license asserts nothing about it.</b> <see cref="Add"/> only needs to read the license
/// identifier out of the payload, so a license that is expired, unsigned or signed by the wrong key is
/// accepted and then fails every query. That keeps installing a license and judging one as separate
/// concerns — a caller who wants to know whether an incoming license is any good asks
/// <see cref="ILicenseValidator"/>, which reports why.
/// </para>
/// </remarks>
public interface ILicenseStore
{
    /// <summary>
    /// Adds a license to the registry, together with the public key it must be validated against.
    /// </summary>
    /// <param name="envelope">The license document.</param>
    /// <param name="publicKey">The public key this license is checked with.</param>
    /// <remarks>
    /// Keyed by the license's own <see cref="License.Id"/>: adding a license whose identifier is
    /// already present <b>replaces</b> the existing entry, which is what reissuing a license under its
    /// original identifier should do.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="envelope"/> or <paramref name="publicKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The envelope's payload is not a readable license — there is no identifier to file it under — or
    /// <paramref name="publicKey"/> is the private half of a pair.
    /// </exception>
    void Add(LicenseEnvelope envelope, LicensingKey publicKey);

    /// <summary>
    /// Removes the license with the given identifier.
    /// </summary>
    /// <param name="licenseId">The <see cref="License.Id"/> to remove. Compared ordinally.</param>
    /// <returns><see langword="true"/> when a license was removed; <see langword="false"/> when none matched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="licenseId"/> is <see langword="null"/>.</exception>
    bool Remove(string licenseId);

    /// <summary>
    /// Determines whether any stored license currently validates for the given product and device.
    /// </summary>
    /// <param name="productId">The product to ask about. Wildcards in the stored licenses are applied.</param>
    /// <param name="deviceId">
    /// The device to ask about, or <see langword="null"/> to skip the device check — which lets a
    /// device-locked license answer yes anywhere.
    /// </param>
    /// <returns><see langword="true"/> when at least one stored license passes every check right now.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="productId"/> is <see langword="null"/>.</exception>
    bool HasValidLicense(string productId, string? deviceId = null);

    /// <summary>
    /// Returns the first stored license that currently validates for the given product and device.
    /// </summary>
    /// <param name="productId">The product to ask about. Wildcards in the stored licenses are applied.</param>
    /// <param name="deviceId">The device to ask about, or <see langword="null"/> to skip the device check.</param>
    /// <returns>
    /// The valid license — from which the caller reads <see cref="License.Owner"/> and the rest — or
    /// <see langword="null"/> when none applies.
    /// </returns>
    /// <remarks>
    /// "First" is in the order the licenses were added, so the result is stable when more than one
    /// applies. An application that has to pick between overlapping licenses on some other basis should
    /// hold them itself and use <see cref="ILicenseValidator"/> directly.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="productId"/> is <see langword="null"/>.</exception>
    License? FindValidLicense(string productId, string? deviceId = null);
}
