using System;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The safety net: every exception nothing else caught ends up here, is logged, and is shown in a dialog that
/// says the application is still running.
/// </summary>
/// <remarks>
/// A safety net, not the error handling: a failure the application knows about is said where it happens, in
/// that page's own words — a verdict card, an info bar — and never reaches this.
/// </remarks>
public interface IUnhandledErrorReporter
{
    /// <summary>Logs an exception and shows it, from any thread.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    void Report(Exception exception, UnhandledErrorSource source);

    /// <summary>Says the application is closing: from here on a cancellation is never worth a word.</summary>
    void ShutdownStarted();

    /// <summary>
    /// Hands over the window's dialog host, on the UI thread, once. What was reported before waits for it; the
    /// dialog is shown through the content dialog service, which shows on this same host, so this is how the
    /// net knows whether another dialog already holds it.
    /// </summary>
    /// <param name="host">The <c>ContentDialog</c> the window registered with the content dialog service.</param>
    void RegisterHost(ContentDialog host);
}
