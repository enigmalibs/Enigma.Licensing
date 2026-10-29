using System;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IUnhandledErrorReporter"/> that logs every exception, as Enigma.GitClient's safety net does.
/// </summary>
/// <remarks>
/// <para>
/// The log is written at once, on whatever thread reported, because that thread may be about to end the
/// process. What happens next is decided on the UI thread, by <see cref="UnhandledErrorPolicy"/>.
/// </para>
/// <para>
/// Nothing is shown yet: a queued batch is opened and closed at once, so every later error does not merge into
/// a batch that never closes, and the repeat window already applies to the log.
/// </para>
/// </remarks>
internal sealed class UnhandledErrorReporter : IUnhandledErrorReporter
{
    private readonly ILogger<UnhandledErrorReporter> _logger;
    private readonly UnhandledErrorPolicy _policy;
    private readonly Action<Action> _post;

    /// <summary>Initializes a new instance of the <see cref="UnhandledErrorReporter"/> class.</summary>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public UnhandledErrorReporter(TimeProvider time, ILogger<UnhandledErrorReporter> logger)
        : this(time, logger, action => Dispatcher.UIThread.Post(action))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledErrorReporter"/> class with its own UI thread — the
    /// thing a test process has no platform for.
    /// </summary>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    /// <param name="post">Runs an action on the UI thread — a test runs it in place.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal UnhandledErrorReporter(TimeProvider time, ILogger<UnhandledErrorReporter> logger, Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(post);

        _logger = logger;
        _post = post;
        _policy = new UnhandledErrorPolicy(time);
    }

    /// <summary>Gets the policy, for a test to read what it decided.</summary>
    internal UnhandledErrorPolicy Policy => _policy;

    /// <inheritdoc/>
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_policy.IsIgnoredCancellation(exception))
        {
            _logger.LogDebug(exception, "A cancellation reached the safety net ({Source}); not an error.", source);

            return;
        }

        _logger.LogError(exception, "An unhandled exception reached the safety net ({Source}).", source);

        _post(() => Offer(exception));
    }

    /// <inheritdoc/>
    public void ShutdownStarted() => _policy.IsShuttingDown = true;

    private void Offer(Exception exception)
    {
        switch (_policy.Offer(exception))
        {
            case UnhandledErrorDecision.Queue:
                // No dialog to show it in yet: opened and closed at once, which starts its repeat window.
                _policy.BeginShow();
                _policy.EndShow();
                break;
            case UnhandledErrorDecision.Suppress:
                _logger.LogDebug("The same error was seen less than {Window} ago.", UnhandledErrorPolicy.RepeatWindow);
                break;
            default:
                break;
        }
    }
}
