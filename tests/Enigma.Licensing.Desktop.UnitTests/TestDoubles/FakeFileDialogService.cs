using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the file dialogs: each answers with whatever it was primed with — including nothing, which
/// is what a cancelled dialog looks like — and keeps every request, so a test can assert what the page
/// offered the user as a file name and a starting folder.
/// </summary>
/// <remarks>
/// Saving and opening keep separate queues. A page that does both — the generate page picks a key, loads a
/// profile and saves two documents — would otherwise have its answers decided by the order the tests happen
/// to prime them in, which is exactly the kind of coupling a double should not add.
/// </remarks>
internal sealed class FakeFileDialogService : IFileDialogService
{
    private readonly Queue<string?> _saveAnswers = new();
    private readonly Queue<string?> _openAnswers = new();
    private readonly List<SaveRequest> _requests = [];
    private readonly List<OpenRequest> _openRequests = [];

    /// <summary>Gets every save request made, in order.</summary>
    internal IReadOnlyList<SaveRequest> Requests => _requests;

    /// <summary>Gets every open request made, in order.</summary>
    internal IReadOnlyList<OpenRequest> OpenRequests => _openRequests;

    /// <summary>Primes the next answer from a save dialog.</summary>
    /// <param name="path">The path the dialog returns, or <see langword="null"/> for a cancellation.</param>
    internal void Answer(string? path) => _saveAnswers.Enqueue(path);

    /// <summary>Primes the next answer from an open dialog.</summary>
    /// <param name="path">The path the dialog returns, or <see langword="null"/> for a cancellation.</param>
    internal void AnswerOpen(string? path) => _openAnswers.Enqueue(path);

    /// <inheritdoc/>
    public Task<string?> ShowSaveFileDialogAsync(
        string title,
        string suggestedFileName,
        string? suggestedStartLocation = null)
    {
        _requests.Add(new SaveRequest(title, suggestedFileName, suggestedStartLocation));

        return Task.FromResult(_saveAnswers.Count > 0 ? _saveAnswers.Dequeue() : null);
    }

    /// <inheritdoc/>
    public Task<string?> ShowOpenFileDialogAsync(string title, string? suggestedStartLocation = null)
    {
        _openRequests.Add(new OpenRequest(title, suggestedStartLocation));

        return Task.FromResult(_openAnswers.Count > 0 ? _openAnswers.Dequeue() : null);
    }

    /// <summary>One save dialog the page raised.</summary>
    /// <param name="Title">What the dialog was titled.</param>
    /// <param name="SuggestedFileName">The file name offered.</param>
    /// <param name="SuggestedStartLocation">The folder offered, if any.</param>
    internal sealed record SaveRequest(string Title, string SuggestedFileName, string? SuggestedStartLocation);

    /// <summary>One open dialog the page raised.</summary>
    /// <param name="Title">What the dialog was titled.</param>
    /// <param name="SuggestedStartLocation">The folder offered, if any.</param>
    internal sealed record OpenRequest(string Title, string? SuggestedStartLocation);
}
