using System.Runtime.CompilerServices;

// The application's service implementations are internal — a page depends on the interface, and nothing
// outside this assembly has a reason to name the class behind it. Two of them nevertheless carry behaviour
// that no fake of their interface can pin down, because the behaviour *is* how they use the control
// library: NotificationService must not wait for the user to dismiss an info bar, and BusyOverlayService
// builds the progress card the overlay shows. Both are the kind of thing that looks fine and hangs, or
// looks fine and renders nothing, so the test assembly gets friend access. Declared here rather than as an
// MSBuild AssemblyAttribute so the reason travels with the attribute — the same choice the library made.
[assembly: InternalsVisibleTo("Enigma.Licensing.Desktop.UnitTests")]
