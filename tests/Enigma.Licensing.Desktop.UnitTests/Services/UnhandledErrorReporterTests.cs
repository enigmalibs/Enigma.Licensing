using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// The safety net logs every error with where it was caught, shows it in one dialog at a time on the window's
/// host, and leaves a stop someone asked for out of both. The UI thread, the view and the icon are delegates
/// here, so no platform is needed; the dialog host is a real <see cref="ContentDialog"/>.
/// </summary>
public sealed class UnhandledErrorReporterTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly RecordingLogger<UnhandledErrorReporter> _logger = new();
    private readonly FakeContentDialogService _dialogs = new();
    private readonly List<UnhandledErrorViewModel> _viewModels = [];
    private readonly ContentDialog _host = new();

    [Theory]
    [InlineData(UnhandledErrorSource.Dispatcher)]
    [InlineData(UnhandledErrorSource.UnobservedTask)]
    [InlineData(UnhandledErrorSource.AppDomain)]
    public void AnError_IsLoggedAsAnError_WithWhereItWasCaught(UnhandledErrorSource source)
    {
        UnhandledErrorReporter reporter = Reporter();

        reporter.Report(new InvalidOperationException("boom"), source);

        (LogLevel level, string message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Contains($"({source})", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnError_IsShown_InTheReferenceDialog()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);

        reporter.Report(new IOException("The disk is full."), UnhandledErrorSource.Dispatcher);

        ContentDialog dialog = Assert.Single(_dialogs.Shown);
        Assert.Equal("Something went wrong", dialog.Title);
        Assert.Equal("Close", dialog.CloseButtonText);
        Assert.Null(dialog.PrimaryButtonText);
        Assert.Equal(DefaultButton.Close, dialog.DefaultButton);
        Assert.Equal(Color.FromRgb(0xE8, 0xA3, 0x3D), Assert.IsType<SolidColorBrush>(dialog.IconBrush).Color);
        Assert.Equal("The disk is full.", Assert.Single(_viewModels).Message);
        Assert.Same(_viewModels[0], Assert.IsType<Border>(dialog.Content).DataContext);
    }

    /// <summary>An error caught before the window registered its host is shown once it has.</summary>
    [Fact]
    public void AnErrorBeforeTheHost_WaitsForIt()
    {
        UnhandledErrorReporter reporter = Reporter();

        reporter.Report(new IOException("early"), UnhandledErrorSource.Dispatcher);

        Assert.Empty(_dialogs.Shown);
        Assert.NotNull(reporter.Policy.Pending);

        reporter.RegisterHost(_host);

        Assert.Single(_dialogs.Shown);
    }

    [Fact]
    public void TwoErrorsInARow_MakeOneDialog_ThatSaysOneMoreOccurred()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);

        reporter.Report(new IOException("first"), UnhandledErrorSource.Dispatcher);
        reporter.Report(new InvalidOperationException("second"), UnhandledErrorSource.UnobservedTask);

        Assert.Single(_dialogs.Shown);
        Assert.Equal("1 more error occurred.", Assert.Single(_viewModels).MoreText);
        Assert.Equal(2, _logger.Entries.Count(entry => entry.Level == LogLevel.Error));
    }

    [Fact]
    public void TheSameErrorAgain_WithinAMinuteOfItsDialogClosing_IsOnlyLogged()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);
        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);
        _dialogs.CloseOldest();

        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);

        Assert.Single(_dialogs.Shown);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("not shown again", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameErrorAgain_AfterAMinute_IsShownAgain()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);
        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);
        _dialogs.CloseOldest();
        _time.Advance(UnhandledErrorPolicy.RepeatWindow);

        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);

        Assert.Equal(2, _dialogs.Shown.Count);
    }

    /// <summary>
    /// Another dialog — About — holds the host: showing over it would replace its content, so the net waits for
    /// it to close.
    /// </summary>
    [Fact]
    public async Task AnErrorWhileAnotherDialogIsOpen_WaitsForItToClose()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);
        Task<DialogResult> other = _host.ShowAsync();

        reporter.Report(new IOException("boom"), UnhandledErrorSource.Dispatcher);

        Assert.Empty(_dialogs.Shown);

        await _host.HideAsync();
        await other;

        Assert.Single(_dialogs.Shown);
    }

    /// <summary>A net that loops on its own failure would be worse than none.</summary>
    [Fact]
    public void ADialogThatCannotBeShown_IsLogged_AndLaterErrorsAreOnlyLogged()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);
        _dialogs.FailWith = new InvalidOperationException("no dialog today");

        reporter.Report(new IOException("first"), UnhandledErrorSource.Dispatcher);
        _dialogs.FailWith = null;
        _time.Advance(UnhandledErrorPolicy.RepeatWindow);
        reporter.Report(new IOException("second"), UnhandledErrorSource.Dispatcher);

        Assert.Empty(_dialogs.Shown);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be shown", StringComparison.Ordinal));
    }

    [Fact]
    public void ARequestedStop_IsLoggedAtDebug_AndNeverOffered()
    {
        using CancellationTokenSource stop = new();
        stop.Cancel();
        int posted = 0;
        UnhandledErrorReporter reporter = Reporter(action => { posted++; action(); });
        reporter.RegisterHost(_host);

        reporter.Report(new OperationCanceledException(stop.Token), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        Assert.Equal(0, posted);
        Assert.Empty(_dialogs.Shown);
    }

    [Fact]
    public void AfterShutdownStarted_EveryCancellationIsADebugLine()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.RegisterHost(_host);

        reporter.ShutdownStarted();
        reporter.Report(new OperationCanceledException(), UnhandledErrorSource.UnobservedTask);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        Assert.Empty(_dialogs.Shown);
    }

    /// <summary>The decision is taken on the UI thread, never on the reporting one.</summary>
    [Fact]
    public void TheOffer_IsPostedToTheUiThread()
    {
        Action? posted = null;
        UnhandledErrorReporter reporter = Reporter(action => posted = action);
        reporter.RegisterHost(_host);

        reporter.Report(new InvalidOperationException("boom"), UnhandledErrorSource.Dispatcher);

        Assert.NotNull(posted);
        Assert.Empty(_dialogs.Shown);

        posted();

        Assert.Single(_dialogs.Shown);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        FakeClipboardService clipboard = new();

        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(null!, clipboard, _time, _logger));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, null!, _time, _logger));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, clipboard, null!, _logger));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, clipboard, _time, null!));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, clipboard, _time, _logger, null!, View, () => null));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, clipboard, _time, _logger, action => action(), null!, () => null));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_dialogs, clipboard, _time, _logger, action => action(), View, null!));
    }

    [Fact]
    public void RegisterHost_RejectsANullHost() => Assert.Throws<ArgumentNullException>(() => Reporter().RegisterHost(null!));

    private UnhandledErrorReporter Reporter(Action<Action>? post = null)
        => new(_dialogs, new FakeClipboardService(), _time, _logger, post ?? (action => action()), Capture, () => null);

    private Control Capture(UnhandledErrorViewModel viewModel)
    {
        _viewModels.Add(viewModel);

        return View(viewModel);
    }

    private static Control View(UnhandledErrorViewModel viewModel) => new Border { DataContext = viewModel };

    private static InvalidOperationException Failure()
    {
        try
        {
            throw new InvalidOperationException("the same failure");
        }
        catch (InvalidOperationException caught)
        {
            return caught;
        }
    }
}
