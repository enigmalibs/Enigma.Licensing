using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing;

/// <summary>
/// The default <see cref="ILicenseGenerator"/> — signs a request into an <c>enigma-license/1</c>
/// envelope using Enigma.Core.
/// </summary>
/// <remarks>
/// Stateless and safe to share: it holds only the Enigma.Core signing services and an
/// <see cref="IKeyService"/> for the path overload, all of which are themselves stateless. A single
/// instance per application is the intended use, which is why the DI registration makes it a singleton.
/// </remarks>
public sealed class LicenseGenerator : ILicenseGenerator
{
    private readonly LicenseSignatures _signatures = new();
    private readonly IKeyService _keyService = new KeyService();

    /// <inheritdoc />
    public LicenseEnvelope Generate(LicenseRequest request, LicensingKey privateKey)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (privateKey is null)
        {
            throw new ArgumentNullException(nameof(privateKey));
        }

        if (privateKey.Kind != KeyKind.Private)
        {
            throw new ArgumentException(
                "Signing needs the private half of the pair; this is the public one.",
                nameof(privateKey));
        }

        string algorithmId = AlgorithmIds.ToIdentifier(privateKey.Algorithm)
            ?? throw new ArgumentException(
                "The key's algorithm is not a defined member.", nameof(privateKey));

        License license = BuildLicense(request);
        byte[] payload = LicensePayload.Serialize(license);

        // Sign the bytes that are about to be stored — not a re-serialization of the license, and not a
        // string assembled from its fields. There is exactly one byte sequence in play.
        byte[] signature = _signatures.Sign(privateKey, payload);

        return new LicenseEnvelope
        {
            Format = LicenseEnvelope.CurrentFormat,
            Algorithm = algorithmId,
            KeyId = privateKey.KeyId,
            Payload = payload,
            Signature = signature,
        };
    }

    /// <inheritdoc />
    public async Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        LicensingKey privateKey,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        LicenseEnvelope envelope = Generate(request, privateKey);

        await envelope.SaveAsync(output, cancellationToken).ConfigureAwait(false);

        return envelope;
    }

    /// <inheritdoc />
    public async Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        string privateKeyPath,
        char[]? password,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        DocumentFile.RequirePath(privateKeyPath, nameof(privateKeyPath));
        DocumentFile.RequirePath(outputPath, nameof(outputPath));

        LicensingKey privateKey = await _keyService
            .LoadKeyAsync(privateKeyPath, password, cancellationToken)
            .ConfigureAwait(false);

        // Signed before the output file is touched, so a rejected request or an unusable key leaves no
        // truncated license behind.
        LicenseEnvelope envelope = Generate(request, privateKey);

        using FileStream output = DocumentFile.Create(outputPath);

        await envelope.SaveAsync(output, cancellationToken).ConfigureAwait(false);

        return envelope;
    }

    private static License BuildLicense(LicenseRequest request)
    {
        if (IsBlank(request.ProductId))
        {
            throw new ArgumentException(
                "The request needs a product identifier — it is the one field with no sensible default.",
                nameof(request));
        }

        // A null Id is the normal case and means "issue one"; a blank one is a caller mistake.
        if (request.Id is not null && IsBlank(request.Id))
        {
            throw new ArgumentException(
                "The request's Id is blank. Leave it null to have a ULID issued.",
                nameof(request));
        }

        if (request.ValidFrom > request.ValidUntil)
        {
            throw new ArgumentException(
                $"The validity window ends before it starts: ValidFrom {request.ValidFrom:O} is after " +
                $"ValidUntil {request.ValidUntil:O}.",
                nameof(request));
        }

        return new License
        {
            // Stamped here rather than on the request, so the request stays a reusable description of
            // intent and each license signed from it is a distinct, individually identifiable artefact.
            Id = request.Id ?? Ulid.NewUlid().ToString(),
            CreatedAt = request.CreatedAt ?? DateTimeOffset.UtcNow,
            ProductId = request.ProductId,
            DeviceId = request.DeviceId,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            Owner = request.Owner,
            // License coerces a null of its own accord; assigning through keeps that in one place.
            Features = request.Features!,
            Claims = request.Claims!,
        };
    }

    // Trim().Length rather than string.IsNullOrWhiteSpace so netstandard2.0 and the modern TFMs agree
    // on exactly which characters count as blank.
    private static bool IsBlank(string? value) => value is null || value.Trim().Length == 0;
}
