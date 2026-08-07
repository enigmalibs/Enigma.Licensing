using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Services;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the info bar, keeping every outcome reported so a test can assert both that something was
/// said and what it said — a page that fails silently is the failure mode this guards.
/// </summary>
internal sealed class FakeNotificationService : INotificationService
{
    private readonly List<Notification> _notifications = [];

    /// <summary>Gets every notification shown, in order.</summary>
    internal IReadOnlyList<Notification> Notifications => _notifications;

    /// <inheritdoc/>
    public Task ShowSuccessAsync(string title, string message)
    {
        _notifications.Add(new Notification(IsError:false, title, message));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowErrorAsync(string title, string message)
    {
        _notifications.Add(new Notification(IsError:true, title, message));

        return Task.CompletedTask;
    }

    /// <summary>One thing the page reported.</summary>
    /// <param name="IsError">Whether it was a failure rather than a success.</param>
    /// <param name="Title">The few words naming the outcome.</param>
    /// <param name="Message">The detail.</param>
    internal sealed record Notification(bool IsError, string Title, string Message);
}
