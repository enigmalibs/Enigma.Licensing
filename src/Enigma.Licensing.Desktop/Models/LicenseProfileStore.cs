using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Models;

/// <summary>
/// The <see cref="ILicenseProfileStore"/> backed by one JSON file per profile.
/// </summary>
/// <remarks>
/// <para>
/// The serializer settings match the preference store's, and for the same reason: this is a small file
/// somebody will eventually open in an editor. Indented and camel-cased on the way out, case-insensitive on
/// the way in, and nulls omitted so an unused field is simply absent rather than written as <c>null</c>.
/// </para>
/// <para>
/// Nothing here is stateful and nothing is cached — a profile is read when it is loaded and written when it
/// is saved, which is why the same instance serves every path the user picks.
/// </para>
/// </remarks>
internal sealed class LicenseProfileStore : ILicenseProfileStore
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <inheritdoc/>
    public async Task<LicenseProfile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        LicenseProfile? profile;

        await using (FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                profile = await JsonSerializer
                    .DeserializeAsync<LicenseProfile>(input, _json, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"'{path}' does not contain a well-formed licence profile.", nameof(path), ex);
            }
        }

        if (profile is null)
        {
            throw new ArgumentException($"'{path}' contains the JSON literal 'null', not a licence profile.", nameof(path));
        }

        if (profile.FormatVersion > LicenseProfile.CurrentFormatVersion)
        {
            // Only *higher* is refused. Anything at or below the current version is a shape this build
            // knows, and a file with no formatVersion at all reads as the current one — which is the only
            // shape there has ever been.
            throw new ArgumentException(
                $"'{path}' declares profile version {profile.FormatVersion}, and this application reads {LicenseProfile.CurrentFormatVersion}.",
                nameof(path));
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(LicenseProfile profile, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream output = new(path, FileMode.Create, FileAccess.Write, FileShare.None);

        // Written with the version this build produces, whatever the loaded profile carried: what is being
        // saved is the form as it stands now, not the file it happened to come from.
        await JsonSerializer
            .SerializeAsync(output, profile with { FormatVersion = LicenseProfile.CurrentFormatVersion }, _json, cancellationToken)
            .ConfigureAwait(false);
    }
}
