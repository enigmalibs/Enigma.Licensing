using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// Hooks the process-wide unhandled-exception events and hands what they catch to the safety net
/// (<see cref="IUnhandledErrorReporter"/>).
/// </summary>
/// <remarks>
/// <para>
/// Static because what it adapts is: <see cref="AppDomain.UnhandledException"/>,
/// <see cref="TaskScheduler.UnobservedTaskException"/> and the UI dispatcher's
/// <see cref="Dispatcher.UnhandledException"/> belong to the process, and they fire before the container
/// exists. What comes before the reporter is attached is kept, a bounded number of times, and handed over once
/// it is; until then it is written to <see cref="Trace"/>.
/// </para>
/// <para>
/// An exception that reaches <see cref="AppDomain.UnhandledException"/> escaped on a thread with nothing
/// above it, and .NET ends the process after the event whatever any handler does. It is logged in full, and
/// that is all that can be done.
/// </para>
/// </remarks>
public static class UnhandledExceptionHooks
{
    /// <summary>How many reports are kept for the reporter before it is attached.</summary>
    internal const int EarlyLimit = 32;

    private static readonly object Gate = new();
    private static readonly Queue<(Exception Exception, UnhandledErrorSource Source)> Early = new();

    private static IUnhandledErrorReporter? _reporter;
    private static bool _installed;
    private static bool _dispatcherInstalled;

    /// <summary>
    /// Hooks <see cref="AppDomain.UnhandledException"/> and <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// The first thing <c>Main</c> does; calling it again does nothing.
    /// </summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
        }

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Hooks the UI dispatcher's <see cref="Dispatcher.UnhandledException"/>, which a command, an
    /// <c>async void</c> handler or a posted callback that throws reaches. Called once the platform is set up —
    /// touching the dispatcher before is not safe — and before anything else the application does; calling it
    /// again does nothing.
    /// </summary>
    public static void InstallDispatcher()
    {
        lock (Gate)
        {
            if (_dispatcherInstalled)
            {
                return;
            }

            _dispatcherInstalled = true;
        }

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>
    /// Hands over to the safety net: what was reported before it existed first, then everything from here on.
    /// </summary>
    /// <param name="reporter">The safety net.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reporter"/> is <see langword="null"/>.</exception>
    public static void Attach(IUnhandledErrorReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);

        List<(Exception Exception, UnhandledErrorSource Source)> early;

        lock (Gate)
        {
            _reporter = reporter;
            early = [.. Early];
            Early.Clear();
        }

        foreach ((Exception exception, UnhandledErrorSource source) in early)
        {
            reporter.Report(exception, source);
        }
    }

    /// <summary>
    /// Takes the safety net away again — before the container that owns it is disposed, so a late report does
    /// not log through a disposed logger. Reports are kept and traced again until the next <see cref="Attach"/>.
    /// </summary>
    public static void Detach()
    {
        lock (Gate)
        {
            _reporter = null;
        }
    }

    /// <summary>Reports an exception nothing handled — the one way in for code the container does not build.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        IUnhandledErrorReporter? reporter;

        lock (Gate)
        {
            reporter = _reporter;

            if (reporter is null)
            {
                if (Early.Count < EarlyLimit)
                {
                    Early.Enqueue((exception, source));
                }

                // The type and the message only: a trace listener is not where a stack goes.
                Trace.TraceError(
                    "Unhandled exception before the safety net was ready ({0}): {1}: {2}",
                    source,
                    exception.GetType().FullName,
                    exception.Message);

                return;
            }
        }

        reporter.Report(exception, source);
    }

    /// <summary>Forgets every early report and the attached reporter — for tests, whose process is shared.</summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _reporter = null;
            Early.Clear();
        }
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"An object of type {e.ExceptionObject?.GetType().FullName ?? "null"} was thrown.");

        Report(exception, UnhandledErrorSource.AppDomain);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Report(e.Exception, UnhandledErrorSource.UnobservedTask);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Report(e.Exception, UnhandledErrorSource.Dispatcher);
    }
}
