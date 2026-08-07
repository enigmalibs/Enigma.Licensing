using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IClipboardService"/> backed by the main window's clipboard.
/// </summary>
/// <remarks>
/// <para>
/// The clipboard hangs off a <c>TopLevel</c>, so it is reached through the running application's main
/// window rather than injected — the same route <c>ThemeService</c> takes to the application itself, and for
/// the same reason: a service that needed the window handed to it would have to be wired at startup, and
/// there is nothing to decide at that point.
/// </para>
/// <para>
/// Every way of not having a clipboard — no application, a lifetime that is not a desktop one, a window not
/// yet shown, a platform that declines — ends in the same place: a log line and no copy.
/// </para>
/// </remarks>
internal sealed class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ClipboardService"/> class.</summary>
    /// <param name="logger">Records a copy that had nowhere to go.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public ClipboardService(ILogger<ClipboardService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CopyTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        IClipboard? clipboard =
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;

        if (clipboard is null)
        {
            _logger.LogWarning("The text could not be copied: there is no clipboard.");

            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }
}
