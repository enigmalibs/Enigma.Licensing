using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Desktop.Views;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// The <see cref="IAboutDialogService"/> that shows <see cref="AboutView"/> in the window's content dialog.
/// </summary>
/// <remarks>
/// <para>
/// On <see cref="IContentDialogService"/>, whose host <c>App</c> registers at startup: About is a dialog like
/// any other — shown, read, closed.
/// </para>
/// <para>
/// The host's size bounds are not restated: they are set once on <c>MainWindow</c>'s <c>HostDialog</c> and no
/// dialog in this application changes them, and the view's own 380 px width sits inside them.
/// </para>
/// </remarks>
internal sealed class AboutDialogService : IAboutDialogService
{
    private readonly IContentDialogService _dialogs;

    /// <summary>Initializes a new instance of the <see cref="AboutDialogService"/> class.</summary>
    /// <param name="dialogs">Hosts the dialog.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialogs"/> is <see langword="null"/>.</exception>
    public AboutDialogService(IContentDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _dialogs = dialogs;
    }

    /// <inheritdoc/>
    public async Task ShowAsync()
    {
        // A fresh ViewModel per showing: it is immutable and costs nothing to build.
        AboutView view = new()
        {
            DataContext = new AboutViewModel(),
        };

        // The service resets the host before configuring it — buttons, commands, icon and its brush — so only
        // what this dialog has is set.
        await _dialogs.ShowAsync(dialog =>
        {
            // No title and no icon: the card draws them as a column beside the content, and the view already
            // shows the application's own icon and name above everything else.
            dialog.Title = null;
            dialog.Content = view;

            // One button, and it only dismisses: the dialog asks nothing.
            dialog.CloseButtonText = "Close";
            dialog.DefaultButton = DefaultButton.Close;
        }).ConfigureAwait(true);
    }
}
