using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The content of the safety net's dialog. Its <see cref="StyledElement.DataContext"/> is a
/// <see cref="ViewModels.UnhandledErrorViewModel"/>.
/// </summary>
public partial class UnhandledErrorView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="UnhandledErrorView"/> class.</summary>
    public UnhandledErrorView() => InitializeComponent();
}
