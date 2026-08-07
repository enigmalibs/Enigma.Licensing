using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="INotificationService"/> backed by the control library's info bar.
/// </summary>
/// <remarks>
/// <para>
/// The info bar is a single shared host that resets itself between uses, so the last thing reported is the
/// only thing showing — which is the behaviour wanted here: a page reports one outcome per action.
/// </para>
/// <para>
/// <b>The library's <c>ShowAsync</c> completes when the bar is dismissed, not when it appears</b> — its task
/// is the user's click on the close button. That is why nothing here awaits it. Awaiting would hold the
/// caller open until somebody noticed the notification: a command would stay "running" with its button
/// disabled, and — because a report is usually the last thing a command does — the <c>finally</c> that wipes
/// a password copy from memory would not run until the user cleared a green banner. This is the one seam
/// where the library's shape and this application's meaning of "show" genuinely differ.
/// </para>
/// </remarks>
internal sealed class NotificationService : INotificationService
{
    private readonly IInfoBarService _infoBar;

    /// <summary>Initializes a new instance of the <see cref="NotificationService"/> class.</summary>
    /// <param name="infoBar">The control library's info-bar service, over the window's host control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="infoBar"/> is <see langword="null"/>.</exception>
    public NotificationService(IInfoBarService infoBar)
    {
        ArgumentNullException.ThrowIfNull(infoBar);

        _infoBar = infoBar;
    }

    /// <inheritdoc/>
    public Task ShowSuccessAsync(string title, string message) =>
        ShowAsync(title, message, InfoBarSeverity.Success);

    /// <inheritdoc/>
    public Task ShowErrorAsync(string title, string message) =>
        ShowAsync(title, message, InfoBarSeverity.Error);

    private Task ShowAsync(string title, string message, InfoBarSeverity severity)
    {
        Task dismissal = _infoBar.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = message;
            bar.Severity = severity;
        });

        // Handed back only when it is already finished, which in practice means it failed before it ever
        // reached the host — an info bar whose host was never registered at startup. A pending task is a bar
        // waiting to be dismissed, and that is not this caller's business; it can only ever be completed by
        // that dismissal, so dropping it observes nothing and loses nothing.
        return dismissal.IsCompleted ? dismissal : Task.CompletedTask;
    }
}
