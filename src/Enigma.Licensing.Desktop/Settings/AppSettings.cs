using System.Text.Json.Serialization;

namespace Enigma.Licensing.Desktop.Settings;

/// <summary>
/// Everything the application remembers between runs.
/// </summary>
/// <remarks>
/// <para>
/// This is a preference file, not a document: a fresh instance is exactly what a first run should behave
/// like, so every property carries the default the application would have used had the file never existed.
/// </para>
/// <para>
/// <see cref="FormatVersion"/> is written on every save and checked on every load. It is what lets a later
/// version change this shape without a newer file silently mis-reading in an older build — the older build
/// sees a number it does not know and falls back to its defaults rather than guessing.
/// </para>
/// <para>
/// Nothing about a license or a key belongs here. A signing key's path is remembered by a license profile
/// (<c>FEATURE-179C-PHASE03</c>), which the user saves deliberately, and a password is never written
/// anywhere by this application.
/// </para>
/// </remarks>
public sealed class AppSettings
{
    /// <summary>The shape of the file this version writes, and the only one it reads.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Gets the shape of the file these settings came from.</summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>Gets the colour variant the user chose.</summary>
    /// <remarks>
    /// Written as its name rather than its number, so the file stays readable — and correctable — by hand.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; init; } = AppTheme.System;
}
