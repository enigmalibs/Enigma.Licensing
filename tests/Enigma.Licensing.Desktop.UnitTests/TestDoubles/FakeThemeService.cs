using System.Collections.Generic;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.Settings;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the application's repainting, keeping every variant it was asked for so a test can assert
/// both how many times it was asked and what it was asked for.
/// </summary>
internal sealed class FakeThemeService : IThemeService
{
    private readonly List<AppTheme> _applied = [];

    /// <summary>Gets every variant applied, in order.</summary>
    internal IReadOnlyList<AppTheme> Applied => _applied;

    /// <inheritdoc/>
    public void Apply(AppTheme theme) => _applied.Add(theme);
}
