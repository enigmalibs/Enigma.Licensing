using Avalonia;
using Avalonia.Controls;

namespace Enigma.Licensing.Desktop.Views;

/// <summary>
/// The application shell window. Its <see cref="StyledElement.DataContext"/> is supplied by the host container in
/// <see cref="App.OnFrameworkInitializationCompleted"/> — never constructed here.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow() => InitializeComponent();
}
