using Enigma.Avalonia.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Enigma.Licensing.Desktop.Hosting;

/// <summary>
/// Registers the <c>Enigma.Avalonia.Desktop</c> control library's services.
/// </summary>
/// <remarks>
/// <para>
/// The library ships no registration extension of its own, so this is it. All six are singletons: three of
/// them own a host control that is handed over once at startup, two own the window's storage provider, and
/// the navigation service owns the rail's items and the page showing — none of that survives being rebuilt
/// per resolution.
/// </para>
/// <para>
/// This lives in its own file, apart from the application's own registrations, because the two
/// <c>Services</c> namespaces will eventually declare types of the same name — the library already has an
/// <c>IFileDialogService</c>, and this application will wrap it — and a single file importing both would
/// not compile (<c>CS0104</c>). Here only the library's namespace is imported, so every name below is
/// unambiguously the library's.
/// </para>
/// </remarks>
public static class EnigmaAvaloniaServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the control library's six services, unless the caller has already registered its own.
        /// </summary>
        public void AddEnigmaAvaloniaDesktop()
        {
            services.TryAddSingleton<INavigationService, NavigationService>();
            services.TryAddSingleton<IContentDialogService, ContentDialogService>();
            services.TryAddSingleton<IOverlayService, OverlayService>();
            services.TryAddSingleton<IInfoBarService, InfoBarService>();
            services.TryAddSingleton<IFileDialogService, FileDialogService>();

            // Registered for completeness and, for now, unused: this application picks files, never folders.
            // It costs one object and keeps the library's set whole, so reaching for a folder later is a
            // call, not a wiring change.
            services.TryAddSingleton<IFolderDialogService, FolderDialogService>();
        }
    }
}
