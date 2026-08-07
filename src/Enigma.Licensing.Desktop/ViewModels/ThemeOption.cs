using System;
using System.Collections.Generic;
using Enigma.Licensing.Desktop.Settings;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// One entry of the appearance selector: an <see cref="AppTheme"/> and the words offering it.
/// </summary>
/// <remarks>
/// A wrapper rather than binding the enum directly: <c>System</c> is not what a user is choosing — "follow
/// the system" is — and an enum member's name is not a label.
/// </remarks>
public sealed class ThemeOption
{
    private ThemeOption(AppTheme theme, string label)
    {
        Theme = theme;
        Label = label;
    }

    /// <summary>Gets every offered variant, starting with the default.</summary>
    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(AppTheme.System, "Follow the system"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    /// <summary>Gets the option the application falls back to whenever no other one holds.</summary>
    public static ThemeOption Default { get; } = For(AppTheme.System);

    /// <summary>Gets the variant this option selects.</summary>
    public AppTheme Theme { get; }

    /// <summary>Gets the words shown in the selector.</summary>
    public string Label { get; }

    /// <summary>Returns the option for <paramref name="theme"/>.</summary>
    /// <param name="theme">The variant to look up.</param>
    /// <returns>The matching option.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="theme"/> is not an offered variant.</exception>
    public static ThemeOption For(AppTheme theme)
    {
        foreach (ThemeOption option in All)
        {
            if (option.Theme == theme)
            {
                return option;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(theme));
    }

    /// <inheritdoc/>
    public override string ToString() => Label;
}
