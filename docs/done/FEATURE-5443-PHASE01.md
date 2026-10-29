# FEATURE-5443-PHASE01 — Hooks, policy and logging

## Summary

The desktop app now catches every exception nothing else handles, logs it with where it was caught, and
keeps running. This is Enigma.GitClient's safety net, the family reference, scaled to one window and no
credentials.

- **Hooks.** `UnhandledExceptionHooks` hooks:
  - `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`, from `Program.Main`, first;
  - `Dispatcher.UIThread.UnhandledException`, from `App`, first; it is marked handled, which is what saves
    the process.
- **Early reports.** A report made before the reporter exists is kept (at most 32) and traced, then handed
  over in order when `App` attaches the reporter. `App` detaches it on exit, before the host stops.
- **Reporter.** `UnhandledErrorReporter` logs at once (`LogError` with the source, or `LogDebug` for a
  requested stop) and decides on the UI thread through `UnhandledErrorPolicy`. It ignores cancellations
  that were asked for, and any during shutdown. It merges errors into an open batch and suppresses a repeat
  within one minute of its batch closing.
- **No dialog yet** (PHASE02). A queued batch is opened and closed at once, so the repeat window already
  applies to the log.

## Files/modules touched

- Created:
  - `src/Enigma.Licensing.Desktop/Services/UnhandledErrorPolicy.cs` (`UnhandledErrorSource`,
    `UnhandledErrorDecision`, `UnhandledErrorBatch`, `UnhandledErrorPolicy`)
  - `src/Enigma.Licensing.Desktop/Services/UnhandledExceptionHooks.cs`
  - `src/Enigma.Licensing.Desktop/Services/IUnhandledErrorReporter.cs`
  - `src/Enigma.Licensing.Desktop/Services/UnhandledErrorReporter.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/Services/UnhandledErrorPolicyTests.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/Services/UnhandledExceptionHooksTests.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/Services/UnhandledErrorReporterTests.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/TestDoubles/ManualTimeProvider.cs`
  - `docs/done/FEATURE-5443-PHASE01.md`
- Modified:
  - `src/Enigma.Licensing.Desktop/Program.cs`
  - `src/Enigma.Licensing.Desktop/App.axaml.cs`
  - `src/Enigma.Licensing.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `tests/.../Hosting/ServiceCollectionExtensionsTests.cs`
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-5443.md`

## Deviations & follow-ups

- **Sources.** GitClient's `FireAndForget` and `Startup` sources were left out. This app has no
  fire-and-forget helper and a single start path, and the plan listed three sources.
- **Policy file layout.** The plan's step 1 named a file per type. Following GitClient, the enums, the batch
  and the policy share `UnhandledErrorPolicy.cs`.
- **Found during the manual check:** a command that throws *synchronously*, invoked through UI Automation,
  surfaces to the automation client, not to the dispatcher. A real click and an async command (every
  command here) do reach the dispatcher. Recorded in CLAUDE.md.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How the reporter reaches the UI thread | An `Action<Action> post` on an `internal` constructor. The public one posts to `Dispatcher.UIThread` | Keeps the suite platform-free. The container only sees public constructors |
| What a queued batch does before PHASE02 | Opened and closed at once | Without it, every later error would merge into a batch that never closes |
| The clock | `TimeProvider.System` via `TryAddSingleton`. A `ManualTimeProvider` test double | Testing both sides of the one-minute window needs no new package |
| Hook tests | A collection with `DisableParallelization`, plus an internal `ResetForTests()` | The hooks' state is static and process-wide |
| `Install()`/`InstallDispatcher()` in tests | Not called | They would subscribe the test process's own AppDomain and dispatcher |
| App's access to the app-own types | Aliases, as `AppTheming` | `App.axaml.cs` imports the library's `Services` namespace, which would collide |

## Build/test evidence

- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**. No `.axaml` was touched.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **771/771 passed**. That is 737 + 34 new:
  - policy and batch: 19;
  - hooks: 6;
  - reporter: 7;
  - registration: 2.
- **Manual check:**
  1. A scratch copy of the Release app was built to the session scratchpad, with `OpenAboutCommand`
     temporarily replaced by an async command that throws.
  2. The rail's Settings item and **About Enigma Licensing** were invoked through UI Automation, with
     stdout redirected.
  3. Result: the process was still alive afterwards, and the log reads
     `An unhandled exception reached the safety net (Dispatcher).` /
     `System.InvalidOperationException: Temporary manual-check failure.`.

  The temporary edit was reverted before anything was staged; `git status` showed the file clean.

## Documentation sweep

- `CLAUDE.md`: the PHASE01 "Program.cs owns nothing" bullet, the layout lines, and a new *Shipped* section.
  Fixed.
- README: nothing became wrong (the error dialog line comes with PHASE02).
