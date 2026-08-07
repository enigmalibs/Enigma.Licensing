# FEATURE-179C-PHASE02 — Keys page

**Status:** DONE
**Branch:** `feature/feature-179c-phase02-keys-page`
**Plan:** `docs/plan/FEATURE-179C.md` (PHASE02)

## Summary

The first of the three placeholder pages is now real. The keys page picks a signing scheme (six on offer,
defaulting to **ML-DSA-65**), shows an RSA modulus picker only when an RSA scheme is chosen (four sizes,
defaulting to **4096**), optionally takes a password with a confirmation, asks where each half should be
written, and then generates the pair on a background thread behind the blocking overlay, reports the outcome
on the info bar, and keeps the new key's fingerprint on the page with a Copy button beside it.

With it come the four application-owned platform seams PHASE01 promised but deliberately deferred until
something called them: `IFileDialogService`, `INotificationService`, `IBusyOverlayService` and
`IClipboardService`. Every one of them exists so that no ViewModel names an Avalonia type — the item-level
acceptance criterion — and so that every step of the page is assertable in a test process with no windowing
platform.

Three things are worth recording because they are not obvious from the plan text:

- **The control library's `IInfoBarService.ShowAsync` completes when the bar is *dismissed*, not when it
  appears.** `InfoBar.ShowAsync()` returns a `TaskCompletionSource` resolved by its `Closed` event, so
  `await`ing it waits for a user's click. The first version of `NotificationService` passed that task
  straight through, which would have left `OnGenerateKeyPairAsync` "running" — its button disabled, and,
  worse, its `finally` unrun — until somebody cleared a green banner. The `finally` is what wipes the
  password copy out of memory. `NotificationService` therefore deliberately does not await, and returns the
  library's task only when it has already completed (which in practice means it failed: an info-bar host
  that was never registered). `NotificationServiceTests` pins this with a double whose task never resolves,
  so a later "simplification" back to a one-line `=>` fails rather than hangs. **This was found by running
  the app, not by a test** — see *Build/test evidence*.
- **The copy action is on the page, not in the info bar.** The plan asks for "success InfoBar shows the new
  keyId fingerprint with copy action". The fingerprint *is* in the info bar's message, but the action could
  not go there: the library's `InfoBar` control template has no `ContentPresenter`, so its inherited
  `Content` is never rendered — and building a button in a ViewModel would break the no-Avalonia-in-a-VM
  rule anyway. A result card below the form holds the fingerprint and its Copy button, which is better
  regardless: a notification that has been dismissed cannot be copied from, and the fingerprint is exactly
  the value an issuer has to carry over to the application that will verify.
- **The application now declares `InternalsVisibleTo` for its test assembly**, which it did not before. Two
  of the new services carry behaviour that no fake of their *interface* can reach, because the behaviour is
  precisely how they use the control library: the info-bar timing above, and the progress card
  `BusyOverlayService` builds in code. Both fail in ways that look like a working application — one hangs, the
  other shows an empty box — so they are tested directly. The reason travels with the attribute, as in the
  library.

## Files/modules touched

**Created — application (`src/Enigma.Licensing.Desktop/`)**

- `Services/IFileDialogService.cs` / `FileDialogService.cs` — the app's save-dialog seam over the control
  library's picker. It returns a plain path, because the library's own interface hands back Avalonia
  `IStorageFile` handles and the plain-path overloads that avoid them are extension methods no double can
  intercept. Every document this application writes is JSON, so the file-type choice lives in the
  implementation rather than in every call.
- `Services/INotificationService.cs` / `NotificationService.cs` — success and error over the info bar, with
  the dismissal-timing fix above.
- `Services/IBusyOverlayService.cs` / `BusyOverlayService.cs` — a title and a line of explanation over the
  blocking overlay. The card is built in code (a `card`-classed `Border` over `h2` / `ProgressBar` / `hint`)
  rather than declared as a templated control, which would have meant a class, a control theme and a
  dictionary merged into `App.axaml` to say the same thing.
- `Services/IClipboardService.cs` / `ClipboardService.cs` — copies a string. It reaches the clipboard through
  the running application's main window, the same route `ThemeService` takes to the application, and treats
  having no clipboard as a log line rather than a failure.
- `ViewModels/SignatureAlgorithmOption.cs` — one picker entry per scheme: the enum member, the format's own
  wire identifier as the label, a line explaining the choice, and whether it brings a key size with it.
- `Properties/AssemblyInfo.cs` — `InternalsVisibleTo("Enigma.Licensing.Desktop.UnitTests")`, with its reason.

**Modified — application**

- `ViewModels/GenerateKeysViewModel.cs` — the placeholder replaced by the page: pickers, password and
  confirmation, two output paths with their own error flags and browse commands, the generate command, the
  fingerprint and its copy command.
- `Views/GenerateKeysView.axaml` — three `SettingsCardExpander`s (algorithm, private-key protection, output
  files) over the library's `TextEditor`s, the accent generate button, and the result card.
- `Hosting/ServiceCollectionExtensions.cs` — the four new services, `AddSingleton`.

**Created — tests (`tests/Enigma.Licensing.Desktop.UnitTests/`)**

- `ViewModels/GenerateKeysViewModelTests.cs` — 22 cases over the page against doubles.
- `ViewModels/GenerateKeysRoundTripTests.cs` — the page against the **real** `KeyService`, writing real files
  to a temporary folder: this is where the phase's acceptance criterion is actually shown.
- `ViewModels/SignatureAlgorithmOptionTests.cs` — including the completeness check against
  `Enum.GetValues<LicenseSignatureAlgorithm>()`, so a scheme added to the library and not to the picker fails
  the build's test run.
- `Services/NotificationServiceTests.cs`, `Services/BusyOverlayServiceTests.cs`.
- `TestDoubles/FakeKeyService.cs`, `FakeFileDialogService.cs`, `FakeNotificationService.cs`,
  `FakeBusyOverlayService.cs`, `FakeClipboardService.cs`, `FakeInfoBarService.cs`, `FakeOverlayService.cs`.

**Modified — tests**

- `Hosting/ServiceCollectionExtensionsTests.cs` — the four new registrations and their lifetimes, a
  resolvability test for them, and four more aliases now that the two `Services` namespaces genuinely collide
  (which is what PHASE01 put the alias structure there for).

**Modified — docs**

- `docs/roadmap.md`, `docs/plan/FEATURE-179C.md` — statuses.

## Deviations & follow-ups

- **The copy action sits on the page rather than inside the info bar** — see *Summary*. The plan's intent
  (the fingerprint is reported and can be copied) is met; the placement is forced by the control's template.
- **A password that has been typed but not repeated keeps the generate button disabled**, and the mismatch
  message appears only once something has been typed into the confirmation field. The predecessor
  application only complained when *both* fields were non-empty and different, which left its button live
  with an unconfirmed password — a key encrypted with a typo is a key nobody can open. The two rules are
  separate on purpose: one governs the button, the other governs the error text.
- **The password's `string` cannot be cleared, only the `char[]` copy.** A `TextBox` hands its text over as a
  string, and .NET strings are immutable and interned into the heap until collected. The page therefore
  clears the array it made — asserted by a test that reads the array back after the command — and the
  password is never written to a file or a log. That is the whole of what this layer can do; the alternative
  would be a custom secure-input control, which is out of scope for this application.
- **`FileDialogService` and `ClipboardService` are covered by their registration and resolution only.** The
  first is an eight-line pass-through whose only untested part would need a stub of Avalonia's
  thirteen-member `IStorageProvider`; the second needs a running `Application`. Both are exercised by hand in
  the running app (the pickers via the two Browse buttons). If either grows a decision, it earns a test then.
- **The `<name>.public.key.json` / `<name>.private.key.json` convention is a suggestion, not a rule.** The
  page offers `signing.*` when nothing has been chosen, and names the second half after the first when one
  has — so choosing `acme.public.key.json` offers `acme.private.key.json` in the same folder. Nothing
  enforces the suffixes: the library reads a key file by its contents, and a user who wants other names
  should have them.
- **A note on the control library, for whoever bumps it:** `InfoBarService.ShowAsync` subscribes a new
  `Closed` handler on every call and never unsubscribes the previous one, so a bar replaced before it is
  dismissed leaves a handler and a `TaskCompletionSource` that never completes. Harmless here — nothing
  awaits them — but it is why this application must not start awaiting them either.
- **No manual click-through of the generate flow was possible**: this machine has no input-automation tooling
  (no `xdotool`, `wtype` or `ydotool`), the same limitation PHASE01 recorded. What replaces it is the
  round-trip test against the real library, plus the direct tests of the overlay card and the info-bar
  timing. See *Build/test evidence* for what was verified in the running application instead.
- No line-ending (CRLF) inconsistency was observed in any file touched by this phase.

## Build/test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded.  0 Warning(s)  0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal
  → 0 lines matching AVLN (XAML warnings bypass TreatWarningsAsErrors, so they are counted by hand)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → total: 976   failed: 0   succeeded: 976   skipped: 0   (13.9 s)
```

976 cases, up from 918: **58 new**, all in the desktop suite (140, up from 82). The suite still runs in under
two seconds, because the only real cryptography in it is ML-DSA-44 — the cheapest scheme the library has —
plus the two Argon2id derivations the encrypted round-trip pays for deliberately.

**Manual verification (Linux/Wayland, Release, `dotnet run`):**

- **The page renders end to end**, in the stored dark variant: all three cards, their `{ei:IconGeometry}`
  icons (Key, LockKey, FloppyDisk), the algorithm picker on ML-DSA-65 with its description line beneath, both
  password fields with their placeholders, and both path fields with their Browse buttons.
- **The RSA branch renders too, with 4096 selected.** Verified by temporarily defaulting
  `SignatureAlgorithmOption.Default` to `RsaSha256`, building, screenshotting and reverting — the "Modulus
  size" picker appears with the right value, which is what proves the `int` `SelectedItem` binding resolves.
  Its *visibility* rule is covered by `IsRsaSelected_FollowsTheChosenScheme` across all six schemes.
- **This is where the info-bar deadlock was found.** A temporary line in `App.OnFrameworkInitializationCompleted`
  raised the overlay and a success notification at startup to photograph them; the application then started,
  logged normally, and never showed a window at all. The cause was `.GetAwaiter().GetResult()` on a task that
  only completes when the user dismisses the bar — a synchronous-blocking artefact of the probe, but the same
  task the real code was awaiting. Both the probe and the temporary defaults were reverted; the fix and its
  tests are described above.
- The app starts and shuts down cleanly — `Application started` / `Application is shutting down` from
  `Microsoft.Hosting.Lifetime`, and **no `Navigation failed` line**, which is the evidence that the window
  resolved, the hosts registered and the page factory built the keys page with its ViewModel.
- The dialogs, the overlay in situ and the clipboard could not be exercised: they need clicks, and there is no
  input-automation tooling on this machine.

## Acceptance criteria

| Criterion | Met |
|---|---|
| Algorithm picker, six schemes, default ML-DSA-65 | Yes |
| RSA key-size picker shown for RSA only, default 4096 | Yes — visibility tested across all six schemes, rendering verified by hand |
| Optional private-key password + confirmation, match validated | Yes — and an unconfirmed password blocks the command |
| Output via save dialogs with the suggested names | Yes — `signing.*` by default, named after the other half once one is chosen |
| Generation on a background task with Overlay progress | Yes — generation *and* the Argon2id save both run off the UI thread |
| Success InfoBar shows the keyId fingerprint with copy action | Fingerprint in the info bar; copy action on the page's result card (see *Deviations*) |
| Failures → InfoBar error | Yes — and the overlay comes down on every path |
| VM tests: mismatch blocks, algorithm/size coupling, arguments, busy state | Yes |
| Build/tests green | Yes — 0 warnings, 0 AVLN, 976/976 |
| A key pair generated in the app loads back through the library | Yes — `GenerateKeysRoundTripTests`, both halves, encrypted and not |
