using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Models;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the profile store: it keeps what was saved and where, hands back what it was primed with,
/// and can be made to fail either way.
/// </summary>
internal sealed class FakeLicenseProfileStore : ILicenseProfileStore
{
    /// <summary>Gets or sets the profile every load returns.</summary>
    internal LicenseProfile? Profile { get; set; }

    /// <summary>Gets or sets a failure to raise instead of loading.</summary>
    internal Exception? FailLoad { get; set; }

    /// <summary>Gets or sets a failure to raise instead of saving.</summary>
    internal Exception? FailSave { get; set; }

    /// <summary>Gets the profile the last save was handed.</summary>
    internal LicenseProfile? Saved { get; private set; }

    /// <summary>Gets where the last save was told to write.</summary>
    internal string? SavedPath { get; private set; }

    /// <summary>Gets where the last load was told to read from.</summary>
    internal string? LoadedPath { get; private set; }

    /// <summary>Gets how many profiles were written.</summary>
    internal int Saves { get; private set; }

    /// <inheritdoc/>
    public Task<LicenseProfile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        LoadedPath = path;

        return FailLoad is not null
            ? Task.FromException<LicenseProfile>(FailLoad)
            : Task.FromResult(Profile ?? new LicenseProfile());
    }

    /// <inheritdoc/>
    public Task SaveAsync(LicenseProfile profile, string path, CancellationToken cancellationToken = default)
    {
        Saves++;
        Saved = profile;
        SavedPath = path;

        return FailSave is not null ? Task.FromException(FailSave) : Task.CompletedTask;
    }
}
