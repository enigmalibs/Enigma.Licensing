using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Models;

/// <summary>
/// Reads and writes <see cref="LicenseProfile"/> files.
/// </summary>
/// <remarks>
/// <para>
/// <b>This one throws, unlike <c>IAppSettingsStore</c>.</b> The preference file is the application's own and
/// a user never asked for it, so falling back to the defaults is the kind thing to do there. A profile is
/// the opposite: the user picked this file from a dialog and expects the form to be filled from it, so a
/// file that cannot be read has to be reported, not silently ignored. Both stores are right; they answer to
/// different owners.
/// </para>
/// <para>
/// <b>Error contract.</b> <see cref="ArgumentException"/> for a document that is not a well-formed profile
/// of a known version, with the underlying parse failure preserved as the inner exception; I/O failures
/// propagate untouched, including a missing file.
/// </para>
/// </remarks>
public interface ILicenseProfileStore
{
    /// <summary>Reads a profile from a file.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty, or the file is not a well-formed profile of a version this
    /// application reads.
    /// </exception>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="path"/>.</exception>
    /// <exception cref="IOException">The file could not be read. I/O failures propagate untouched.</exception>
    Task<LicenseProfile> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes a profile to a file, creating any missing parent directories.</summary>
    /// <param name="profile">The profile to write.</param>
    /// <param name="path">Where to write it. An existing file is overwritten.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="IOException">The file could not be written. I/O failures propagate untouched.</exception>
    Task SaveAsync(LicenseProfile profile, string path, CancellationToken cancellationToken = default);
}
