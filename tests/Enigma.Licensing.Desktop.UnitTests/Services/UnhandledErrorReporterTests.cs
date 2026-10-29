using System;
using System.Threading;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// The safety net logs every error with where it was caught, and leaves a stop someone asked for out of it. The
/// UI thread is a delegate here, so no platform is needed.
/// </summary>
public sealed class UnhandledErrorReporterTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly RecordingLogger<UnhandledErrorReporter> _logger = new();

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
    public void ARequestedStop_IsLoggedAtDebug_AndNeverOffered()
    {
        using CancellationTokenSource stop = new();
        stop.Cancel();
        int posted = 0;
        UnhandledErrorReporter reporter = Reporter(action => { posted++; action(); });

        reporter.Report(new OperationCanceledException(stop.Token), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        Assert.Equal(0, posted);
    }

    [Fact]
    public void AfterShutdownStarted_EveryCancellationIsADebugLine()
    {
        UnhandledErrorReporter reporter = Reporter();

        reporter.ShutdownStarted();
        reporter.Report(new OperationCanceledException(), UnhandledErrorSource.UnobservedTask);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }

    /// <summary>
    /// No dialog yet: a batch is opened and closed at once, so the repeat window already applies to the log.
    /// </summary>
    [Fact]
    public void TheSameErrorAgain_WithinAMinute_IsLoggedAsARepeat()
    {
        UnhandledErrorReporter reporter = Reporter();
        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);

        reporter.Report(Failure(), UnhandledErrorSource.Dispatcher);

        Assert.Null(reporter.Policy.Pending);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("less than", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(null!, _logger));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_time, null!));
        Assert.Throws<ArgumentNullException>(() => new UnhandledErrorReporter(_time, _logger, null!));
    }

    private UnhandledErrorReporter Reporter(Action<Action>? post = null)
        => new(_time, _logger, post ?? (action => action()));

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
