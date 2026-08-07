using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Enigma.Avalonia.Desktop.Services;
using LibraryFileDialogs = Enigma.Avalonia.Desktop.Services.IFileDialogService;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IFileDialogService"/> backed by the control library's picker service.
/// </summary>
/// <remarks>
/// <para>
/// The plain <c>Enigma.Avalonia.Desktop.Services</c> import is what makes the library's string-path
/// extension overloads visible; the library's own interface is reached through an alias, because this file
/// lives in a namespace that declares a type of the same name.
/// </para>
/// <para>
/// The picker service throws <see cref="InvalidOperationException"/> until it has been handed the window's
/// storage provider, which <c>App</c> does before the window is shown. Nothing here guards against that: a
/// missing handover is a startup fault, and swallowing it would turn "the pickers do not work" into "the
/// buttons do nothing".
/// </para>
/// </remarks>
internal sealed class FileDialogService : IFileDialogService
{
    /// <summary>
    /// The one file type offered. Every document this application writes is a JSON file, so the filter is
    /// fixed here rather than passed in — and <c>All</c> follows it so a user who wants another extension is
    /// not blocked by the picker.
    /// </summary>
    private static readonly IReadOnlyList<FilePickerFileType> JsonDocuments =
    [
        new FilePickerFileType("JSON documents")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"],
        },
        FilePickerFileTypes.All,
    ];

    private readonly LibraryFileDialogs _dialogs;

    /// <summary>Initializes a new instance of the <see cref="FileDialogService"/> class.</summary>
    /// <param name="dialogs">The control library's picker service, over the window's storage provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialogs"/> is <see langword="null"/>.</exception>
    public FileDialogService(LibraryFileDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _dialogs = dialogs;
    }

    /// <inheritdoc/>
    public Task<string?> ShowSaveFileDialogAsync(
        string title,
        string suggestedFileName,
        string? suggestedStartLocation = null) =>
        _dialogs.ShowSaveFileDialogAsync(
            title: title,
            suggestedStartLocation: suggestedStartLocation,
            suggestedFileName: suggestedFileName,
            defaultExtension: "json",
            showOverwritePrompt: true,
            fileTypeChoices: JsonDocuments);
}
