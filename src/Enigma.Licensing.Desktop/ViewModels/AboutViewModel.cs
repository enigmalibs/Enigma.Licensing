using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Licensing.Desktop.Resources;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// What the About dialog says: which product this is, and which build of it is running.
/// </summary>
/// <remarks>
/// Read-only: nothing here changes while the dialog is open. It derives from <see cref="ObservableObject"/>
/// anyway, so it is the same shape as every other ViewModel in this folder. The dialog is the one every Enigma
/// application shows — Enigma.GitClient's is the reference.
/// </remarks>
public sealed class AboutViewModel : ObservableObject
{
    /// <summary>Initializes a new instance of the <see cref="AboutViewModel"/> class over the running build.</summary>
    public AboutViewModel()
        : this(AppInfo.AppVersion, AppInfo.BuildSha, AppInfo.Copyright)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AboutViewModel"/> class over given values.</summary>
    /// <param name="version">The version to show.</param>
    /// <param name="buildSha">The short revision to show, or <see langword="null"/> to show no build line.</param>
    /// <param name="copyright">The copyright notice, or an empty string to show no copyright line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> or <paramref name="copyright"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The values are arguments rather than read here because <see cref="AppInfo"/> reports the assembly under
    /// test, which a test cannot vary: this is the seam that lets a build with no revision be asserted at all.
    /// </remarks>
    public AboutViewModel(string version, string? buildSha, string copyright)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(copyright);

        Version = version;
        BuildSha = buildSha;
        Copyright = copyright;
    }

    /// <summary>Gets the product's name.</summary>
    public string DisplayName => AppInfo.Title;

    /// <summary>Gets the version of the running build.</summary>
    public string Version { get; }

    /// <summary>Gets the short commit this build was cut from, or <see langword="null"/> if it recorded none.</summary>
    public string? BuildSha { get; }

    /// <summary>
    /// Gets a value indicating whether there is a revision to show: a build with none shows no build line at
    /// all, not an empty one.
    /// </summary>
    public bool HasBuildSha => !string.IsNullOrEmpty(BuildSha);

    /// <summary>Gets the copyright notice, or an empty string if the assembly carries none.</summary>
    public string Copyright { get; }

    /// <summary>Gets a value indicating whether there is a copyright notice to show.</summary>
    public bool HasCopyright => Copyright.Length > 0;
}
