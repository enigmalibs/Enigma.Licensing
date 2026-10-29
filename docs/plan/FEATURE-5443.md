# FEATURE-5443 — Catch unhandled exceptions

**Status:** TODO
**Type:** multi-phase (2 phases, one branch per phase: `feature/feature-5443-phaseNN-<slug>`)
**Branch:** `feature/feature-5443-phase01-error-hooks`, `feature/feature-5443-phase02-error-dialog`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every exception nothing else catches is logged and shown in an error **content dialog** that says the
application is still running — the safety net every Enigma app carries. The reference is
Enigma.GitClient's (`Services/UnhandledExceptionHooks.cs`, `UnhandledErrorPolicy.cs`,
`UnhandledErrorReporter.cs`, `ViewModels/Dialogs/UnhandledErrorViewModel.cs`,
`Views/Dialogs/UnhandledErrorView.axaml` @ `2c781d3`), scaled to a single-window app with no credentials.

## Context & constraints

- Today nothing hooks `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`
  or Avalonia's `Dispatcher.UIThread.UnhandledException`: a throwing command ends the process, and the only
  trace is the Console/Debug log.
- The pages already report the failures they expect (`INotificationService` on the info bar; validation
  verdicts on the card). The net is a safety net, not the error handling — none of that changes.
- CommunityToolkit.Mvvm's `AsyncRelayCommand` rethrows a faulted execution on the UI
  `SynchronizationContext`, i.e. into `Dispatcher.UIThread.UnhandledException`.
- An exception reaching `AppDomain.UnhandledException` ends the process whatever a handler does: it is logged.
- One window; one `ContentDialog` host (`HostDialog`), also used by About (FEATURE-CF21). The host order is
  Overlay → ContentDialog → InfoBar, so a dialog is drawn **above** the busy overlay — no waiting on it.
- Secrets: passwords are `char[]` and never enter an exception message; the details show type, message,
  stack and inner exceptions, never `Exception.Data`. No redaction layer is needed.
- The copy action reuses `IClipboardService` (exists, platform-free to fake).
- `App` owns the `IHost`; `Program.Main` is synchronous.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Shape | GitClient's: static hooks → reporter → policy → dialog | Uniform across apps; each part testable on its own | A try/catch in `Main` (catches nothing on the UI thread) |
| Scaling down | No multi-window tracking, no token recovery, no redaction | One window, no credentials here | Copying GitClient verbatim |
| What is shown | Dispatcher and unobserved-task errors → dialog; AppDomain → logged | The runtime allows nothing more | Pretending to recover from AppDomain |
| Floods | One dialog at a time; later errors counted into it; the same error again < 1 min after its dialog closed → logged only | A failing loop must not open a dialog per failure | A dialog per exception |
| Cancellations | `OperationCanceledException` ignored when its token was cancelled, or during shutdown | A stop someone asked for is not an error | Showing them |
| Errors before the window | Buffered (≤ 32) and handed over when the reporter attaches | The dialog needs its host | Dropping them |
| Copy details | `CopyDetailsCommand` on the ViewModel over `IClipboardService` | The seam exists and keeps the view logic-free and testable | Code-behind on `TopLevel.Clipboard` |
| Phasing | PHASE01 hooks + policy + logging; PHASE02 dialog + wiring | Two reviewable units; PHASE01 already stops the crashes | One dev |

## PHASE01 — Hooks, policy and logging

**Status:** TODO

### Steps

1. `Services/UnhandledErrorPolicy.cs` (GitClient's file layout): `UnhandledErrorSource` (`Dispatcher`,
   `UnobservedTask`, `AppDomain`), `UnhandledErrorDecision` (`Ignore`, `Merge`, `Suppress`, `Queue`),
   `UnhandledErrorBatch` (first error + later ones; `Message` unwrapping single-inner
   `AggregateException`/`TargetInvocationException`; `MoreCount`; `Details` — type, message, stack, inner
   exceptions to a depth cap, "Error i of n" when several; a `Changed` event), and `UnhandledErrorPolicy`
   (`TimeProvider`-driven: `IsShuttingDown`, `Offer`, `BeginShow`, `EndShow`, `RepeatWindow` = 1 min).
2. `Services/IUnhandledErrorReporter.cs` (`Report(exception, source)`, `ShutdownStarted()`) and
   `Services/UnhandledExceptionHooks.cs` (static: `Install()` for AppDomain + unobserved tasks,
   `InstallDispatcher()` marking the error handled, `Attach`/`Detach`, `Report`; early reports buffered
   ≤ 32 and traced).
3. `Services/UnhandledErrorReporter.cs` (`internal sealed`): logs every report with its source
   (`LogError`; `LogDebug` for an ignored cancellation) and runs the policy; no dialog yet. Registered
   `AddSingleton` with `TimeProvider.System`.
4. Wiring: `Program.Main` calls `Install()` first; `App.OnFrameworkInitializationCompleted` calls
   `InstallDispatcher()` first, attaches the reporter once the host is built, calls `ShutdownStarted` on
   `ShutdownRequested`, and `Detach()` on exit before the host stops.
5. Tests: policy (ignore same instance / cancellation / shutdown; merge into an open batch; suppress a
   repeat within the window; expiry with a manual `TimeProvider`), batch (message unwrapping, details shape,
   depth cap, `Changed`), hooks (buffered before attach and flushed on attach, cap at 32, detach), reporter
   (logs with source — `RecordingLogger`). Hook tests in one non-parallel collection (static state).

### Acceptance criteria

- A command that throws no longer ends the process; the exception is in the log with its source.
- Release build warning-free; the whole suite green, new tests included.

## PHASE02 — The error dialog

**Status:** TODO

### Steps

1. `ViewModels/UnhandledErrorViewModel.cs`: `Lead`, `Message`, `HasMore`/`MoreText` (singular/plural),
   `Details`, `CopyDetailsCommand` (`IClipboardService`); follows the batch's `Changed`.
2. `Views/UnhandledErrorView.axaml(.cs)`: the reference's markup (named elements, automation names,
   Details expander collapsed, read-only monospace details box, Copy details button).
3. `UnhandledErrorReporter` shows a queued batch on the UI thread through `IContentDialogService`: the
   reference's chrome (title, warning icon and brush, one default `Close`); a dialog already open (About)
   is waited for, not replaced; the next pending batch is shown after close; a failure to show is logged
   and the net falls back to log-only.
4. Tests: `UnhandledErrorViewModelTests` (texts, singular/plural, follows `Changed`, copy goes to the
   clipboard), reporter tests over a fake `IContentDialogService` (one dialog for two errors, repeat
   suppressed, shutdown shows nothing).
5. Docs: README desktop paragraph (one line), `CLAUDE.md` (*Shipped*, layout).
6. Manual check: a temporary throwing path in a Release build shows the dialog and the app keeps running;
   recorded, the temporary code removed.

### Acceptance criteria

- A throwing command shows the dialog with its message; the app keeps running.
- Two errors in a row → one dialog with "1 more error occurred."; the same error again within a minute →
  log only.
- Release build warning-free with 0 `AVLN`; the whole suite green, new tests included.

## Out of scope

- Crash reporting to any service; restarting the app after an AppDomain-level failure.
- A dialog for a failure inside `OnFrameworkInitializationCompleted` before the window exists (logged).
- Changing the pages' existing, specific error handling.
