using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// What the safety net's dialog says: that something went wrong and the application is still running, what
/// went wrong, how many more errors came, and the details for a bug report.
/// </summary>
/// <remarks>
/// Enigma.GitClient's dialog, the family reference, without its token-reset remedy — this application stores
/// no credentials. It follows its batch: an error counted into it while the dialog is open shows at once, in
/// the count and in the details.
/// </remarks>
public sealed class UnhandledErrorViewModel : ObservableObject
{
    private readonly UnhandledErrorBatch _batch;
    private readonly IClipboardService _clipboard;

    /// <summary>Initializes a new instance of the <see cref="UnhandledErrorViewModel"/> class.</summary>
    /// <param name="batch">The errors to show.</param>
    /// <param name="clipboard">Puts the details on the clipboard.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public UnhandledErrorViewModel(UnhandledErrorBatch batch, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(clipboard);

        _batch = batch;
        _clipboard = clipboard;

        CopyDetailsCommand = new AsyncRelayCommand(OnCopyDetailsAsync);

        batch.Changed += OnBatchChanged;
    }

    /// <summary>Gets the sentence that opens the dialog.</summary>
    public string Lead
        => "Something went wrong, but the application is still running. You can carry on, or close it and "
            + "start it again if something looks wrong.";

    /// <summary>Gets what went wrong.</summary>
    public string Message => _batch.Message;

    /// <summary>Gets a value indicating whether more errors came after the first.</summary>
    public bool HasMore => _batch.More > 0;

    /// <summary>Gets how many more errors came after the first, as a sentence.</summary>
    public string MoreText
        => _batch.More == 1
            ? "1 more error occurred."
            : $"{_batch.More.ToString(CultureInfo.CurrentCulture)} more errors occurred.";

    /// <summary>Gets the details of every error, for a bug report.</summary>
    public string Details => _batch.Details;

    /// <summary>Gets the command that puts the details on the clipboard.</summary>
    public AsyncRelayCommand CopyDetailsCommand { get; }

    private Task OnCopyDetailsAsync() => _clipboard.CopyTextAsync(Details);

    private void OnBatchChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(MoreText));
        OnPropertyChanged(nameof(Details));
    }
}
