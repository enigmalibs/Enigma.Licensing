namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// How badly a licence failed to validate — the three weights the validate page paints a verdict in.
/// </summary>
/// <remarks>
/// <para>
/// The application's own vocabulary rather than the control library's <c>InfoBarSeverity</c>, for the same
/// reason <see cref="Services.INotificationService"/> exists: a ViewModel says what happened and the view
/// decides what that looks like. The verdict is painted on the page rather than in a notification, so the
/// severity has to reach XAML — as three bound style classes on <see cref="LicenseVerdict"/>, not as a
/// control-library enum on a page.
/// </para>
/// <para>
/// <b>The split follows the library's own boundary</b>, which is what makes it worth having three weights
/// rather than a bare valid/invalid pair. Authenticity is settled before anything the licence <i>says</i> is
/// read, so a failure is either "this document cannot be trusted at all" — <see cref="Error"/> — or
/// "authentic, and it does not apply here" — <see cref="Warning"/>. An expired licence and a forged one are
/// completely different conversations with a customer, and a page that painted both red would hide that.
/// </para>
/// </remarks>
public enum LicenseVerdictSeverity
{
    /// <summary>The licence passed every check.</summary>
    Success,

    /// <summary>
    /// The signature holds, but the licence does not apply: it is outside its validity window, or it is for
    /// another product or another machine. Nothing is wrong with the document.
    /// </summary>
    Warning,

    /// <summary>
    /// The document cannot be trusted — it is unreadable, names a scheme this library cannot check, or its
    /// signature does not verify.
    /// </summary>
    Error,
}
