using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// The default <see cref="ILicenseValidator"/> — the documented check order, on top of Enigma.Core.
/// </summary>
/// <remarks>
/// Stateless and safe to share: it holds only the Enigma.Core verifying services and an
/// <see cref="IKeyService"/> for the overloads that read a key, all of which are themselves stateless.
/// A single instance per application is the intended use, which is why
/// <see cref="Microsoft.Extensions.DependencyInjection.EnigmaLicensingServiceCollectionExtensions.AddEnigmaLicensing"/>
/// registers it as a singleton.
/// </remarks>
public sealed class LicenseValidator : ILicenseValidator
{
    private readonly LicenseSignatures _signatures = new();
    private readonly IKeyService _keyService = new KeyService();

    /// <inheritdoc />
    public LicenseValidationResult Validate(
        LicenseEnvelope envelope,
        LicensingKey publicKey,
        string? productId = null,
        string? deviceId = null)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        RequirePublicKey(publicKey, nameof(publicKey));

        if (!string.Equals(envelope.Format, LicenseEnvelope.CurrentFormat, StringComparison.Ordinal))
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.InvalidFormat,
                $"Unsupported license format '{envelope.Format}'; this library reads " +
                $"'{LicenseEnvelope.CurrentFormat}'.");
        }

        if (!AlgorithmIds.TryParse(envelope.Algorithm, out LicenseSignatureAlgorithm algorithm))
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.UnknownAlgorithm,
                $"The license is signed with '{envelope.Algorithm}', which this library does not " +
                "implement.");
        }

        // The algorithm comes from the envelope, not from the key: a rewritten 'algorithm' field is
        // checked under the scheme the file claims, and fails for exactly that reason.
        if (!_signatures.Verify(algorithm, publicKey, envelope.Payload, envelope.Signature))
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.InvalidSignature,
                $"The signature does not verify under {envelope.Algorithm} with key " +
                $"{publicKey.KeyId}. The license was modified after signing, or it was signed by a " +
                "different key.");
        }

        License license;

        try
        {
            license = LicensePayload.Deserialize(envelope.Payload);
        }
        catch (JsonException ex)
        {
            // Authentic, and still unreadable — a license issued by a newer version of this library,
            // most likely. The signature already proved it was not tampered with.
            return LicenseValidationResult.Invalid(
                LicenseFailure.InvalidFormat,
                $"The license payload is not readable as a license: {ex.Message}");
        }

        return CheckLicense(license, productId, deviceId);
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(
        Stream licenseInput,
        Stream publicKeyInput,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        if (licenseInput is null)
        {
            throw new ArgumentNullException(nameof(licenseInput));
        }

        if (publicKeyInput is null)
        {
            throw new ArgumentNullException(nameof(publicKeyInput));
        }

        // The key is loaded first and its failures propagate: it is the application's own file, so a
        // bad one is a deployment fault and must not be reported as a verdict about the license.
        LicensingKey publicKey = await _keyService
            .LoadKeyAsync(publicKeyInput, password: null, cancellationToken)
            .ConfigureAwait(false);

        RequirePublicKey(publicKey, nameof(publicKeyInput));

        LicenseEnvelope envelope;

        try
        {
            envelope = await LicenseEnvelope.LoadAsync(licenseInput, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return LicenseValidationResult.Invalid(LicenseFailure.InvalidFormat, ex.Message);
        }

        return Validate(envelope, publicKey, productId, deviceId);
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(
        string licensePath,
        string publicKeyPath,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        DocumentFile.RequirePath(licensePath, nameof(licensePath));
        DocumentFile.RequirePath(publicKeyPath, nameof(publicKeyPath));

        LicensingKey publicKey = await _keyService
            .LoadKeyAsync(publicKeyPath, password: null, cancellationToken)
            .ConfigureAwait(false);

        RequirePublicKey(publicKey, nameof(publicKeyPath));

        using FileStream licenseInput = DocumentFile.OpenRead(licensePath);

        LicenseEnvelope envelope;

        try
        {
            envelope = await LicenseEnvelope.LoadAsync(licenseInput, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return LicenseValidationResult.Invalid(LicenseFailure.InvalidFormat, ex.Message);
        }

        return Validate(envelope, publicKey, productId, deviceId);
    }

    /// <summary>
    /// The checks that run on a license whose signature has already been verified: the window, then the
    /// product, then the device.
    /// </summary>
    private static LicenseValidationResult CheckLicense(License license, string? productId, string? deviceId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (license.ValidFrom is { } validFrom && now < validFrom)
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.NotYetValid,
                $"The license is not valid until {validFrom:O}.",
                license);
        }

        // At ValidUntil the license has stopped being usable — the window is half-open, so a license
        // valid "until midnight" is not valid at midnight.
        if (license.ValidUntil is { } validUntil && now >= validUntil)
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.Expired,
                $"The license expired on {validUntil:O}.",
                license);
        }

        if (productId is not null && !ProductIdMatch.Matches(license.ProductId, productId))
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.ProductMismatch,
                $"The license is for '{license.ProductId}', not '{productId}'.",
                license);
        }

        // A license with no DeviceId runs anywhere, so it passes whatever the caller asked about. Only
        // a locked license can mismatch.
        if (deviceId is not null &&
            license.DeviceId is not null &&
            !string.Equals(license.DeviceId, deviceId, StringComparison.Ordinal))
        {
            return LicenseValidationResult.Invalid(
                LicenseFailure.DeviceMismatch,
                "The license is locked to a different device.",
                license);
        }

        return LicenseValidationResult.Valid(license);
    }

    private static void RequirePublicKey(LicensingKey key, string parameterName)
    {
        if (key is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (key.Kind != KeyKind.Public)
        {
            throw new ArgumentException(
                "Verifying needs the public half of the pair. An application that ships the private " +
                "half ships the ability to mint its own licenses.",
                parameterName);
        }
    }
}
