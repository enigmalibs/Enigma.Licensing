using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// Puts a short piece of text on the system clipboard.
/// </summary>
/// <remarks>
/// The application's own seam over Avalonia's clipboard. What it is for is the values a user has to move
/// somewhere else by hand and cannot afford to mistype — a key fingerprint above all.
/// </remarks>
public interface IClipboardService
{
    /// <summary>Copies <paramref name="text"/> to the clipboard, replacing whatever was there.</summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>A task that completes once the clipboard has been written.</returns>
    /// <remarks>
    /// A clipboard the platform will not give up is <b>not</b> an error: nothing a user copies is worth
    /// failing an operation over, so the copy is dropped and recorded in the log.
    /// </remarks>
    Task CopyTextAsync(string text);
}
