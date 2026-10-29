using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the control library's dialog service: each showing configures a real
/// <see cref="ContentDialog"/> — constructing one needs no platform — and stays open until the test closes it.
/// </summary>
internal sealed class FakeContentDialogService : IContentDialogService
{
    private readonly List<ContentDialog> _shown = [];
    private readonly Queue<TaskCompletionSource<DialogResult>> _open = new();

    /// <summary>Gets every dialog shown, configured as the caller left it, in order.</summary>
    internal IReadOnlyList<ContentDialog> Shown => _shown;

    /// <summary>Gets or sets the exception the next showing throws instead of opening, if any.</summary>
    internal Exception? FailWith { get; set; }

    /// <inheritdoc/>
    public void RegisterHost(ContentDialog dialog)
    {
    }

    /// <inheritdoc/>
    public Task<DialogResult> ShowMessageAsync(string title, string message, string closeButtonText = "OK")
        => throw new NotSupportedException("Not used by the code under test.");

    /// <inheritdoc/>
    public Task<DialogResult> ShowAsync(Action<ContentDialog> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (FailWith is { } failure)
        {
            return Task.FromException<DialogResult>(failure);
        }

        ContentDialog dialog = new();
        configure(dialog);
        _shown.Add(dialog);

        TaskCompletionSource<DialogResult> open = new();
        _open.Enqueue(open);

        return open.Task;
    }

    /// <inheritdoc/>
    public Task HideAsync() => Task.CompletedTask;

    /// <summary>Closes the oldest dialog still open, as its Close button would.</summary>
    internal void CloseOldest() => _open.Dequeue().SetResult(DialogResult.Close);
}
