using System.Collections.Generic;
using Enigma.Licensing.Desktop.Settings;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the preference file: it hands back whatever it was primed with and keeps every save, so a
/// test can assert that a choice is written once and only once.
/// </summary>
internal sealed class FakeAppSettingsStore : IAppSettingsStore
{
    private readonly List<AppSettings> _saved = [];

    /// <summary>Initializes a new instance of the <see cref="FakeAppSettingsStore"/> class.</summary>
    /// <param name="stored">What a load returns. The defaults when not given.</param>
    internal FakeAppSettingsStore(AppSettings? stored = null) => Stored = stored ?? new AppSettings();

    /// <summary>Gets or sets what the next load returns.</summary>
    internal AppSettings Stored { get; set; }

    /// <summary>Gets every settings object saved, in order.</summary>
    internal IReadOnlyList<AppSettings> Saved => _saved;

    /// <summary>Gets how many times a load was asked for.</summary>
    internal int Loads { get; private set; }

    /// <inheritdoc/>
    public AppSettings Load()
    {
        Loads++;

        return Stored;
    }

    /// <inheritdoc/>
    public void Save(AppSettings settings) => _saved.Add(settings);
}
