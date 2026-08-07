namespace Enigma.Licensing.Desktop.Settings;

/// <summary>
/// Where the application's preferences are kept between runs.
/// </summary>
/// <remarks>
/// <b>Neither method may throw into the application.</b> A preference file is not worth a failed startup or
/// a crashed page: a file that is missing, unreadable, malformed or written by a newer version reads back as
/// the defaults, and a save that cannot be made is dropped. Both are recorded in the log instead.
/// </remarks>
public interface IAppSettingsStore
{
    /// <summary>Reads the stored preferences.</summary>
    /// <returns>What was stored, or the defaults when nothing usable was.</returns>
    AppSettings Load();

    /// <summary>Stores the preferences, replacing whatever was there.</summary>
    /// <param name="settings">The preferences to keep.</param>
    void Save(AppSettings settings);
}
