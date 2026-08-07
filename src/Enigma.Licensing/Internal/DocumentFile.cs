using System;
using System.IO;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The file plumbing behind every path overload in the library: the argument guard, and opening a
/// document for reading or writing.
/// </summary>
/// <remarks>
/// <para>
/// One place, so a key file and a license file behave identically. In particular <b>writing creates the
/// parent directory</b>. That is the point of the path overloads: the predecessor library made every
/// caller do it, and forgetting produced a <see cref="DirectoryNotFoundException"/> at the end of a
/// flow that had already generated a key or signed a license. Reading deliberately does <i>not</i>
/// create anything — a missing file there is a real failure and propagates.
/// </para>
/// </remarks>
internal static class DocumentFile
{
    // The default FileStream buffer; large enough that a key or license file is a single read or write.
    private const int BufferSize = 4096;

    /// <summary>
    /// Rejects a path that could not name a file.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or only whitespace.</exception>
    internal static void RequirePath(string path, string parameterName)
    {
        if (path is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (path.Trim().Length == 0)
        {
            throw new ArgumentException("The path must not be empty.", parameterName);
        }
    }

    /// <summary>Opens an existing document for reading.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>An asynchronous read stream the caller owns.</returns>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="path"/>.</exception>
    /// <exception cref="IOException">The file could not be opened.</exception>
    internal static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

    /// <summary>
    /// Opens a document for writing, creating any missing parent directories and truncating an
    /// existing file.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <returns>An asynchronous write stream the caller owns.</returns>
    /// <exception cref="IOException">The directory or file could not be created.</exception>
    internal static FileStream Create(string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
    }
}
