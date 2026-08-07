using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The settings page. Its <see cref="StyledElement.DataContext"/> is the
/// <see cref="ViewModels.SettingsViewModel"/> the shell binds into it.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class.</summary>
    public SettingsView() => InitializeComponent();
}
