using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// What the safety net's dialog says, and that it follows its batch while it is open.
/// </summary>
public sealed class UnhandledErrorViewModelTests
{
    [Fact]
    public void TheDialog_SaysTheAppIsStillRunning_AndWhatWentWrong()
    {
        UnhandledErrorViewModel viewModel = new(new UnhandledErrorBatch(new IOException("The disk is full.")), new FakeClipboardService());

        Assert.StartsWith("Something went wrong, but the application is still running.", viewModel.Lead, StringComparison.Ordinal);
        Assert.Equal("The disk is full.", viewModel.Message);
        Assert.False(viewModel.HasMore);
        Assert.StartsWith("System.IO.IOException: The disk is full.", viewModel.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void OneMoreError_IsSaidInTheSingular_AndSeveralInThePlural()
    {
        UnhandledErrorBatch batch = new(new IOException("first"));
        UnhandledErrorViewModel viewModel = new(batch, new FakeClipboardService());

        batch.Add(new IOException("second"));

        Assert.True(viewModel.HasMore);
        Assert.Equal("1 more error occurred.", viewModel.MoreText);

        batch.Add(new IOException("third"));

        Assert.Equal("2 more errors occurred.", viewModel.MoreText);
    }

    /// <summary>An error counted in while the dialog is open shows at once: the bindings are told.</summary>
    [Fact]
    public void AnErrorCountedIn_RaisesTheCountAndTheDetails()
    {
        UnhandledErrorBatch batch = new(new IOException("first"));
        UnhandledErrorViewModel viewModel = new(batch, new FakeClipboardService());
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        batch.Add(new IOException("second"));

        Assert.Equal([nameof(viewModel.HasMore), nameof(viewModel.MoreText), nameof(viewModel.Details)], changed);
        Assert.Contains("Error 2 of 2", viewModel.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyDetails_PutsTheWholeDetailsOnTheClipboard()
    {
        FakeClipboardService clipboard = new();
        UnhandledErrorViewModel viewModel = new(new UnhandledErrorBatch(new IOException("The disk is full.")), clipboard);

        await viewModel.CopyDetailsCommand.ExecuteAsync(null);

        Assert.Equal([viewModel.Details], clipboard.Copied);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorViewModel(null!, new FakeClipboardService()));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorViewModel(new UnhandledErrorBatch(new IOException("x")), null!));
    }
}
