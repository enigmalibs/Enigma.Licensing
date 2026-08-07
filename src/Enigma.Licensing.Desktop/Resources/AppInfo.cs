using System.Reflection;

namespace Enigma.Licensing.Desktop.Resources;

/// <summary>
/// What the application says about itself: its name, the two versions it is built from, and where it lives.
/// </summary>
/// <remarks>
/// <para>
/// The versions are read from the assemblies rather than written down here, so they cannot drift from what
/// is actually running — which is the only reason an about section is worth showing at all. Both are the
/// SDK default <c>1.0.0</c> until <c>FEATURE-57BC</c> sets the real release numbers.
/// </para>
/// <para>
/// <see cref="ProjectUrl"/> is shown as text, not opened. Launching a browser is a platform action that
/// would need a seam of its own, and nothing else in this application needs one.
/// </para>
/// </remarks>
public static class AppInfo
{
    /// <summary>The application's name, used as the window title and in the about section.</summary>
    public const string Title = "Enigma Licensing";

    /// <summary>Where the source lives.</summary>
    public const string ProjectUrl = "https://github.com/enigmalibs/Enigma.Licensing";

    /// <summary>Gets the version of the application itself.</summary>
    public static string AppVersion { get; } = VersionOf(typeof(AppInfo).Assembly);

    /// <summary>Gets the version of the <c>Enigma.Licensing</c> library this build signs and checks with.</summary>
    /// <remarks>
    /// <c>LicenseEnvelope</c> is named without a using directive: this namespace sits under
    /// <c>Enigma.Licensing</c>, so the library's types already resolve from the enclosing namespace.
    /// </remarks>
    public static string LibraryVersion { get; } = VersionOf(typeof(LicenseEnvelope).Assembly);

    /// <summary>Reads one assembly's version as a person would want to read it.</summary>
    /// <param name="assembly">The assembly to report on.</param>
    /// <returns>
    /// Its informational version with any build metadata dropped, its assembly version if it declares no
    /// informational one, and <c>unknown</c> if it somehow declares neither.
    /// </returns>
    /// <remarks>
    /// The informational version is the one that carries a pre-release suffix, so it is preferred — but the
    /// SDK appends <c>+&lt;commit&gt;</c> to it when source-link metadata is present, and that hash is noise
    /// in an about box.
    /// </remarks>
    private static string VersionOf(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            int metadata = informational.IndexOf('+');

            return metadata < 0 ? informational : informational[..metadata];
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
