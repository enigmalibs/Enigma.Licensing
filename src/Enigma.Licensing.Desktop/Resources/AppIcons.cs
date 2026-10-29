using Avalonia.Media;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

namespace Enigma.Licensing.Desktop.Resources;

/// <summary>
/// The Phosphor icon outlines this application resolves from C#.
/// </summary>
/// <remarks>
/// <para>
/// XAML reaches the same artwork more briefly through <c>{ei:IconGeometry …}</c>, and that is the route to
/// prefer for a fixed icon written straight into markup. This class is for the geometries that have to be
/// chosen in code — a navigation item's icon is a <c>Geometry</c> property, not markup.
/// </para>
/// <para>
/// Each outline is resolved once and kept: <c>ToGeometry()</c> re-parses the path data on every call and
/// hands back a fresh, mutable <see cref="Geometry"/>, so resolving per binding evaluation would be pure
/// waste. One consumer per icon means the shared instance is safe; an icon that needed its own
/// <see cref="Geometry.Transform"/> would have to resolve its own.
/// </para>
/// </remarks>
public static class AppIcons
{
    /// <summary>The keys page, on the navigation rail: the key pair a licence is signed with.</summary>
    public static Geometry Keys { get; } = Resolve(PhosphorIcon.Key);

    /// <summary>The generate page, on the navigation rail: the signed licence itself.</summary>
    public static Geometry Generate { get; } = Resolve(PhosphorIcon.Certificate);

    /// <summary>The validate page, on the navigation rail: checking a licence's seal.</summary>
    public static Geometry Validate { get; } = Resolve(PhosphorIcon.SealCheck);

    /// <summary>The settings page, on the navigation rail's footer.</summary>
    public static Geometry Settings { get; } = Resolve(PhosphorIcon.Gear);

    /// <summary>The safety net's error dialog: something went wrong, and the application is still running.</summary>
    public static Geometry Warning { get; } = Resolve(PhosphorIcon.Warning);

    /// <summary>Resolves one Phosphor glyph, at the regular weight, into its outline.</summary>
    /// <param name="icon">The icon to resolve.</param>
    /// <returns>The glyph's outline, ready for a <c>PathIcon.Data</c> or any other geometry slot.</returns>
    private static Geometry Resolve(PhosphorIcon icon) =>
        PhosphorIconSet.Instance.GetGlyph(icon, PhosphorWeight.Regular).ToGeometry();
}
