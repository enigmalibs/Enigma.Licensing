using System;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The safety net: every exception nothing else caught ends up here and is logged, with where it was caught.
/// </summary>
/// <remarks>
/// A safety net, not the error handling: a failure the application knows about is said where it happens, in
/// that page's own words — a verdict card, an info bar — and never reaches this.
/// </remarks>
public interface IUnhandledErrorReporter
{
    /// <summary>Logs an exception, from any thread.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    void Report(Exception exception, UnhandledErrorSource source);

    /// <summary>Says the application is closing: from here on a cancellation is never worth a word.</summary>
    void ShutdownStarted();
}
