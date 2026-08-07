using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the control library's info-bar service, <b>with its real timing</b>: the task it hands back
/// completes when the bar is dismissed, not when it appears.
/// </summary>
/// <remarks>
/// That timing is the whole point of this double. The library's <c>InfoBar.ShowAsync</c> resolves on its
/// <c>Closed</c> event, so anything that awaits it waits for a user. A wrapper that passed that task on
/// would deadlock a command against a banner nobody clicked, and only a double that keeps the task pending
/// can show the difference.
/// </remarks>
internal sealed class FakeInfoBarService : IInfoBarService
{
    private readonly TaskCompletionSource _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets how many bars were shown.</summary>
    internal int Shows { get; private set; }

    /// <summary>Gets or sets a failure to raise instead of showing — what an unregistered host looks like.</summary>
    internal Exception? Fail { get; set; }

    /// <summary>Gets the bar the last show configured, or <see langword="null"/> if none has been.</summary>
    internal InfoBar? Bar { get; private set; }

    /// <inheritdoc/>
    public void RegisterHost(InfoBar infoBar) => Bar = infoBar;

    /// <inheritdoc/>
    public Task ShowAsync(Action<InfoBar>? configure = null)
    {
        Shows++;

        if (Fail is not null)
        {
            return Task.FromException(Fail);
        }

        Bar = new InfoBar();
        configure?.Invoke(Bar);

        // Never completed by this double: a bar that nobody dismisses.
        return _dismissed.Task;
    }

    /// <inheritdoc/>
    public Task HideAsync() => Task.CompletedTask;
}
