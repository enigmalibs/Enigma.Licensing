# FEATURE-CF21 — Uniform About dialog

## Summary

The desktop app now has the About dialog every Enigma application shows. It is a copy of
Enigma.GitClient's (`Views/Dialogs/AboutView.axaml`, `AboutViewModel`, `AboutDialogService` @ `2c781d3`).

- **Layout:** a 380 px column holding these lines:
  - the 96 px icon (`Assets/app-256.png`);
  - **Enigma Licensing** (18, SemiBold);
  - `Version X.Y.Z` (secondary brush);
  - `Build <7-char sha>` (11 px, tertiary brush, the sha selectable, hidden when the build recorded none);
  - the copyright (11 px, hidden when empty).
- **Chrome:** the dialog has no title, no icon and one default **Close** button.
- **Where it opens:** an **About Enigma Licensing** button at the end of the settings page's About
  expander, as GitClient's settings page does.

`AppInfo` gained `ParseVersion(informational, fallback)`, `BuildSha`, `Copyright` and `UnknownVersion`.
`AppVersion` and `LibraryVersion` now go through the same parse.

## Files/modules touched

- Created:
  - `src/Enigma.Licensing.Desktop/Assets/app-256.png`
  - `src/Enigma.Licensing.Desktop/Services/IAboutDialogService.cs`
  - `src/Enigma.Licensing.Desktop/Services/AboutDialogService.cs`
  - `src/Enigma.Licensing.Desktop/ViewModels/AboutViewModel.cs`
  - `src/Enigma.Licensing.Desktop/Views/AboutView.axaml(.cs)`
  - `tests/Enigma.Licensing.Desktop.UnitTests/TestDoubles/FakeAboutDialogService.cs`
  - `tests/Enigma.Licensing.Desktop.UnitTests/ViewModels/AboutViewModelTests.cs`
  - `docs/done/FEATURE-CF21.md`
- Modified:
  - `src/Enigma.Licensing.Desktop/Resources/AppInfo.cs`
  - `src/Enigma.Licensing.Desktop/ViewModels/SettingsViewModel.cs`
  - `src/Enigma.Licensing.Desktop/Views/SettingsView.axaml`
  - `src/Enigma.Licensing.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `tests/.../Hosting/ServiceCollectionExtensionsTests.cs`
  - `tests/.../Resources/AppInfoTests.cs`
  - `tests/.../ViewModels/SettingsViewModelTests.cs`
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-CF21.md`

## Deviations & follow-ups

- **`AppInfo`'s fallback string changed** from `unknown` to `0.0.0`, as the reference has it. This happens
  only when an assembly declares no version at all, so it is never visible in a real build.
- **The view's header comment** names Enigma.GitClient as the reference rather than MarkdownEditor, which is
  what GitClient's own comment says. GitClient is the reference this spec names. The markup is otherwise
  line for line.
- **README:** unchanged. Its desktop paragraph does not describe the settings page.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `ParseVersion`'s return | A public static returning `(string Version, string? BuildSha)` | Testable with no new type (Msi precedent). It builds clean under the repo's analyzers |
| The private parsed field | Named `Running`, not `Application` | Avoids a name that reads like Avalonia's `Application` |
| Command type | `AsyncRelayCommand` built in the constructor | The app's existing ViewModels declare commands this way, not with `[RelayCommand]` |
| Button margin | `0,8,0,0` | It sits below a `value` row, which already has a 4 px bottom margin. GitClient uses `0,4,0,0` under rows with none |

## Build/test evidence

- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**.
- AVLN count for the desktop project (`--no-incremental -v normal | grep -c AVLN`): **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **735/735 passed**. That is 712 + 23 new:
  - `AboutViewModelTests`: 6;
  - `AppInfoTests`: 14 (parse cases, fallbacks, copyright, sha length);
  - `SettingsViewModelTests`: 2;
  - the registration theory: 1.
- `app-256.png`: `cmp`-identical to `Branding/…/png/enigma-licensing-256.png`, which FEATURE-1ECA showed is the
  `.ico`'s 256 frame.
- **Manual check** in the Release build:
  1. Launched it.
  2. Selected the rail's footer item through UI Automation.
  3. Invoked **About Enigma Licensing** (found by its automation name).
  4. Captured the window with `PrintWindow`.

  The dialog showed the certificate icon, **Enigma Licensing**, `Version 2.0.0`, a quieter `Build c973b70`
  (the run-branch tip), `Copyright © 2026 Josué Clément` and a lone **Close**, with no title and no icon.
  That is the reference's layout. The process was closed afterwards.

## Documentation sweep

- `CLAUDE.md`: a new *Shipped* section, the layout lines, and the FEATURE-7310/1ECA note that said this app
  had no about-box bitmap. Fixed.
