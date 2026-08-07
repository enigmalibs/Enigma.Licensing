using System;
using System.Collections.Generic;
using System.Text.Json;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// The default <see cref="ILicenseStore"/> — an in-memory registry guarded by a lock, re-validating
/// through an <see cref="ILicenseValidator"/> on every query.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lock covers the registry, not the cryptography.</b> A query copies the entries under the
/// lock and then verifies signatures outside it, so a slow verification never blocks another thread
/// from adding or removing a license. The cost is that a query sees the registry as it was when the
/// query started: a license added part-way through a <see cref="FindValidLicense"/> call may or may not
/// be considered. That is the right trade for a registry that holds a handful of licenses and is
/// written to at start-up.
/// </para>
/// <para>
/// A list rather than a dictionary, because insertion order is part of the contract —
/// <see cref="FindValidLicense"/> returns the first match — and because a store holding enough licenses
/// for the difference to matter would be doing something this type is not for.
/// </para>
/// </remarks>
public sealed class LicenseStore : ILicenseStore
{
    private readonly object _gate = new();
    private readonly List<StoredLicense> _licenses = [];
    private readonly ILicenseValidator _validator;

    /// <summary>
    /// Creates a store that validates with a new <see cref="LicenseValidator"/>.
    /// </summary>
    public LicenseStore()
        : this(new LicenseValidator())
    {
    }

    /// <summary>
    /// Creates a store that validates with <paramref name="validator"/>.
    /// </summary>
    /// <param name="validator">The validator every query goes through.</param>
    /// <remarks>
    /// The constructor dependency injection resolves. It exists because the store is the one service
    /// here that genuinely composes with another rather than wrapping Enigma.Core directly.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="validator"/> is <see langword="null"/>.</exception>
    public LicenseStore(ILicenseValidator validator) =>
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    /// <inheritdoc />
    public void Add(LicenseEnvelope envelope, LicensingKey publicKey)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        if (publicKey is null)
        {
            throw new ArgumentNullException(nameof(publicKey));
        }

        if (publicKey.Kind != KeyKind.Public)
        {
            throw new ArgumentException(
                "A store validates licenses, so it takes the public half of the pair.",
                nameof(publicKey));
        }

        // Read only far enough to file the license: the identifier. Whether it is authentic, in date or
        // for this product is a question for the queries, asked afresh each time.
        string licenseId;

        try
        {
            licenseId = LicensePayload.Deserialize(envelope.Payload).Id;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                "The envelope's payload is not a readable license, so it has no identifier to store it " +
                "under.",
                nameof(envelope),
                ex);
        }

        StoredLicense entry = new(licenseId, envelope, publicKey);

        lock (_gate)
        {
            int existing = IndexOf(licenseId);

            if (existing < 0)
            {
                _licenses.Add(entry);
            }
            else
            {
                // Replace in place, so a reissued license keeps its position and FindValidLicense stays
                // predictable across a renewal.
                _licenses[existing] = entry;
            }
        }
    }

    /// <inheritdoc />
    public bool Remove(string licenseId)
    {
        if (licenseId is null)
        {
            throw new ArgumentNullException(nameof(licenseId));
        }

        lock (_gate)
        {
            int existing = IndexOf(licenseId);

            if (existing < 0)
            {
                return false;
            }

            _licenses.RemoveAt(existing);

            return true;
        }
    }

    /// <inheritdoc />
    public bool HasValidLicense(string productId, string? deviceId = null) =>
        FindValidLicense(productId, deviceId) is not null;

    /// <inheritdoc />
    public License? FindValidLicense(string productId, string? deviceId = null)
    {
        if (productId is null)
        {
            throw new ArgumentNullException(nameof(productId));
        }

        StoredLicense[] snapshot;

        lock (_gate)
        {
            snapshot = [.. _licenses];
        }

        foreach (StoredLicense stored in snapshot)
        {
            LicenseValidationResult result = _validator.Validate(
                stored.Envelope, stored.PublicKey, productId, deviceId);

            if (result.IsValid)
            {
                return result.License;
            }
        }

        return null;
    }

    // Callers hold _gate.
    private int IndexOf(string licenseId)
    {
        for (int index = 0; index < _licenses.Count; index++)
        {
            if (string.Equals(_licenses[index].Id, licenseId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>One registry entry: a license, the key that vouches for it, and the id it is filed under.</summary>
    private sealed record StoredLicense(string Id, LicenseEnvelope Envelope, LicensingKey PublicKey);
}
