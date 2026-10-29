using System;
using System.Collections.Generic;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// The hooks keep what is caught before the safety net exists, a bounded number of times, and hand it over
/// once it does. Process-wide state, so the class runs in a collection of its own, never alongside another.
/// </summary>
/// <remarks>
/// <see cref="UnhandledExceptionHooks.Install"/> and <see cref="UnhandledExceptionHooks.InstallDispatcher"/> are
/// not called here: they subscribe this test process's own <c>AppDomain</c> and dispatcher, which no test should
/// own. What they forward to — <see cref="UnhandledExceptionHooks.Report"/> — is what is asserted.
/// </remarks>
[Collection(UnhandledExceptionHooksCollection.Name)]
public sealed class UnhandledExceptionHooksTests : IDisposable
{
    public UnhandledExceptionHooksTests() => UnhandledExceptionHooks.ResetForTests();

    public void Dispose() => UnhandledExceptionHooks.ResetForTests();

    [Fact]
    public void WhatIsReportedBeforeTheNetIsAttached_IsHandedOverWhenItIs_InOrder()
    {
        InvalidOperationException first = new("first");
        InvalidOperationException second = new("second");
        UnhandledExceptionHooks.Report(first, UnhandledErrorSource.Dispatcher);
        UnhandledExceptionHooks.Report(second, UnhandledErrorSource.UnobservedTask);
        RecordingReporter reporter = new();

        UnhandledExceptionHooks.Attach(reporter);

        Assert.Equal(
            [(first, UnhandledErrorSource.Dispatcher), (second, UnhandledErrorSource.UnobservedTask)],
            reporter.Reports);
    }

    [Fact]
    public void OnceAttached_AReportGoesStraightToTheNet()
    {
        RecordingReporter reporter = new();
        UnhandledExceptionHooks.Attach(reporter);
        InvalidOperationException error = new("boom");

        UnhandledExceptionHooks.Report(error, UnhandledErrorSource.AppDomain);

        Assert.Equal([(error, UnhandledErrorSource.AppDomain)], reporter.Reports);
    }

    [Fact]
    public void TheEarlyReports_AreKeptUpToTheLimit()
    {
        for (int index = 0; index < UnhandledExceptionHooks.EarlyLimit + 10; index++)
        {
            UnhandledExceptionHooks.Report(new InvalidOperationException($"error {index}"), UnhandledErrorSource.Dispatcher);
        }

        RecordingReporter reporter = new();
        UnhandledExceptionHooks.Attach(reporter);

        Assert.Equal(UnhandledExceptionHooks.EarlyLimit, reporter.Reports.Count);
        Assert.Equal("error 0", reporter.Reports[0].Exception.Message);
    }

    [Fact]
    public void TheEarlyReports_AreHandedOverOnce()
    {
        UnhandledExceptionHooks.Report(new InvalidOperationException("early"), UnhandledErrorSource.Dispatcher);
        UnhandledExceptionHooks.Attach(new RecordingReporter());
        RecordingReporter second = new();

        UnhandledExceptionHooks.Attach(second);

        Assert.Empty(second.Reports);
    }

    /// <summary>After <see cref="UnhandledExceptionHooks.Detach"/> the disposed net is never called again.</summary>
    [Fact]
    public void AfterDetach_ReportsAreKeptAgain_NotSentToTheOldNet()
    {
        RecordingReporter old = new();
        UnhandledExceptionHooks.Attach(old);
        UnhandledExceptionHooks.Detach();
        InvalidOperationException late = new("late");

        UnhandledExceptionHooks.Report(late, UnhandledErrorSource.Dispatcher);
        RecordingReporter next = new();
        UnhandledExceptionHooks.Attach(next);

        Assert.Empty(old.Reports);
        Assert.Equal([(late, UnhandledErrorSource.Dispatcher)], next.Reports);
    }

    [Fact]
    public void Report_RejectsANullException()
    {
        Assert.Throws<ArgumentNullException>(() => UnhandledExceptionHooks.Report(null!, UnhandledErrorSource.Dispatcher));
    }

    private sealed class RecordingReporter : IUnhandledErrorReporter
    {
        private readonly List<(Exception Exception, UnhandledErrorSource Source)> _reports = [];

        public IReadOnlyList<(Exception Exception, UnhandledErrorSource Source)> Reports => _reports;

        public void Report(Exception exception, UnhandledErrorSource source) => _reports.Add((exception, source));

        public void ShutdownStarted()
        {
        }

        public void RegisterHost(ContentDialog host)
        {
        }
    }
}

/// <summary>The hooks' static state is the process's: their tests never run beside another test class.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UnhandledExceptionHooksCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "UnhandledExceptionHooks";
}
