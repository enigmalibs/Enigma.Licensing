# FEATURE-CF21 — Uniform About dialog

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-cf21-uniform-about-dialog`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

An About dialog with the same layout, style and colours as every other Enigma app's. The reference is
Enigma.GitClient's (`Views/Dialogs/AboutView.axaml`, `ViewModels/Dialogs/AboutViewModel.cs`,
`Services/AboutDialogService.cs` @ `2c781d3`), itself Enigma.MarkdownEditor's.

## Context & constraints

**The reference:**

- Dialog: on `IContentDialogService`; `Title = null`, no icon; one `Close` button,
  `DefaultButton = DefaultButton.Close`; `ConfigureAwait(true)`.
- Content: a `UserControl`, `Width="380"`, one `StackPanel Spacing="14"`:
  1. `Image Source="/Assets/app-256.png"`, 96×96, centred (the PNG, not the `.ico` — Skia's frame choice
     inside an `.ico` is unspecified);
  2. a `StackPanel Spacing="3"`: the product name (`productName`: 18, SemiBold, centred,
     `EnigmaForegroundBrush`); a horizontal `Spacing="5"` line `Version` + version (`version`:
     `EnigmaForegroundSecondaryBrush`); the `BuildLine` (`build`: 11, `EnigmaForegroundTertiaryBrush`,
     the sha a `SelectableTextBlock x:Name="BuildSha"`), hidden when the build recorded no revision;
  3. the `Copyright` (`copyright`: 11, centred, wrapping, `EnigmaForegroundSecondaryBrush`), hidden when
     empty — read from `AssemblyCopyrightAttribute` (`Copyright © 2026 Josué Clément`, Directory.Build.props).
- ViewModel: `DisplayName`, `Version`, `BuildSha` + `HasBuildSha`, `Copyright` + `HasCopyright`; a
  `(version, buildSha, copyright)` constructor beside the parameterless one. Version = informational
  version before `+`; sha = what follows, cut to 7 characters; fallback = assembly version (3 parts),
  then `0.0.0`.

**This app today:** no About dialog. `SettingsView` has an *About* `SettingsCardExpander` with the app
version, the library version and the project URL (from `Resources/AppInfo.cs`, which already strips
`+<sha>`). `HostDialog` (`MainWindow.axaml`) is registered at startup with `DialogMinWidth 380`,
`DialogMaxWidth 640`, `DialogMaxHeight 480` and is unused so far. `IClipboardService` and the other app
seams are public interfaces with `internal sealed` implementations registered `AddSingleton` in
`AddEnigmaLicensingDesktop()`. Views/ViewModels are flat folders; ViewModels derive from `ObservableObject`.
The desktop suite stands up **no Avalonia platform** (CLAUDE.md) — a XAML view is not loaded in a test.

## Steps

1. `Assets/app-256.png` — `Branding/Apps/final/licensing/png/enigma-licensing-256.png`, byte-identical to
   `appicon.ico`'s 256 frame (verified).
2. `Resources/AppInfo.cs`: add `BuildSha` and `Copyright`; one public static parse
   (`ParseVersion(informational, fallback)` → version + short sha) behind `AppVersion`/`BuildSha`, so a build
   with no revision is testable. `Title` stays the display name; `LibraryVersion` and `ProjectUrl` stay.
3. `ViewModels/AboutViewModel.cs` — the reference's shape, on `ObservableObject`.
4. `Views/AboutView.axaml(.cs)` — the reference's markup and comments, line for line (names `BuildLine`,
   `BuildSha`, `Copyright`).
5. `Services/IAboutDialogService.cs` + `Services/AboutDialogService.cs` (`internal sealed`) — the reference's
   service, a fresh `AboutViewModel` per showing; registered `AddSingleton`.
6. `SettingsViewModel` takes `IAboutDialogService` and exposes `OpenAboutCommand` (`AsyncRelayCommand`);
   `SettingsView`'s About expander ends with an `About Enigma Licensing` button (with its automation name),
   as GitClient's settings page does. The expander's three rows stay — the library version is this app's own.
7. Tests: `AppInfoTests` (parse: with sha / without / long sha cut to 7 / blank → fallback / nothing →
   `0.0.0`; copyright non-empty); `AboutViewModelTests` (shape, `HasBuildSha`, `HasCopyright`);
   `SettingsViewModelTests` (the command shows the dialog once) with a `FakeAboutDialogService`;
   `ServiceCollectionExtensionsTests` (the registration's lifetime).
8. Docs: README's desktop paragraph if it describes the settings page; `CLAUDE.md` (*Shipped*, layout).
9. Manual check: launch the Release build, open Settings → About, screenshot the dialog (UI Automation),
   compare with the reference layout; record it.

## Acceptance criteria

- The view matches the reference element for element (order, sizes, font sizes, brushes, spacing).
- The dialog has no title and no icon and one default `Close` button.
- Release build warning-free with 0 `AVLN`; the whole suite green, new tests included.

## Out of scope

- A shared About library across Enigma apps (each app carries its copy, as GitClient does).
- Opening the project URL in a browser (`AppInfo` keeps showing it as text).
- Headless view tests (new test infrastructure this suite deliberately lacks).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where it opens from | A button at the end of Settings' About expander | GitClient's settings page does exactly this; the rail has no toolbar | A rail footer item; a window menu |
| The expander's rows | Kept | They carry the library version, which the dialog does not | Replacing the expander by the button |
| Version parsing | A public static on `AppInfo` | Testable with no new type; GitClient's `ProductVersion` lives in a Core assembly this app lacks (Msi precedent) | A new record type |
| ViewModel lifetime | New per showing, not DI | Immutable and cheap; the reference does the same | A DI singleton |
| Dialog sizes | Not restated | The host's three sizes are set once in XAML and no dialog changes them | Restating them in the service |
| View test | None; manual screenshot instead | The suite stands up no platform on purpose (CLAUDE.md) | Adding Avalonia.Headless |
