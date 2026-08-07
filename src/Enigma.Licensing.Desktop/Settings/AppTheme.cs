namespace Enigma.Licensing.Desktop.Settings;

/// <summary>
/// Which colour variant the application paints itself in.
/// </summary>
/// <remarks>
/// Three values rather than a light/dark switch: "follow the operating system" is a state a user has to be
/// able to come back to, and a two-valued toggle only has it until the first click.
/// </remarks>
public enum AppTheme
{
    /// <summary>Follow the operating system, and keep following it when it changes. The default.</summary>
    System = 0,

    /// <summary>Always light, whatever the operating system is set to.</summary>
    Light = 1,

    /// <summary>Always dark, whatever the operating system is set to.</summary>
    Dark = 2,
}
