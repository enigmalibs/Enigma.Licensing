using System.Threading.Tasks;

namespace Enigma.Licensing.Desktop.Services;

/// <summary>
/// Tells the user how an operation went, without interrupting them.
/// </summary>
/// <remarks>
/// The application's own seam over the control library's info bar. A ViewModel says what happened; the
/// implementation decides what that looks like — which is also what keeps the severity enum, and the info
/// bar itself, out of every page.
/// </remarks>
public interface INotificationService
{
    /// <summary>Reports that an operation finished as intended.</summary>
    /// <param name="title">A few words naming what succeeded.</param>
    /// <param name="message">The detail worth reading — what was produced, and where.</param>
    /// <returns>A task that completes once the notification is showing.</returns>
    Task ShowSuccessAsync(string title, string message);

    /// <summary>Reports that an operation failed.</summary>
    /// <param name="title">A few words naming what failed.</param>
    /// <param name="message">Why it failed, in the words the failure itself used.</param>
    /// <returns>A task that completes once the notification is showing.</returns>
    Task ShowErrorAsync(string title, string message);
}
