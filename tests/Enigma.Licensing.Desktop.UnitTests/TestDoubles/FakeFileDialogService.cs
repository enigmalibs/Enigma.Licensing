using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the save dialog: it answers with whatever it was primed with — including nothing, which is
/// what a cancelled dialog looks like — and keeps every request, so a test can assert what the page offered
/// the user as a file name and a starting folder.
/// </summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    private readonly Queue<string?> _answers = new();
    private readonly List<SaveRequest> _requests = [];

    /// <summary>Gets every save request made, in order.</summary>
    internal IReadOnlyList<SaveRequest> Requests => _requests;

    /// <summary>Primes the next answer.</summary>
    /// <param name="path">The path the dialog returns, or <see langword="null"/> for a cancellation.</param>
    internal void Answer(string? path) => _answers.Enqueue(path);

    /// <inheritdoc/>
    public Task<string?> ShowSaveFileDialogAsync(
        string title,
        string suggestedFileName,
        string? suggestedStartLocation = null)
    {
        _requests.Add(new SaveRequest(title, suggestedFileName, suggestedStartLocation));

        return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }

    /// <summary>One dialog the page raised.</summary>
    /// <param name="Title">What the dialog was titled.</param>
    /// <param name="SuggestedFileName">The file name offered.</param>
    /// <param name="SuggestedStartLocation">The folder offered, if any.</param>
    internal sealed record SaveRequest(string Title, string SuggestedFileName, string? SuggestedStartLocation);
}
