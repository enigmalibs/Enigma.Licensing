using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The validate page. Its <see cref="StyledElement.DataContext"/> is the
/// <see cref="ViewModels.ValidateLicenseViewModel"/> the shell binds into it.
/// </summary>
public partial class ValidateLicenseView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ValidateLicenseView"/> class.</summary>
    public ValidateLicenseView() => InitializeComponent();
}
