using System;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Licensing.Desktop.Hosting;
using Enigma.Licensing.Desktop.Models;
using Enigma.Licensing.Desktop.Settings;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Desktop.Views;
using Enigma.Licensing.Keys;
using Microsoft.Extensions.DependencyInjection;
using AppAboutDialog = Enigma.Licensing.Desktop.Services.IAboutDialogService;
using AppBusyOverlay = Enigma.Licensing.Desktop.Services.IBusyOverlayService;
using AppClipboard = Enigma.Licensing.Desktop.Services.IClipboardService;
using AppFileDialogs = Enigma.Licensing.Desktop.Services.IFileDialogService;
using AppNotifications = Enigma.Licensing.Desktop.Services.INotificationService;
using AppTheming = Enigma.Licensing.Desktop.Services.IThemeService;

namespace Enigma.Licensing.Desktop.UnitTests.Hosting;

/// <summary>
/// The container wiring, asserted rather than discovered at run time: a missing registration is a blank
/// window with a line in the log, and a wrong <b>lifetime</b> is worse — a transient page ViewModel silently
/// discards a half-filled licence form on every page switch, and a singleton view leaks its visual tree
/// instead.
/// </summary>
/// <remarks>
/// <para>
/// The application's own services are reached through aliases so that a bare name in this file is always the
/// control library's. The two <c>Services</c> namespaces now do collide — this app wraps the library's
/// <c>IFileDialogService</c> behind one of its own — which is exactly what the aliases were put here for.
/// </para>
/// <para>
/// Registrations are checked as descriptors, and only the services that need no windowing platform are
/// actually resolved. <see cref="MainWindow"/>, the four views and <see cref="MainWindowViewModel"/> are
/// deliberately left unresolved: a <c>Window</c> needs a windowing platform, and the shell ViewModel's
/// factory resolves Phosphor icon geometry, which needs a render interface. Both are verified in the running
/// app.
/// </para>
/// </remarks>
public sealed class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(typeof(INavigationService), typeof(NavigationService))]
    [InlineData(typeof(IContentDialogService), typeof(ContentDialogService))]
    [InlineData(typeof(IOverlayService), typeof(OverlayService))]
    [InlineData(typeof(IInfoBarService), typeof(InfoBarService))]
    [InlineData(typeof(IFileDialogService), typeof(FileDialogService))]
    [InlineData(typeof(IFolderDialogService), typeof(FolderDialogService))]
    public void AddEnigmaAvaloniaDesktop_RegistersTheLibrarysService_AsASingleton(Type service, Type implementation)
    {
        ServiceCollection services = [];

        services.AddEnigmaAvaloniaDesktop();

        ServiceDescriptor descriptor = DescriptorFor(services, service);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddEnigmaAvaloniaDesktop_RegistersNothingElse()
    {
        ServiceCollection services = [];

        services.AddEnigmaAvaloniaDesktop();

        Assert.Equal(6, services.Count);
    }

    [Fact]
    public void AddEnigmaAvaloniaDesktop_LeavesAnEarlierRegistrationAlone()
    {
        NavigationService mine = new();
        ServiceCollection services = [];
        services.AddSingleton<INavigationService>(mine);

        services.AddEnigmaAvaloniaDesktop();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(mine, provider.GetRequiredService<INavigationService>());
    }

    [Fact]
    public void AddEnigmaAvaloniaDesktop_RegistersServicesThatCanBeBuilt()
    {
        ServiceCollection services = [];
        services.AddEnigmaAvaloniaDesktop();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<INavigationService>());
        Assert.NotNull(provider.GetRequiredService<IContentDialogService>());
        Assert.NotNull(provider.GetRequiredService<IOverlayService>());
        Assert.NotNull(provider.GetRequiredService<IInfoBarService>());
        Assert.NotNull(provider.GetRequiredService<IFileDialogService>());
        Assert.NotNull(provider.GetRequiredService<IFolderDialogService>());
    }

    [Theory]
    [InlineData(typeof(IAppSettingsStore), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppTheming), ServiceLifetime.Singleton)]
    [InlineData(typeof(ILicenseProfileStore), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppFileDialogs), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppNotifications), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppBusyOverlay), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppClipboard), ServiceLifetime.Singleton)]
    [InlineData(typeof(AppAboutDialog), ServiceLifetime.Singleton)]
    [InlineData(typeof(MainWindow), ServiceLifetime.Singleton)]
    [InlineData(typeof(MainWindowViewModel), ServiceLifetime.Singleton)]
    public void AddEnigmaLicensingDesktop_RegistersTheShellsService_WithTheRightLifetime(Type service, ServiceLifetime lifetime)
    {
        ServiceCollection services = [];

        services.AddEnigmaLicensingDesktop();

        Assert.Equal(lifetime, DescriptorFor(services, service).Lifetime);
    }

    /// <summary>
    /// The library is registered through its own extension rather than service by service, so the app always
    /// gets the composition the library intends — including the explicit factory that decides which
    /// validator the store validates with.
    /// </summary>
    [Theory]
    [InlineData(typeof(IKeyService))]
    [InlineData(typeof(ILicenseGenerator))]
    [InlineData(typeof(ILicenseValidator))]
    [InlineData(typeof(ILicenseStore))]
    public void AddEnigmaLicensingDesktop_RegistersTheLibrarysService_AsASingleton(Type service)
    {
        ServiceCollection services = [];

        services.AddEnigmaLicensingDesktop();

        Assert.Equal(ServiceLifetime.Singleton, DescriptorFor(services, service).Lifetime);
    }

    [Fact]
    public void AddEnigmaLicensingDesktop_ResolvesTheLibrarysServices()
    {
        using ServiceProvider provider = Build();

        Assert.NotNull(provider.GetRequiredService<IKeyService>());
        Assert.NotNull(provider.GetRequiredService<ILicenseGenerator>());
        Assert.NotNull(provider.GetRequiredService<ILicenseValidator>());
        Assert.NotNull(provider.GetRequiredService<ILicenseStore>());
    }

    /// <summary>
    /// The lifetime split that carries the page-state guarantee: the ViewModel outlives the navigation, the
    /// control does not.
    /// </summary>
    /// <param name="view">The page's view.</param>
    /// <param name="viewModel">The page's ViewModel.</param>
    [Theory]
    [InlineData(typeof(GenerateKeysView), typeof(GenerateKeysViewModel))]
    [InlineData(typeof(GenerateLicenseView), typeof(GenerateLicenseViewModel))]
    [InlineData(typeof(ValidateLicenseView), typeof(ValidateLicenseViewModel))]
    [InlineData(typeof(SettingsView), typeof(SettingsViewModel))]
    public void AddEnigmaLicensingDesktop_RegistersPageViewsTransient_AndPageViewModelsSingleton(Type view, Type viewModel)
    {
        ServiceCollection services = [];

        services.AddEnigmaLicensingDesktop();

        Assert.Equal(ServiceLifetime.Transient, DescriptorFor(services, view).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorFor(services, viewModel).Lifetime);
    }

    /// <summary>
    /// The preference seams. The store is registered by factory rather than by type — its path is a plain
    /// string the container cannot supply — so resolving it is the only way to know the factory is right.
    /// </summary>
    [Fact]
    public void ThePreferenceSeams_AreResolvable()
    {
        using ServiceProvider provider = Build();

        Assert.NotNull(provider.GetRequiredService<IAppSettingsStore>());
        Assert.NotNull(provider.GetRequiredService<AppTheming>());
        Assert.NotNull(provider.GetRequiredService<ILicenseProfileStore>());
    }

    /// <summary>
    /// The application's own seams over the library's hosts. Resolving them is the assertion: each wraps a
    /// control-library service that has to be registered for the constructor to be satisfiable at all, so a
    /// wrapper added without its dependency fails here rather than the first time a user clicks a button.
    /// </summary>
    [Fact]
    public void ThePlatformSeams_AreResolvable()
    {
        using ServiceProvider provider = Build();

        Assert.NotNull(provider.GetRequiredService<AppFileDialogs>());
        Assert.NotNull(provider.GetRequiredService<AppNotifications>());
        Assert.NotNull(provider.GetRequiredService<AppBusyOverlay>());
        Assert.NotNull(provider.GetRequiredService<AppClipboard>());
        Assert.NotNull(provider.GetRequiredService<AppAboutDialog>());
    }

    [Fact]
    public void ThePageViewModels_AreResolvable_AndAreTheSameInstanceEveryTime()
    {
        using ServiceProvider provider = Build();

        Assert.Same(provider.GetRequiredService<GenerateKeysViewModel>(), provider.GetRequiredService<GenerateKeysViewModel>());
        Assert.Same(provider.GetRequiredService<GenerateLicenseViewModel>(), provider.GetRequiredService<GenerateLicenseViewModel>());
        Assert.Same(provider.GetRequiredService<ValidateLicenseViewModel>(), provider.GetRequiredService<ValidateLicenseViewModel>());
        Assert.Same(provider.GetRequiredService<SettingsViewModel>(), provider.GetRequiredService<SettingsViewModel>());
    }

    /// <summary>
    /// Calling the app's registration twice is what a mis-edited startup looks like. The library's own
    /// extension uses <c>TryAdd</c> throughout, so nothing it registers may be duplicated by the second call.
    /// </summary>
    [Fact]
    public void AddEnigmaLicensingDesktop_Twice_LeavesTheLibrarysServicesRegisteredOnce()
    {
        ServiceCollection services = [];

        services.AddEnigmaLicensingDesktop();
        services.AddEnigmaLicensingDesktop();

        // DescriptorFor asserts there is exactly one; that assertion is the whole test.
        DescriptorFor(services, typeof(IKeyService));
        DescriptorFor(services, typeof(ILicenseGenerator));
        DescriptorFor(services, typeof(ILicenseValidator));
        DescriptorFor(services, typeof(ILicenseStore));
    }

    private static ServiceProvider Build()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddEnigmaAvaloniaDesktop();
        services.AddEnigmaLicensingDesktop();

        return services.BuildServiceProvider();
    }

    private static ServiceDescriptor DescriptorFor(IServiceCollection services, Type service)
        => Assert.Single(services, descriptor => descriptor.ServiceType == service);
}
