using System;
using System.Reflection;

namespace Enigma.Licensing.Desktop.Resources;

/// <summary>
/// What the application says about itself: its name, the two versions it is built from, the revision it was
/// cut from, its copyright, and where it lives.
/// </summary>
/// <remarks>
/// <para>
/// The versions are read from the assemblies rather than written down here, so they cannot drift from what
/// is actually running — which is the only reason an about section is worth showing at all.
/// </para>
/// <para>
/// <see cref="ProjectUrl"/> is shown as text, not opened. Launching a browser is a platform action that
/// would need a seam of its own, and nothing else in this application needs one.
/// </para>
/// </remarks>
public static class AppInfo
{
    /// <summary>The application's name, used as the window title, in the about section and in the About dialog.</summary>
    public const string Title = "Enigma Licensing";

    /// <summary>Where the source lives.</summary>
    public const string ProjectUrl = "https://github.com/enigmalibs/Enigma.Licensing";

    /// <summary>
    /// What <see cref="ParseVersion"/> reports when neither the informational version nor the fallback says
    /// anything usable. A version is always shown, so there has to be one.
    /// </summary>
    public const string UnknownVersion = "0.0.0";

    /// <summary>
    /// How many characters of a source revision are kept — the length git itself abbreviates to.
    /// </summary>
    private const int ShaLength = 7;

    private static readonly (string Version, string? BuildSha) Running = Read(typeof(AppInfo).Assembly);

    /// <summary>Gets the version of the application itself.</summary>
    public static string AppVersion => Running.Version;

    /// <summary>
    /// Gets the short commit this build was cut from, or <see langword="null"/> when it recorded none — a build
    /// from a source drop rather than from the repository.
    /// </summary>
    public static string? BuildSha => Running.BuildSha;

    /// <summary>
    /// Gets the copyright notice <c>Directory.Build.props</c> stamps on the assembly, or an empty string when it
    /// carries none.
    /// </summary>
    public static string Copyright { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Trim() ?? string.Empty;

    /// <summary>Gets the version of the <c>Enigma.Licensing</c> library this build signs and checks with.</summary>
    /// <remarks>
    /// <c>LicenseEnvelope</c> is named without a using directive: this namespace sits under
    /// <c>Enigma.Licensing</c>, so the library's types already resolve from the enclosing namespace.
    /// </remarks>
    public static string LibraryVersion { get; } = Read(typeof(LicenseEnvelope).Assembly).Version;

    /// <summary>
    /// Reads an <c>AssemblyInformationalVersion</c> value as a person would want to read it.
    /// </summary>
    /// <param name="informationalVersion">
    /// The attribute's value — <c>X.Y.Z</c>, or <c>X.Y.Z+&lt;sha&gt;</c> when the build recorded the source
    /// revision, which the SDK does in a git checkout.
    /// </param>
    /// <param name="fallbackVersion">
    /// What to report when <paramref name="informationalVersion"/> says nothing usable — the plain assembly
    /// version. May itself be <see langword="null"/>.
    /// </param>
    /// <returns>The version to show, and the revision cut to seven characters if there was one.</returns>
    /// <remarks>
    /// The version is everything before the first <c>+</c>, verbatim, so a pre-release such as
    /// <c>2.1.0-rc.1</c> is shown exactly as the project states it. Nothing here throws: a missing attribute
    /// must not be the reason an About dialog cannot open.
    /// </remarks>
    public static (string Version, string? BuildSha) ParseVersion(string? informationalVersion, string? fallbackVersion)
    {
        string candidate = informationalVersion?.Trim() ?? string.Empty;

        if (candidate.Length > 0)
        {
            int separator = candidate.IndexOf('+', StringComparison.Ordinal);
            string version = separator < 0 ? candidate : candidate[..separator];

            if (version.Length > 0)
            {
                return (version, Shorten(separator < 0 ? null : candidate[(separator + 1)..]));
            }
        }

        string fallback = fallbackVersion?.Trim() ?? string.Empty;

        return (fallback.Length > 0 ? fallback : UnknownVersion, null);
    }

    /// <summary>Reads one assembly's version and revision.</summary>
    /// <param name="assembly">The assembly to report on.</param>
    /// <returns>What <see cref="ParseVersion"/> makes of its informational and assembly versions.</returns>
    private static (string Version, string? BuildSha) Read(Assembly assembly)
    {
        Version? version = assembly.GetName().Version;

        return ParseVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            version?.ToString(version.Build >= 0 ? 3 : 2));
    }

    /// <summary>Abbreviates a source revision to seven characters, keeping a shorter one whole.</summary>
    private static string? Shorten(string? revision)
    {
        string trimmed = revision?.Trim() ?? string.Empty;

        return trimmed.Length switch
        {
            0 => null,
            <= ShaLength => trimmed,
            _ => trimmed[..ShaLength],
        };
    }
}
