using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The keys page. Its <see cref="StyledElement.DataContext"/> is the
/// <see cref="ViewModels.GenerateKeysViewModel"/> the shell binds into it.
/// </summary>
public partial class GenerateKeysView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="GenerateKeysView"/> class.</summary>
    public GenerateKeysView() => InitializeComponent();
}
