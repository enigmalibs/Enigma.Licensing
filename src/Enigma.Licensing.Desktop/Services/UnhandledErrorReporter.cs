using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Desktop.Views;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IUnhandledErrorReporter"/> that logs every exception and shows it in the window's content
/// dialog, as Enigma.GitClient's safety net does.
/// </summary>
/// <remarks>
/// <para>
/// The log is written at once, on whatever thread reported, because that thread may be about to end the
/// process. What is shown is decided on the UI thread, by <see cref="UnhandledErrorPolicy"/>.
/// </para>
/// <para>
/// The dialog is shown on the window's one dialog host, which About shares: showing it over another dialog
/// would replace that dialog's content, so it waits until the host is free, and until the host is registered
/// at all. The busy overlay needs no waiting — <c>MainWindow</c> stacks the dialog host above it. If the
/// dialog itself fails, that is logged and no dialog is tried again in this session: the net must not loop on
/// itself.
/// </para>
/// </remarks>
internal sealed class UnhandledErrorReporter : IUnhandledErrorReporter
{
    /// <summary>The warning amber of the reference dialog's icon.</summary>
    private static readonly Color WarningColor = Color.FromRgb(0xE8, 0xA3, 0x3D);

    private readonly IContentDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly ILogger<UnhandledErrorReporter> _logger;
    private readonly UnhandledErrorPolicy _policy;
    private readonly Action<Action> _post;
    private readonly Func<UnhandledErrorViewModel, Control> _createView;
    private readonly Func<Geometry?> _warningIcon;

    private ContentDialog? _host;
    private ContentDialog? _waitingFor;
    private bool _presenting;
    private bool _broken;

    /// <summary>Initializes a new instance of the <see cref="UnhandledErrorReporter"/> class.</summary>
    /// <param name="dialogs">Shows the dialog on the window's host.</param>
    /// <param name="clipboard">Copies the details for a bug report.</param>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public UnhandledErrorReporter(
        IContentDialogService dialogs,
        IClipboardService clipboard,
        TimeProvider time,
        ILogger<UnhandledErrorReporter> logger)
        : this(
            dialogs,
            clipboard,
            time,
            logger,
            action => Dispatcher.UIThread.Post(action),
            viewModel => new UnhandledErrorView { DataContext = viewModel },
            () => AppIcons.Warning)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledErrorReporter"/> class with its own UI thread, view
    /// and icon — the three things a test process has no platform for.
    /// </summary>
    /// <param name="dialogs">Shows the dialog on the window's host.</param>
    /// <param name="clipboard">Copies the details for a bug report.</param>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    /// <param name="post">Runs an action on the UI thread — a test runs it in place.</param>
    /// <param name="createView">Builds the dialog's content for its ViewModel.</param>
    /// <param name="warningIcon">Resolves the dialog's icon.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal UnhandledErrorReporter(
        IContentDialogService dialogs,
        IClipboardService clipboard,
        TimeProvider time,
        ILogger<UnhandledErrorReporter> logger,
        Action<Action> post,
        Func<UnhandledErrorViewModel, Control> createView,
        Func<Geometry?> warningIcon)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(createView);
        ArgumentNullException.ThrowIfNull(warningIcon);

        _dialogs = dialogs;
        _clipboard = clipboard;
        _logger = logger;
        _post = post;
        _createView = createView;
        _warningIcon = warningIcon;
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

    /// <inheritdoc/>
    public void RegisterHost(ContentDialog host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;

        // Whatever was reported before the window existed has waited for exactly this.
        _ = PresentAsync();
    }

    private void Offer(Exception exception)
    {
        switch (_policy.Offer(exception))
        {
            case UnhandledErrorDecision.Queue:
                _ = PresentAsync();
                break;
            case UnhandledErrorDecision.Suppress:
                _logger.LogDebug("The same error was shown less than {Window} ago; it is not shown again.", UnhandledErrorPolicy.RepeatWindow);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Shows the pending batch, once the host is registered and free. Never throws: nothing awaits it.
    /// </summary>
    private async Task PresentAsync()
    {
        if (_broken || _presenting || _policy.Pending is null || _host is not { } host)
        {
            return;
        }

        if (host.IsOpen)
        {
            WaitFor(host);

            return;
        }

        if (_policy.BeginShow() is not { } batch)
        {
            return;
        }

        _presenting = true;

        try
        {
            Control view = _createView(new UnhandledErrorViewModel(batch, _clipboard));

            await _dialogs.ShowAsync(dialog =>
            {
                dialog.Title = "Something went wrong";
                dialog.Content = view;
                dialog.CloseButtonText = "Close";
                dialog.DefaultButton = DefaultButton.Close;
                dialog.IconData = _warningIcon();
                dialog.IconBrush = new SolidColorBrush(WarningColor);
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // Logged, never reported: a dialog that cannot be shown would otherwise report itself again and
            // again.
            _broken = true;
            _logger.LogError(exception, "The safety net's dialog could not be shown; later errors are only logged.");
        }

        _presenting = false;
        _policy.EndShow();

        if (_policy.Pending is not null)
        {
            _ = PresentAsync();
        }
    }

    /// <summary>Waits for the dialog on the host to be closed, then tries again.</summary>
    private void WaitFor(ContentDialog host)
    {
        if (ReferenceEquals(_waitingFor, host))
        {
            return;
        }

        _waitingFor = host;
        host.Closed += OnWaitedClosed;
    }

    private void OnWaitedClosed(object? sender, DialogResult e)
    {
        if (sender is ContentDialog host)
        {
            host.Closed -= OnWaitedClosed;
        }

        _waitingFor = null;

        // Posted: the closed dialog's own continuation runs first, and may ask its next question at once.
        _post(() => _ = PresentAsync());
    }
}
