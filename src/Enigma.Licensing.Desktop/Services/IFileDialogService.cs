using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// Asks the user where to write a document, and reports the path they chose.
/// </summary>
/// <remarks>
/// <para>
/// The application's own seam over the control library's picker service. It exists for two reasons the
/// library's interface cannot serve on its own: its methods hand back Avalonia <c>IStorageFile</c> handles
/// (so a ViewModel test would have to fake a storage item to answer a picker), and the plain-path overloads
/// that avoid them are <b>extension methods</b>, which no test double can intercept. A ViewModel therefore
/// depends on this — one string in, one string or <see langword="null"/> out.
/// </para>
/// <para>
/// Every document this application writes is JSON — key files, licence envelopes, licence profiles — so the
/// file-type choice is the implementation's business rather than a parameter of every call.
/// </para>
/// </remarks>
public interface IFileDialogService
{
    /// <summary>
    /// Shows a save dialog and returns the chosen path.
    /// </summary>
    /// <param name="title">The dialog's title — what the user is choosing a destination for.</param>
    /// <param name="suggestedFileName">The name to offer, extension included.</param>
    /// <param name="suggestedStartLocation">
    /// The directory to open in, or <see langword="null"/> for the platform default. A path that does not
    /// exist is not an error: the dialog simply opens where it would have anyway.
    /// </param>
    /// <returns>The chosen path, or <see langword="null"/> if the user cancelled.</returns>
    Task<string?> ShowSaveFileDialogAsync(
        string title,
        string suggestedFileName,
        string? suggestedStartLocation = null);

    /// <summary>
    /// Shows an open dialog for a single file and returns the chosen path.
    /// </summary>
    /// <param name="title">The dialog's title — what the user is choosing a file for.</param>
    /// <param name="suggestedStartLocation">
    /// The directory to open in, or <see langword="null"/> for the platform default. As on
    /// <see cref="ShowSaveFileDialogAsync"/>, a path that does not exist is not an error.
    /// </param>
    /// <returns>
    /// The chosen path, or <see langword="null"/> if the user cancelled — or, on the platforms where
    /// that is possible, picked something with no local path at all. Both are the same thing to a
    /// caller that can only work with a file name, so they are reported the same way.
    /// </returns>
    Task<string?> ShowOpenFileDialogAsync(
        string title,
        string? suggestedStartLocation = null);
}
