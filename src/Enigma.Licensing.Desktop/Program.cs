using System;
using Avalonia;

namespace Enigma.Licensing.Desktop;

/// <summary>
/// Application entry point. Stays synchronous: the Avalonia classic desktop lifetime owns the message
/// loop, and the <see cref="Microsoft.Extensions.Hosting.IHost"/> is started from
/// <see cref="App.OnFrameworkInitializationCompleted"/>.
/// </summary>
public static class Program
{
    /// <summary>Starts the Avalonia desktop lifetime.</summary>
    /// <param name="args">Command-line arguments passed through to Avalonia.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the Avalonia application. Also used by the visual designer.</summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
