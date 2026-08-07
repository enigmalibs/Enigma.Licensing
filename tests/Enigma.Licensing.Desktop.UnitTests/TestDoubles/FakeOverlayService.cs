using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.Avalonia.Desktop.Controls;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the control library's overlay service, keeping the control it was handed so a test can look
/// at the card the application built.
/// </summary>
/// <remarks>
/// Distinct from <see cref="FakeBusyOverlayService"/>, which doubles the <i>application's</i> seam for a page
/// to talk to. This one doubles the layer underneath it, which is what the application's own wrapper calls.
/// </remarks>
internal sealed class FakeOverlayService : IOverlayService
{
    /// <summary>Gets the control the last show was given, or <see langword="null"/> if there has been none.</summary>
    internal Control? Content { get; private set; }

    /// <summary>Gets how many times the overlay was taken away.</summary>
    internal int Hides { get; private set; }

    /// <inheritdoc/>
    public void RegisterHost(Overlay presenter)
    {
    }

    /// <inheritdoc/>
    public Task ShowAsync(Control control)
    {
        Content = control;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task HideAsync()
    {
        Hides++;

        return Task.CompletedTask;
    }
}
