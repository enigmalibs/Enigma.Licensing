using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.Settings;

/// <summary>
/// The preference store, as one small JSON file in the user's own configuration directory.
/// </summary>
/// <remarks>
/// <para>
/// The path is <c>%APPDATA%\Enigma.Licensing\settings.json</c> on Windows and
/// <c>~/.config/Enigma.Licensing/settings.json</c> on Linux — one call to
/// <see cref="Environment.SpecialFolder.ApplicationData"/> lands in the right place on both, so there is no
/// per-platform branch to get wrong.
/// </para>
/// <para>
/// <b>Nothing here throws.</b> Every failure a preference file can produce — absent, unreadable,
/// half-written, hand-edited into nonsense, or written by a version that knows more than this one —
/// resolves to the defaults and a line in the log. The alternative is an application that will not start
/// because of a file it could have simply ignored.
/// </para>
/// <para>
/// A save writes a sibling temporary file and then moves it over the target, so a crash mid-write leaves
/// the previous settings intact rather than a truncated file that the next start would report as corrupt.
/// </para>
/// </remarks>
public sealed class AppSettingsStore : IAppSettingsStore
{
    /// <summary>
    /// Camel-cased on the way out and case-insensitive on the way in: this file is small enough that
    /// someone will eventually edit it by hand, and a capital letter is not a reason to lose their
    /// preferences.
    /// </summary>
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ILogger<AppSettingsStore> _logger;
    private readonly string _filePath;

    /// <summary>Initializes a new instance of the <see cref="AppSettingsStore"/> class.</summary>
    /// <param name="logger">Records anything that stopped a load or a save; the user is never told.</param>
    /// <param name="filePath">The file to read and write. <see cref="DefaultFilePath"/> in the application.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public AppSettingsStore(ILogger<AppSettingsStore> logger, string filePath)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(filePath);

        _logger = logger;
        _filePath = filePath;
    }

    /// <summary>Gets the file the running application keeps its preferences in.</summary>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Enigma.Licensing",
        "settings.json");

    /// <inheritdoc/>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                // The normal first run. Not worth a log line.
                return new AppSettings();
            }

            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), _json);

            if (settings is null)
            {
                _logger.LogWarning("{Path} holds no settings object; the defaults are used.", _filePath);

                return new AppSettings();
            }

            if (settings.FormatVersion > AppSettings.CurrentFormatVersion)
            {
                // A later version wrote this file, and this one would be guessing at what its keys mean.
                // Only higher is refused: anything at or below the current version is a shape this build
                // knows, and a file with no formatVersion at all reads as the current one, which is the only
                // shape there has ever been.
                _logger.LogWarning(
                    "{Path} declares format version {Found}, and this version reads {Expected}; the defaults are used.",
                    _filePath,
                    settings.FormatVersion,
                    AppSettings.CurrentFormatVersion);

                return new AppSettings();
            }

            if (!Enum.IsDefined(settings.Theme))
            {
                // A theme written as a number the enum does not have: the string converter accepts numbers
                // as well as names, so this is the one malformed value that does not throw on its own.
                _logger.LogWarning("{Path} names a theme this version does not have; the defaults are used.", _filePath);

                return new AppSettings();
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "{Path} could not be read; the defaults are used.", _filePath);

            return new AppSettings();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Written beside the target and then moved over it: the move is what the next start sees, so it
            // never reads a file that is half a preference.
            string temporary = _filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, _json));
            File.Move(temporary, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "{Path} could not be written; the preference will not survive this run.", _filePath);
        }
    }
}
