using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the blocking overlay, keeping a running count of shows and hides.
/// </summary>
/// <remarks>
/// <see cref="IsShowing"/> is the assertion that matters: an overlay left up over a failed operation is an
/// application a user has to kill, so every path through a long command has to end with it down.
/// </remarks>
internal sealed class FakeBusyOverlayService : IBusyOverlayService
{
    private readonly List<string> _titles = [];

    /// <summary>Gets the title of every overlay shown, in order.</summary>
    internal IReadOnlyList<string> Titles => _titles;

    /// <summary>Gets how many times the overlay was taken away.</summary>
    internal int Hides { get; private set; }

    /// <summary>Gets a value indicating whether the overlay is up right now.</summary>
    internal bool IsShowing => _titles.Count > Hides;

    /// <inheritdoc/>
    public Task ShowAsync(string title, string message)
    {
        _titles.Add(title);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task HideAsync()
    {
        Hides++;

        return Task.CompletedTask;
    }
}
