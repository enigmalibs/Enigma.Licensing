using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Licensing.Desktop.Services;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;

namespace Enigma.Licensing.Desktop.UnitTests.Services;

/// <summary>
/// The wrapper over the control library's info bar, and the one thing about it that is not obvious.
/// </summary>
/// <remarks>
/// <b>The library's <c>ShowAsync</c> resolves on dismissal, not on display.</b> Passing that task on would
/// mean every command that reports its outcome stays open until a user clicks a banner away — the button
/// still disabled, the overlay bookkeeping still pending, and the <c>finally</c> that wipes a password copy
/// from memory not yet run. These tests are what stop that from being reintroduced by someone simplifying
/// the method back into a one-line <c>=></c>.
/// </remarks>
public sealed class NotificationServiceTests
{
    [Fact]
    public async Task ShowSuccessAsync_ReturnsWithoutWaitingForTheBarToBeDismissed()
    {
        FakeInfoBarService infoBar = new();
        NotificationService notifications = new(infoBar);

        // Hangs for ever here if the dismissal task is passed through.
        await notifications.ShowSuccessAsync("Key pair generated", "Fingerprint 378A86040C38330F.");

        Assert.Equal(1, infoBar.Shows);
    }

    [Fact]
    public async Task ShowErrorAsync_ReturnsWithoutWaitingForTheBarToBeDismissed()
    {
        FakeInfoBarService infoBar = new();
        NotificationService notifications = new(infoBar);

        await notifications.ShowErrorAsync("The key pair could not be generated", "The disk is full.");

        Assert.Equal(1, infoBar.Shows);
    }

    [Theory]
    [InlineData(true, InfoBarSeverity.Error)]
    [InlineData(false, InfoBarSeverity.Success)]
    public async Task TheBar_CarriesTheTitleTheMessageAndTheSeverity(bool isError, InfoBarSeverity severity)
    {
        FakeInfoBarService infoBar = new();
        NotificationService notifications = new(infoBar);

        await (isError
            ? notifications.ShowErrorAsync("A title", "A message")
            : notifications.ShowSuccessAsync("A title", "A message"));

        Assert.NotNull(infoBar.Bar);
        Assert.Equal("A title", infoBar.Bar.Title);
        Assert.Equal("A message", infoBar.Bar.Message);
        Assert.Equal(severity, infoBar.Bar.Severity);
    }

    /// <summary>
    /// A host that was never registered is a startup fault, and the one failure this wrapper can actually
    /// see. It has to reach the caller rather than be dropped with the dismissal task.
    /// </summary>
    [Fact]
    public async Task AFailureThatHappensImmediately_ReachesTheCaller()
    {
        FakeInfoBarService infoBar = new() { Fail = new InvalidOperationException("InfoBar host has not been registered.") };
        NotificationService notifications = new(infoBar);

        await Assert.ThrowsAsync<InvalidOperationException>(() => notifications.ShowSuccessAsync("A title", "A message"));
    }

    [Fact]
    public void Constructor_RejectsAMissingInfoBar()
    {
        Assert.Throws<ArgumentNullException>(() => new NotificationService(null!));
    }
}
