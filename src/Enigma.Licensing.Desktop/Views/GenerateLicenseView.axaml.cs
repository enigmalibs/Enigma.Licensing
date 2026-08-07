using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The generate page. Its <see cref="StyledElement.DataContext"/> is the
/// <see cref="ViewModels.GenerateLicenseViewModel"/> the shell binds into it.
/// </summary>
public partial class GenerateLicenseView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="GenerateLicenseView"/> class.</summary>
    public GenerateLicenseView() => InitializeComponent();
}
