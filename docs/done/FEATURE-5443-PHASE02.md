# FEATURE-5443-PHASE02 — The error dialog

## Summary

Every error the safety net does not ignore or suppress is now shown in Enigma.GitClient's error dialog,
and the application keeps running. The dialog's chrome:

- title *Something went wrong*;
- the Phosphor `Warning` icon in `#E8A33D`;
- one default **Close** button.

Its content (`UnhandledErrorView`):

- the lead sentence ("…the application is still running…");
- the message in SemiBold;
- "N more errors occurred." when errors were merged in;
- a collapsed **Details** expander, holding a read-only monospace box and **Copy details**.

`UnhandledErrorViewModel` follows its batch while the dialog is open.

How the reporter shows it (`UnhandledErrorReporter`):

- It shows through `IContentDialogService`, on the window's `HostDialog`. `App` hands it that host with the
  new `IUnhandledErrorReporter.RegisterHost`, after the content dialog service has it.
- A batch waits for the host to be registered. If another dialog (About) holds the host, the batch waits for
  that dialog's `Closed`.
- After each dialog it shows the next pending batch.
- If the dialog itself fails, it logs that and stays log-only for the rest of the session.

## Files/modules touched

- Created:
  - `src/Enigma.Licensing.Desktop/ViewModels/UnhandledErrorViewModel.cs`
  - `src/Enigma.Licensing.Desktop/Views/UnhandledErrorView.axaml(.cs)`
  - `tests/Enigma.Licensing.Desktop.UnitTests/ViewModels/UnhandledErrorViewModelTests.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/TestDoubles/FakeContentDialogService.cs`
  - `docs/done/FEATURE-5443-PHASE02.md`
- Modified:
  - `src/Enigma.Licensing.Desktop/Services/UnhandledErrorReporter.cs`
  - `src/Enigma.Licensing.Desktop/Services/IUnhandledErrorReporter.cs`
  - `src/Enigma.Licensing.Desktop/Resources/AppIcons.cs` (`Warning`)
  - `src/Enigma.Licensing.Desktop/App.axaml.cs`
  - `tests/.../Services/UnhandledErrorReporterTests.cs`
  - `tests/.../Services/UnhandledExceptionHooksTests.cs` (the recording double implements `RegisterHost`)
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-5443.md`

## Deviations & follow-ups

- **README not touched.** Plan step 5 asked for one line in its desktop paragraph. The README is the packed
  NuGet landing page, and that paragraph says what the app is for, not how it behaves inside. An error
  dialog line would be noise for a package reader. CLAUDE.md carries the detail.
- **Two departures from GitClient's markup:**
  - the details box uses the app's own `mono` class (fixed-pitch chain, 12 px), because this theme defines
    no `MonospaceFontFamily` resource;
  - **Copy details** is a plain button, because the `toolbar` class is GitClient's own style.
- **The remedy line and token reset are left out**, as planned: this app stores no credentials.
- **No headless view test.** The suite stays platform-free. The view was checked in the running app.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How the net knows another dialog is open | `RegisterHost(ContentDialog)` on the reporter, with the same host the content dialog service shows on | One window, one host. GitClient's multi-window `IAppWindows` seam is unnecessary here |
| Testability | Two more internal-constructor delegates: the view factory and the icon | `UnhandledErrorView`'s XAML and `AppIcons` (a `StreamGeometry`) need a platform the suite does not have |
| Testing the busy-host path | A real `ContentDialog`: `ShowAsync()` opens it, `HideAsync()` raises `Closed` | Neither needs a platform. The test exercises the real event rather than a fake |
| Where `errors.RegisterHost` sits in `App` | After the five library handovers, with a comment | The net shows *through* the content dialog service, so that service must have the host first |

## Build/test evidence

- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**.
- AVLN count for the desktop project (`--no-incremental -v normal | grep -c AVLN`): **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **783/783 passed**, run twice with the same
  result (the async continuations did not flake). That is 771 + 12 net new:
  - `UnhandledErrorViewModelTests`: 5;
  - `UnhandledErrorReporterTests`: grew from 7 to 14 (chrome, waiting for the host, one dialog for two
    errors, the repeat window on both sides, waiting for a busy host, dialog failure, posting).
- **Manual check:**
  1. A scratch copy of the Release app was built with `OpenAboutCommand` temporarily replaced by an async
     command that throws.
  2. The rail's Settings item and **About Enigma Licensing** were invoked through UI Automation, and the
     window captured with `PrintWindow`.
  3. Result: the dialog showed *Something went wrong* with the amber warning icon, the lead sentence,
     **Temporary manual-check failure.**, a collapsed **Details** and a lone **Close**. The process was alive
     afterwards, and the log has the `(Dispatcher)` line.

  The temporary edit was reverted before anything was staged.

## Documentation sweep

- `CLAUDE.md`: the FEATURE-5443 *Shipped* section (dialog, host waiting, test seams) and the layout lines.
  Fixed.
