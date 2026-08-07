using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// The wrapper that turns "say what is running" into the card the control library's overlay shows.
/// </summary>
/// <remarks>
/// The card is built in code, so nothing in the build checks its shape — a control tree assembled wrongly
/// compiles perfectly and shows a user an empty grey box while a key is generated. These tests walk it
/// instead. They stand up no Avalonia platform: constructing controls needs none, and nothing here measures,
/// templates or renders one.
/// </remarks>
public sealed class BusyOverlayServiceTests
{
    [Fact]
    public async Task ShowAsync_HandsTheOverlayACardCarryingBothLines()
    {
        FakeOverlayService overlay = new();
        BusyOverlayService busy = new(overlay);

        await busy.ShowAsync("Generating the key pair", "This can take a while.");

        Border card = Assert.IsType<Border>(overlay.Content);
        StackPanel content = Assert.IsType<StackPanel>(card.Child);

        Assert.Collection(
            content.Children,
            child => Assert.Equal("Generating the key pair", Assert.IsType<TextBlock>(child).Text),
            child => Assert.True(Assert.IsType<ProgressBar>(child).IsIndeterminate),
            child => Assert.Equal("This can take a while.", Assert.IsType<TextBlock>(child).Text));
    }

    /// <summary>
    /// The card is styled by the application's own classes rather than by properties set here, so it follows
    /// the colour variant like every other surface. A missing class is an unpainted box.
    /// </summary>
    [Fact]
    public async Task TheCard_IsStyledByTheApplicationsOwnClasses()
    {
        FakeOverlayService overlay = new();
        BusyOverlayService busy = new(overlay);

        await busy.ShowAsync("A title", "A message");

        Border card = Assert.IsType<Border>(overlay.Content);
        Assert.Contains("card", card.Classes);

        StackPanel content = Assert.IsType<StackPanel>(card.Child);
        Assert.Contains("h2", ((TextBlock)content.Children[0]).Classes);
        Assert.Contains("hint", ((TextBlock)content.Children[2]).Classes);
    }

    [Fact]
    public async Task HideAsync_TakesTheOverlayAway()
    {
        FakeOverlayService overlay = new();
        BusyOverlayService busy = new(overlay);

        await busy.HideAsync();

        Assert.Equal(1, overlay.Hides);
    }

    /// <summary>A fresh card each time, so a second operation cannot inherit the first one's words.</summary>
    [Fact]
    public async Task ShowAsync_BuildsANewCardEachTime()
    {
        FakeOverlayService overlay = new();
        BusyOverlayService busy = new(overlay);

        await busy.ShowAsync("First", "One");
        Control first = overlay.Content!;

        await busy.ShowAsync("Second", "Two");

        Assert.NotSame(first, overlay.Content);
    }

    [Fact]
    public void Constructor_RejectsAMissingOverlay()
    {
        Assert.Throws<ArgumentNullException>(() => new BusyOverlayService(null!));
    }
}
