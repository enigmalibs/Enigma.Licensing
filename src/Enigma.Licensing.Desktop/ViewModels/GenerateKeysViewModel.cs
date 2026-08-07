using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// The keys page: generating a signing key pair and writing both halves to disk.
/// </summary>
/// <remarks>
/// A placeholder. The page exists from this phase so the shell has its three workflows on the rail from the
/// first run; the algorithm and RSA key-size pickers, the private-key password, the save dialogs and the
/// call into <c>IKeyService</c> arrive with <c>FEATURE-179C-PHASE02</c>.
/// </remarks>
public sealed class GenerateKeysViewModel : ObservableObject;
