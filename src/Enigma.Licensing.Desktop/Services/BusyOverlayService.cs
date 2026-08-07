using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IBusyOverlayService"/> backed by the control library's overlay.
/// </summary>
/// <remarks>
/// <para>
/// The card is built in code rather than declared as a control of its own, because it has no state, no
/// template variants and exactly one caller shape: a title, a line of explanation and a bar that spins.
/// A <c>TemplatedControl</c> would mean a class, a control theme and a dictionary merged into
/// <c>App.axaml</c> to say the same thing.
/// </para>
/// <para>
/// It is styled by the application's own <c>card</c>, <c>h2</c> and <c>hint</c> classes, so it follows the
/// colour variant like everything else — the classes resolve their brushes through <c>DynamicResource</c>,
/// and the styles are declared application-wide. The host <c>Overlay</c> centres this content and draws the
/// scrim behind it.
/// </para>
/// </remarks>
internal sealed class BusyOverlayService : IBusyOverlayService
{
    private readonly IOverlayService _overlay;

    /// <summary>Initializes a new instance of the <see cref="BusyOverlayService"/> class.</summary>
    /// <param name="overlay">The control library's overlay service, over the window's host control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overlay"/> is <see langword="null"/>.</exception>
    public BusyOverlayService(IOverlayService overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        _overlay = overlay;
    }

    /// <inheritdoc/>
    public Task ShowAsync(string title, string message) => _overlay.ShowAsync(BuildCard(title, message));

    /// <inheritdoc/>
    public Task HideAsync() => _overlay.HideAsync();

    /// <summary>Builds the progress card the overlay shows.</summary>
    /// <param name="title">What is running.</param>
    /// <param name="message">Why it may take a while.</param>
    /// <returns>The card, ready to be handed to the overlay.</returns>
    private static Control BuildCard(string title, string message)
    {
        TextBlock heading = new() { Text = title, TextWrapping = TextWrapping.Wrap };
        heading.Classes.Add("h2");

        TextBlock detail = new() { Text = message, TextWrapping = TextWrapping.Wrap };
        detail.Classes.Add("hint");

        StackPanel content = new()
        {
            Spacing = 12,
            Children =
            {
                heading,
                new ProgressBar { IsIndeterminate = true, HorizontalAlignment = HorizontalAlignment.Stretch },
                detail,
            },
        };

        // The host Overlay clips its content to a rounded rectangle of its own, so this border only has to
        // supply the surface colour, the padding and a width the text can breathe in.
        Border card = new()
        {
            Child = content,
            MinWidth = 320,
            MaxWidth = 420,
        };
        card.Classes.Add("card");

        return card;
    }
}
