using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the About dialog, counting how often it was asked for.
/// </summary>
internal sealed class FakeAboutDialogService : IAboutDialogService
{
    /// <summary>Gets how many times the dialog was shown.</summary>
    internal int Shown { get; private set; }

    /// <inheritdoc/>
    public Task ShowAsync()
    {
        Shown++;

        return Task.CompletedTask;
    }
}
