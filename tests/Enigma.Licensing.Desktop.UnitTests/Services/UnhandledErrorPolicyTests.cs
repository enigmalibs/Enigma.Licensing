using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// What the safety net decides about an exception, and what it says about one. The rules are what keep a
/// failing loop from burying the window in dialogs, and a stop someone asked for from being called an error.
/// </summary>
public sealed class UnhandledErrorPolicyTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void TheFirstError_IsQueued_AsANewBatch()
    {
        UnhandledErrorPolicy policy = new(_time);
        InvalidOperationException error = Thrown(new InvalidOperationException("boom"));

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(error));
        Assert.Same(error, policy.Pending?.First);
        Assert.Null(policy.Showing);
    }

    /// <summary>The same exception can reach two hooks — the dispatcher's and the unobserved task's.</summary>
    [Fact]
    public void TheSameExceptionOfferedTwice_IsIgnoredTheSecondTime()
    {
        UnhandledErrorPolicy policy = new(_time);
        InvalidOperationException error = Thrown(new InvalidOperationException("boom"));

        policy.Offer(error);

        Assert.Equal(UnhandledErrorDecision.Ignore, policy.Offer(error));
        Assert.Equal(0, policy.Pending!.More);
    }

    [Fact]
    public void AnErrorWhileOneWaits_IsCountedIntoIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown(new InvalidOperationException("first")));

        Assert.Equal(UnhandledErrorDecision.Merge, policy.Offer(Thrown(new IOException("second"))));
        Assert.Equal(1, policy.Pending!.More);
    }

    [Fact]
    public void AnErrorWhileOneIsOnScreen_IsCountedIntoIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown(new InvalidOperationException("first")));
        UnhandledErrorBatch showing = policy.BeginShow()!;

        Assert.Equal(UnhandledErrorDecision.Merge, policy.Offer(Thrown(new IOException("second"))));
        Assert.Same(showing, policy.Showing);
        Assert.Equal(1, showing.More);
        Assert.Null(policy.Pending);
    }

    [Fact]
    public void TheSameErrorAgain_WithinTheRepeatWindowAfterItClosed_IsSuppressed()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(ThrownAt(1));
        policy.BeginShow();
        policy.EndShow();

        _time.Advance(UnhandledErrorPolicy.RepeatWindow - TimeSpan.FromSeconds(1));

        Assert.Equal(UnhandledErrorDecision.Suppress, policy.Offer(ThrownAt(1)));
        Assert.Null(policy.Pending);
    }

    [Fact]
    public void TheSameErrorAgain_OnceTheRepeatWindowHasPassed_IsShownAgain()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(ThrownAt(1));
        policy.BeginShow();
        policy.EndShow();

        _time.Advance(UnhandledErrorPolicy.RepeatWindow);

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(ThrownAt(1)));
    }

    [Fact]
    public void ADifferentError_WithinTheRepeatWindow_IsShown()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(ThrownAt(1));
        policy.BeginShow();
        policy.EndShow();

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(ThrownAt(2)));
    }

    [Fact]
    public void ACancellationOfARequestedStop_IsIgnored()
    {
        using CancellationTokenSource stop = new();
        stop.Cancel();
        UnhandledErrorPolicy policy = new(_time);

        Assert.Equal(UnhandledErrorDecision.Ignore, policy.Offer(new OperationCanceledException(stop.Token)));
        Assert.Null(policy.Pending);
    }

    /// <summary>A cancellation nobody asked for — a timeout — is an error like any other.</summary>
    [Fact]
    public void ACancellationNobodyAskedFor_IsAnError()
    {
        UnhandledErrorPolicy policy = new(_time);

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(new OperationCanceledException()));
    }

    [Fact]
    public void AnyCancellation_WhileShuttingDown_IsIgnored()
    {
        UnhandledErrorPolicy policy = new(_time) { IsShuttingDown = true };

        Assert.Equal(UnhandledErrorDecision.Ignore, policy.Offer(new OperationCanceledException()));
    }

    [Fact]
    public void AnAggregateOfRequestedStopsOnly_IsIgnored_ButOneRealErrorInsideMakesItAnError()
    {
        using CancellationTokenSource stop = new();
        stop.Cancel();
        UnhandledErrorPolicy policy = new(_time);

        Assert.True(policy.IsIgnoredCancellation(new AggregateException(new OperationCanceledException(stop.Token), new TaskCanceledException(null, null, stop.Token))));
        Assert.False(policy.IsIgnoredCancellation(new AggregateException(new OperationCanceledException(stop.Token), new IOException("disk"))));
    }

    [Fact]
    public void BeginShow_MovesThePendingBatchOnScreen_AndEndShowClosesIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown(new InvalidOperationException("boom")));

        UnhandledErrorBatch? batch = policy.BeginShow();

        Assert.NotNull(batch);
        Assert.Null(policy.Pending);
        Assert.Same(batch, policy.Showing);

        policy.EndShow();

        Assert.Null(policy.Showing);
        Assert.Null(policy.BeginShow());
    }

    /// <summary>Wrappers say "one or more errors occurred": the one inside says what.</summary>
    [Fact]
    public void Summarise_UnwrapsSingleInnerWrappers()
    {
        Exception wrapped = new AggregateException(new TargetInvocationException(new IOException("The disk is full.")));

        Assert.Equal("The disk is full.", UnhandledErrorPolicy.Summarise(wrapped));
    }

    [Fact]
    public void Summarise_KeepsAnAggregateOfSeveral_AsItIs()
    {
        AggregateException several = new("Two things failed.", new IOException("a"), new IOException("b"));

        Assert.StartsWith("Two things failed.", UnhandledErrorPolicy.Summarise(several), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDetails_NamesEachExceptionInsideWithItsMessageAndStack()
    {
        Exception error = Thrown(new InvalidOperationException("outer", Thrown(new IOException("inner"))));

        string details = UnhandledErrorPolicy.BuildDetails(error);

        Assert.StartsWith("System.InvalidOperationException: outer", details, StringComparison.Ordinal);
        Assert.Contains("--- Inner exception ---", details, StringComparison.Ordinal);
        Assert.Contains("System.IO.IOException: inner", details, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), details, StringComparison.Ordinal);
    }

    /// <summary><see cref="Exception.Data"/> is where a caller attaches what it likes — never copied out.</summary>
    [Fact]
    public void BuildDetails_NeverIncludesTheExceptionsData()
    {
        InvalidOperationException error = new("boom");
        error.Data["password"] = "hunter2";

        Assert.DoesNotContain("hunter2", UnhandledErrorPolicy.BuildDetails(error), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDetails_StopsFollowingInnerExceptions_AtTheDepthCap()
    {
        Exception error = new IOException("level 0");

        for (int level = 1; level <= 20; level++)
        {
            error = new IOException($"level {level}", error);
        }

        string details = UnhandledErrorPolicy.BuildDetails(error);

        Assert.Contains("level 20", details, StringComparison.Ordinal);
        Assert.Contains("level 12", details, StringComparison.Ordinal);
        Assert.DoesNotContain("level 11", details, StringComparison.Ordinal);
    }

    [Fact]
    public void ABatchOfSeveral_NumbersEachErrorInItsDetails_AndRaisesChangedOnEach()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown(new InvalidOperationException("first")));
        UnhandledErrorBatch batch = policy.Pending!;
        int changed = 0;
        batch.Changed += (_, _) => changed++;

        policy.Offer(Thrown(new IOException("second")));
        policy.Offer(Thrown(new IOException("third")));

        Assert.Equal(2, changed);
        Assert.Equal(2, batch.More);
        Assert.Equal("first", batch.Message);
        Assert.Contains("Error 1 of 3", batch.Details, StringComparison.Ordinal);
        Assert.Contains("Error 3 of 3", batch.Details, StringComparison.Ordinal);
    }

    private static T Thrown<T>(T exception)
        where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T caught)
        {
            return caught;
        }
    }

    /// <summary>
    /// A fresh exception per call: two calls with the same <paramref name="site"/> are the same error to the
    /// policy — type, message and stack — while being two different objects.
    /// </summary>
    private static InvalidOperationException ThrownAt(int site) => Thrown(new InvalidOperationException($"repeating failure {site}"));
}
