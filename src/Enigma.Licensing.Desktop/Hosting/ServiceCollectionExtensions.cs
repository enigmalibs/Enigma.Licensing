using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.Settings;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LibraryNavigation = Enigma.Avalonia.Desktop.Services.INavigationService;

namespace Enigma.Licensing.Desktop.Hosting;

/// <summary>
/// Registers everything the application itself resolves.
/// </summary>
/// <remarks>
/// It is an extension rather than a private method on <see cref="App"/> so that a test can assert the
/// wiring without standing up Avalonia: the lifetimes here are load-bearing, and getting one wrong is
/// invisible until a user loses work. The control library's own services are registered separately by
/// <see cref="EnigmaAvaloniaServiceCollectionExtensions.AddEnigmaAvaloniaDesktop"/> — see that file for why
/// the two cannot share one.
/// </remarks>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the licensing library's services, the shell, the four pages and the platform services
        /// behind them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="EnigmaLicensingServiceCollectionExtensions.AddEnigmaLicensing"/> is called rather than
        /// re-registering the library's four services by hand: the library owns its own lifetimes and its
        /// own composition — notably the explicit factory that decides which validator the store validates
        /// with — and this application has no reason to have an opinion about either. It uses
        /// <c>TryAddSingleton</c> throughout, so calling it here does not fight anything registered above.
        /// </para>
        /// <para>
        /// <b>Page ViewModels are singletons and their views transient:</b> each ViewModel holds work in
        /// progress — a half-filled licence form, a chosen key file — that must survive leaving the page and
        /// coming back, while a fresh control per navigation is what keeps the visual tree from leaking.
        /// Swapping either lifetime looks like a working app and silently breaks one of those two things.
        /// </para>
        /// </remarks>
        public void AddEnigmaLicensingDesktop()
        {
            // The library: IKeyService, ILicenseGenerator, ILicenseValidator and ILicenseStore, all as the
            // library itself registers them.
            services.AddEnigmaLicensing();

            // The shell window. Resolved rather than constructed, because the control library's services
            // need this very window's hosts and its storage provider.
            services.AddSingleton<MainWindow>();

            // The preference file and the variant it selects. The store is built by hand because its path is
            // a plain string the container has no way to supply — and passing it in is what lets a test point
            // the same class at a temporary file.
            services.AddSingleton<IAppSettingsStore>(provider => new AppSettingsStore(
                provider.GetRequiredService<ILogger<AppSettingsStore>>(),
                AppSettingsStore.DefaultFilePath));
            services.AddSingleton<IThemeService, ThemeService>();

            // Pages: the view fresh each time, the ViewModel kept.
            services.AddTransient<GenerateKeysView>();
            services.AddTransient<GenerateLicenseView>();
            services.AddTransient<ValidateLicenseView>();
            services.AddTransient<SettingsView>();
            services.AddSingleton<GenerateKeysViewModel>();
            services.AddSingleton<GenerateLicenseViewModel>();
            services.AddSingleton<ValidateLicenseViewModel>();
            services.AddSingleton<SettingsViewModel>();

            // The shell's rail icons are passed in rather than resolved by the container: turning a Phosphor
            // glyph into a Geometry needs Avalonia's platform render interface, so resolving them from a type
            // would tie this registration to a live rendering platform. This factory runs at first
            // resolution, by which time the app has one.
            services.AddSingleton(provider => new MainWindowViewModel(
                provider,
                provider.GetRequiredService<LibraryNavigation>(),
                AppIcons.Keys,
                AppIcons.Generate,
                AppIcons.Validate,
                AppIcons.Settings));
        }
    }
}
