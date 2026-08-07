using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the system clipboard, keeping everything copied to it.
/// </summary>
internal sealed class FakeClipboardService : IClipboardService
{
    private readonly List<string> _copied = [];

    /// <summary>Gets every value copied, in order.</summary>
    internal IReadOnlyList<string> Copied => _copied;

    /// <inheritdoc/>
    public Task CopyTextAsync(string text)
    {
        _copied.Add(text);

        return Task.CompletedTask;
    }
}
