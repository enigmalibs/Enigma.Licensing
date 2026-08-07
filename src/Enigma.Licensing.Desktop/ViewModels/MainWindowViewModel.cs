using System;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// ViewModel of the application shell: the window title, and the navigation rail the pages hang off — the
/// three issuer workflows in the list, and settings in the footer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="INavigationService"/> is exposed for binding rather than wrapped, and it is the one library
/// service a ViewModel in this application names: its <c>CurrentPage</c> is an Avalonia
/// <see cref="Control"/> by definition, so a wrapper would only move the type one file over.
/// </para>
/// <para>
/// A <see cref="NavigationItem"/> is the single declaration site for a page — its rail header, its icon, its
/// view type and its ViewModel type — which is why nothing here needs a page base class or a name-matching
/// view locator. <see cref="CreatePage"/> resolves both halves from the container, so a page and its
/// ViewModel may take constructor dependencies; views are registered transient and page ViewModels
/// singleton, so revisiting a page gets a fresh control and the state it was left in.
/// </para>
/// <para>
/// The rail's order is the issuer's own order of work: keys are generated first, a licence is signed with
/// them second, and validating one is the check that closes the loop.
/// </para>
/// <para>
/// <b>The constructor navigates nowhere.</b> It builds the rail and stops: the first item is selected by
/// <see cref="App"/> once the container is up, which keeps this ViewModel constructible — and the rail
/// assertable — with no windowing platform behind it.
/// </para>
/// <para>
/// The rail icons are handed in rather than resolved here. Turning a Phosphor glyph into a
/// <see cref="Geometry"/> builds a <c>StreamGeometry</c>, which needs Avalonia's platform render interface:
/// present in the running app, absent in a test process. The container's factory passes
/// <see cref="AppIcons"/>, and a test passes its own.
/// </para>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    /// <summary>Initializes a new instance of the <see cref="MainWindowViewModel"/> class.</summary>
    /// <param name="services">Resolves each page and its ViewModel when the rail navigates.</param>
    /// <param name="navigation">Owns the rail's items, its selection and the page showing.</param>
    /// <param name="keysIcon">The key-generation page's rail icon.</param>
    /// <param name="generateIcon">The licence-generation page's rail icon.</param>
    /// <param name="validateIcon">The validation page's rail icon.</param>
    /// <param name="settingsIcon">The settings page's rail icon.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public MainWindowViewModel(
        IServiceProvider services,
        INavigationService navigation,
        Geometry keysIcon,
        Geometry generateIcon,
        Geometry validateIcon,
        Geometry settingsIcon)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(keysIcon);
        ArgumentNullException.ThrowIfNull(generateIcon);
        ArgumentNullException.ThrowIfNull(validateIcon);
        ArgumentNullException.ThrowIfNull(settingsIcon);

        _services = services;
        Navigation = navigation;
        Navigation.PageFactory = CreatePage;

        Navigation.Items.Add(new NavigationItem
        {
            Header = "Keys",
            IconData = keysIcon,
            PageType = typeof(GenerateKeysView),
            PageViewModelType = typeof(GenerateKeysViewModel),
        });

        Navigation.Items.Add(new NavigationItem
        {
            Header = "Generate",
            IconData = generateIcon,
            PageType = typeof(GenerateLicenseView),
            PageViewModelType = typeof(GenerateLicenseViewModel),
        });

        Navigation.Items.Add(new NavigationItem
        {
            Header = "Validate",
            IconData = validateIcon,
            PageType = typeof(ValidateLicenseView),
            PageViewModelType = typeof(ValidateLicenseViewModel),
        });

        // Settings goes in the footer rather than the list: it is not one of the three things this
        // application is for, and the rail's footer is where a page that serves the app rather than the work
        // belongs.
        Navigation.FooterItems.Add(new NavigationItem
        {
            Header = "Settings",
            IconData = settingsIcon,
            PageType = typeof(SettingsView),
            PageViewModelType = typeof(SettingsViewModel),
        });
    }

    /// <summary>Gets the window title.</summary>
    public string Title => AppInfo.Title;

    /// <summary>Gets the navigation state the shell binds to: the rail's items, its selection and the page showing.</summary>
    public INavigationService Navigation { get; }

    /// <summary>
    /// Builds the page a rail item stands for, with the ViewModel it is bound to already attached.
    /// </summary>
    /// <param name="item">The item being navigated to.</param>
    /// <returns>The page, ready to be shown.</returns>
    /// <remarks>
    /// A failure here is reported on <see cref="INavigationService.NavigationFailed"/> rather than thrown —
    /// the service swallows it and empties the content area — which is why <see cref="App"/> subscribes to
    /// that event and logs it.
    /// </remarks>
    private Control CreatePage(NavigationItem item)
    {
        Control page = (Control)_services.GetRequiredService(item.PageType);
        page.DataContext = _services.GetRequiredService(item.PageViewModelType);

        return page;
    }
}
