using System;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The shell's navigation rail: the three issuer workflows in order, each declaring the view and the
/// ViewModel of its page, settings pinned to the footer — and a constructor that navigates nowhere, which is
/// both a design rule and what keeps the rail assertable with no windowing platform behind it.
/// </summary>
/// <remarks>
/// The icons are passed in as sentinels rather than resolved from the Phosphor pack: a real icon
/// <see cref="Geometry"/> is a <c>StreamGeometry</c>, whose construction needs Avalonia's platform render
/// interface, and this suite deliberately stands up no platform. A <see cref="PathGeometry"/> needs none, so
/// each item can still be checked against the exact instance it was given — which proves the wiring, not
/// just that something was set.
/// </remarks>
public sealed class MainWindowViewModelTests
{
    private static readonly Geometry KeysIcon = new PathGeometry();
    private static readonly Geometry GenerateIcon = new PathGeometry();
    private static readonly Geometry ValidateIcon = new PathGeometry();
    private static readonly Geometry SettingsIcon = new PathGeometry();

    [Fact]
    public void Title_IsTheApplicationName() => Assert.Equal(AppInfo.Title, Create().Title);

    [Fact]
    public void Navigation_IsTheServiceItWasGiven()
    {
        NavigationService navigation = new();

        MainWindowViewModel viewModel = Create(navigation);

        Assert.Same(navigation, viewModel.Navigation);
    }

    /// <summary>
    /// The rail's order is the issuer's own order of work: keys first, the licence they sign second, the
    /// check that closes the loop third.
    /// </summary>
    [Fact]
    public void Items_AreKeysThenGenerateThenValidate_WithTheirViewsAndViewModels()
    {
        MainWindowViewModel viewModel = Create();

        Assert.Collection(
            viewModel.Navigation.Items,
            item =>
            {
                Assert.Equal("Keys", item.Header);
                Assert.Equal(typeof(GenerateKeysView), item.PageType);
                Assert.Equal(typeof(GenerateKeysViewModel), item.PageViewModelType);
                Assert.Same(KeysIcon, item.IconData);
            },
            item =>
            {
                Assert.Equal("Generate", item.Header);
                Assert.Equal(typeof(GenerateLicenseView), item.PageType);
                Assert.Equal(typeof(GenerateLicenseViewModel), item.PageViewModelType);
                Assert.Same(GenerateIcon, item.IconData);
            },
            item =>
            {
                Assert.Equal("Validate", item.Header);
                Assert.Equal(typeof(ValidateLicenseView), item.PageType);
                Assert.Equal(typeof(ValidateLicenseViewModel), item.PageViewModelType);
                Assert.Same(ValidateIcon, item.IconData);
            });
    }

    /// <summary>
    /// Settings is pinned to the footer rather than added to the list: it is not one of the three things this
    /// application is for, and which collection it lands in is the whole of that statement.
    /// </summary>
    [Fact]
    public void FooterItems_AreSettingsAlone_WithItsViewAndViewModel()
    {
        MainWindowViewModel viewModel = Create();

        NavigationItem item = Assert.Single(viewModel.Navigation.FooterItems);
        Assert.Equal("Settings", item.Header);
        Assert.Equal(typeof(SettingsView), item.PageType);
        Assert.Equal(typeof(SettingsViewModel), item.PageViewModelType);
        Assert.Same(SettingsIcon, item.IconData);
    }

    [Fact]
    public void Constructor_NavigatesNowhere()
    {
        MainWindowViewModel viewModel = Create();

        Assert.Null(viewModel.Navigation.SelectedItem);
        Assert.Null(viewModel.Navigation.CurrentPage);
    }

    [Fact]
    public void PageFactory_ResolvesThePageAndItsViewModelFromTheContainer()
    {
        ServiceProvider services = new ServiceCollection()
            .AddTransient<StubPage>()
            .AddSingleton<StubPageViewModel>()
            .BuildServiceProvider();

        MainWindowViewModel viewModel = Create(services: services);
        NavigationItem item = new()
        {
            PageType = typeof(StubPage),
            PageViewModelType = typeof(StubPageViewModel),
        };

        Control page = viewModel.Navigation.PageFactory(item);

        Assert.IsType<StubPage>(page);
        Assert.Same(services.GetRequiredService<StubPageViewModel>(), page.DataContext);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(null!, new NavigationService(), KeysIcon, GenerateIcon, ValidateIcon, SettingsIcon));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(services, null!, KeysIcon, GenerateIcon, ValidateIcon, SettingsIcon));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(services, new NavigationService(), null!, GenerateIcon, ValidateIcon, SettingsIcon));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(services, new NavigationService(), KeysIcon, null!, ValidateIcon, SettingsIcon));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(services, new NavigationService(), KeysIcon, GenerateIcon, null!, SettingsIcon));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(services, new NavigationService(), KeysIcon, GenerateIcon, ValidateIcon, null!));
    }

    private static MainWindowViewModel Create(
        INavigationService? navigation = null,
        IServiceProvider? services = null)
        => new(
            services ?? new ServiceCollection().BuildServiceProvider(),
            navigation ?? new NavigationService(),
            KeysIcon,
            GenerateIcon,
            ValidateIcon,
            SettingsIcon);

    private sealed class StubPage : UserControl;

    private sealed class StubPageViewModel;
}
