using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.Settings;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// The settings page: the appearance of the application, and what this build is made of.
/// </summary>
/// <remarks>
/// <para>
/// Nothing about the issuer's work is remembered here. A signing key's path and the fields of a licence
/// belong to a licence profile the user saves deliberately, and a password is never written anywhere by
/// this application — so appearance is the only preference there is.
/// </para>
/// <para>
/// Choosing a variant does two things, in this order and exactly once each: it repaints the application
/// through <see cref="IThemeService"/>, and it writes the choice through <see cref="IAppSettingsStore"/>.
/// The stored choice is read once here, into the backing field rather than through the property, because
/// <c>App</c> has already applied it before the window was shown — going through the setter would repaint
/// what is already painted and rewrite the file on every first visit to this page.
/// </para>
/// </remarks>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IAppSettingsStore _store;

    private ThemeOption _selectedTheme;

    /// <summary>Initializes a new instance of the <see cref="SettingsViewModel"/> class.</summary>
    /// <param name="theme">Repaints the application when the choice changes.</param>
    /// <param name="store">Holds the choice between runs.</param>
    /// <param name="about">Shows the About dialog the about section opens.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SettingsViewModel(IThemeService theme, IAppSettingsStore store, IAboutDialogService about)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(about);

        _theme = theme;
        _store = store;
        OpenAboutCommand = new AsyncRelayCommand(about.ShowAsync);

        // A page that throws while being built is a blank window, so a stored value outside the enum falls
        // back here as well — the store already guards its own file, this guards the contract.
        AppTheme stored = store.Load().Theme;
        _selectedTheme = Enum.IsDefined(stored) ? ThemeOption.For(stored) : ThemeOption.Default;
    }

    /// <summary>Gets the variants on offer.</summary>
    public IReadOnlyList<ThemeOption> Themes => ThemeOption.All;

    /// <summary>Gets the version of the application itself.</summary>
    public string AppVersion => AppInfo.AppVersion;

    /// <summary>Gets the version of the <c>Enigma.Licensing</c> library this build signs and checks with.</summary>
    public string LibraryVersion => AppInfo.LibraryVersion;

    /// <summary>Gets the address of the project's source, shown so it can be read and copied.</summary>
    public string ProjectUrl => AppInfo.ProjectUrl;

    /// <summary>
    /// Gets the command that opens the About dialog — the one every Enigma application shows, with the icon,
    /// the build and the copyright.
    /// </summary>
    public AsyncRelayCommand OpenAboutCommand { get; }

    /// <summary>Gets or sets the variant the application is painted in.</summary>
    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            // A selector can hand back null while its items are being replaced, and re-selecting what is
            // already selected is not a change: neither is worth a repaint or a write.
            if (value is null || !SetProperty(ref _selectedTheme, value))
            {
                return;
            }

            _theme.Apply(value.Theme);
            _store.Save(new AppSettings { Theme = value.Theme });
        }
    }
}
