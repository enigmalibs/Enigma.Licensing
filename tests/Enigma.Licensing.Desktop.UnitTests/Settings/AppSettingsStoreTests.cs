using System;
using System.IO;
using System.Linq;
using Enigma.Licensing.Desktop.Settings;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.UnitTests.Settings;

/// <summary>
/// The preference file, and every way it can be useless.
/// </summary>
/// <remarks>
/// The point of this class is not the round trip — that is one test — but the other ways a small JSON file
/// in a user's home directory goes wrong: absent, unreadable, truncated, hand-edited into nonsense, naming a
/// theme this version does not have, or written by a version that knows more than this one. Every one of
/// them has to come back as the defaults and a warning, because the alternative is an application that will
/// not start because of a file it could have ignored.
/// </remarks>
public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "Enigma.Licensing.Tests", Guid.NewGuid().ToString("N"));

    private readonly RecordingLogger<AppSettingsStore> _logger = new();

    private string FilePath => Path.Combine(_directory, "settings.json");

    private bool Warned => _logger.Entries.Any(entry => entry.Level == LogLevel.Warning);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void Save_ThenLoad_ReturnsWhatWasStored(AppTheme theme)
    {
        AppSettingsStore store = Create();

        store.Save(new AppSettings { Theme = theme });

        AppSettings loaded = store.Load();
        Assert.Equal(theme, loaded.Theme);
        Assert.Equal(AppSettings.CurrentFormatVersion, loaded.FormatVersion);
        Assert.False(Warned);
    }

    [Fact]
    public void Save_WritesTheThemeByName_SoTheFileReadsAsProse()
    {
        Create().Save(new AppSettings { Theme = AppTheme.Dark });

        string written = File.ReadAllText(FilePath);
        Assert.Contains("\"theme\": \"Dark\"", written, StringComparison.Ordinal);
        Assert.Contains("\"formatVersion\": 1", written, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_CreatesTheDirectory_WhenTheApplicationHasNeverRun()
    {
        string nested = Path.Combine(_directory, "Enigma.Licensing", "settings.json");

        new AppSettingsStore(_logger, nested).Save(new AppSettings { Theme = AppTheme.Light });

        Assert.True(File.Exists(nested));
        Assert.False(Warned);
    }

    /// <summary>
    /// The save writes a sibling file and moves it over the target, so a crash mid-write cannot truncate the
    /// settings. What must not survive is the sibling.
    /// </summary>
    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        Create().Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.Equal([FilePath], Directory.GetFiles(_directory));
    }

    [Fact]
    public void Save_Twice_KeepsTheSecondChoice()
    {
        AppSettingsStore store = Create();

        store.Save(new AppSettings { Theme = AppTheme.Dark });
        store.Save(new AppSettings { Theme = AppTheme.Light });

        Assert.Equal(AppTheme.Light, store.Load().Theme);
    }

    /// <summary>The normal first run: nothing stored, nothing wrong, nothing to say about it.</summary>
    [Fact]
    public void Load_WithNoFile_ReturnsTheDefaults_AndSaysNothing()
    {
        AppSettings loaded = Create().Load();

        Assert.Equal(AppTheme.System, loaded.Theme);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Load_WithCorruptJson_ReturnsTheDefaults_AndWarns()
    {
        Given("{ \"formatVersion\": 1, \"theme\": ");

        Assert.Equal(AppTheme.System, Create().Load().Theme);
        Assert.True(Warned);
    }

    [Fact]
    public void Load_WithAnEmptyFile_ReturnsTheDefaults_AndWarns()
    {
        Given(string.Empty);

        Assert.Equal(AppTheme.System, Create().Load().Theme);
        Assert.True(Warned);
    }

    [Fact]
    public void Load_WithJsonNull_ReturnsTheDefaults_AndWarns()
    {
        Given("null");

        Assert.Equal(AppTheme.System, Create().Load().Theme);
        Assert.True(Warned);
    }

    /// <summary>
    /// A name the enum does not have throws inside the converter; a number it does not have does not, because
    /// the string converter accepts numbers too. Both have to end in the same place.
    /// </summary>
    /// <param name="theme">The theme value as it appears in the file.</param>
    [Theory]
    [InlineData("\"Chartreuse\"")]
    [InlineData("7")]
    [InlineData("null")]
    public void Load_WithAThemeThisVersionDoesNotHave_ReturnsTheDefaults_AndWarns(string theme)
    {
        Given($"{{ \"formatVersion\": 1, \"theme\": {theme} }}");

        Assert.Equal(AppTheme.System, Create().Load().Theme);
        Assert.True(Warned);
    }

    [Fact]
    public void Load_WithAHigherFormatVersion_ReturnsTheDefaults_AndWarns()
    {
        Given("{ \"formatVersion\": 2, \"theme\": \"Dark\" }");

        Assert.Equal(AppTheme.System, Create().Load().Theme);
        Assert.True(Warned);
    }

    /// <summary>
    /// Only a <i>higher</i> version is refused. A file with no version at all — the shape someone gets by
    /// typing the preference in themselves — is the current shape, because it is the only shape there has
    /// ever been, and refusing it would lose a preference this version understands perfectly well.
    /// </summary>
    [Fact]
    public void Load_WithNoFormatVersion_ReadsItAsTheCurrentShape()
    {
        Given("{ \"theme\": \"Dark\" }");

        Assert.Equal(AppTheme.Dark, Create().Load().Theme);
    }

    /// <summary>
    /// The file is small enough that someone will eventually edit it by hand, and a capital letter in the
    /// wrong place is not a reason to lose their preference.
    /// </summary>
    /// <param name="json">The hand-typed file.</param>
    [Theory]
    [InlineData("{ \"FormatVersion\": 1, \"Theme\": \"Dark\" }")]
    [InlineData("{ \"formatVersion\": 1, \"theme\": \"dark\" }")]
    public void Load_WithHandTypedCasing_StillReadsThePreference(string json)
    {
        Given(json);

        Assert.Equal(AppTheme.Dark, Create().Load().Theme);
    }

    /// <summary>A path that cannot exist is the same situation as a file that does not: nothing stored.</summary>
    [Fact]
    public void Load_WithAnUnreachablePath_ReturnsTheDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "blocker"), "not a directory");

        // The parent of this path is a file, so nothing can ever be read through it.
        AppSettings loaded = new AppSettingsStore(_logger, Path.Combine(_directory, "blocker", "settings.json")).Load();

        Assert.Equal(AppTheme.System, loaded.Theme);
    }

    [Fact]
    public void Save_WithAnUnwritablePath_DoesNotThrow_AndWarns()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "blocker"), "not a directory");

        AppSettingsStore store = new(_logger, Path.Combine(_directory, "blocker", "settings.json"));

        store.Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.True(Warned);
    }

    [Fact]
    public void DefaultFilePath_IsTheApplicationsOwnFolderInTheUsersConfiguration()
    {
        Assert.Equal("settings.json", Path.GetFileName(AppSettingsStore.DefaultFilePath));
        Assert.Equal("Enigma.Licensing", Path.GetFileName(Path.GetDirectoryName(AppSettingsStore.DefaultFilePath)));
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new AppSettingsStore(null!, FilePath));
        Assert.Throws<ArgumentNullException>(() => new AppSettingsStore(_logger, null!));
    }

    [Fact]
    public void Save_RejectsNull() => Assert.Throws<ArgumentNullException>(() => Create().Save(null!));

    private AppSettingsStore Create() => new(_logger, FilePath);

    private void Given(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, json);
    }
}
