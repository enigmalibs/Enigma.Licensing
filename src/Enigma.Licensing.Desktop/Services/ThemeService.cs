using System;
using Avalonia;
using Avalonia.Styling;
using Enigma.Licensing.Desktop.Settings;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IThemeService"/> backed by Avalonia's own theme variant.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place in the application that assigns <c>RequestedThemeVariant</c>. Every colour on
/// screen comes from a theme-scoped <c>Enigma*</c> brush reached through a <c>DynamicResource</c>, so one
/// assignment here repaints everything already drawn — there is nothing to rebuild and nothing to reload.
/// </para>
/// <para>
/// <see cref="AppTheme.System"/> maps to <see cref="ThemeVariant.Default"/>, which is not a third palette
/// but the absence of a request: Avalonia then follows the operating system, and keeps following it.
/// </para>
/// </remarks>
internal sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ThemeService"/> class.</summary>
    /// <param name="logger">Records a repaint asked for with no application behind it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public ThemeService(ILogger<ThemeService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc/>
    public void Apply(AppTheme theme)
    {
        if (Application.Current is not Application application)
        {
            // Only reachable with no Avalonia application running at all. A preference is not worth throwing
            // over, and the caller has nothing useful to do with the failure.
            _logger.LogWarning("The theme could not be applied: there is no application.");

            return;
        }

        application.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
