using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// Blocks the application behind a progress card while a long operation runs, and takes it away afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The application's own seam over the control library's overlay. The library's service takes an Avalonia
/// <c>Control</c> as its content — building one is exactly what a ViewModel must not do — so a page says
/// what is happening in words and the implementation builds the card.
/// </para>
/// <para>
/// <b>Always hide in a <c>finally</c>.</b> An operation that throws between showing and hiding leaves the
/// scrim up over an application that will never accept another click.
/// </para>
/// </remarks>
public interface IBusyOverlayService
{
    /// <summary>Shows the progress card, with an indeterminate bar.</summary>
    /// <param name="title">What is running, in a few words.</param>
    /// <param name="message">Why it may take a while — the line that stops a user thinking the app hung.</param>
    /// <returns>A task that completes once the overlay is showing.</returns>
    Task ShowAsync(string title, string message);

    /// <summary>Takes the overlay away. Doing so when nothing is showing is not an error.</summary>
    /// <returns>A task that completes once the overlay is gone.</returns>
    Task HideAsync();
}
