# FEATURE-179C-PHASE01 — App shell, hosting, theme & settings

**Status:** DONE
**Branch:** `feature/feature-179c-phase01-shell`
**Plan:** `docs/plan/FEATURE-179C.md` (PHASE01)

## Summary

The desktop application now exists as a running shell: `src/Enigma.Licensing.Desktop` (net10.0, Avalonia
12.1.1 on Enigma.Avalonia.Desktop 1.0.0) starts, builds an `IHost` inside `App`, resolves its window from
the container, hands the control library its three hosts and its storage provider, applies the stored
colour variant *before* the first frame, and shows a four-item navigation rail — Keys, Generate, Validate,
with Settings pinned to the footer. Three of those four pages are deliberate placeholders that PHASE02–04
fill in; Settings is complete, with the appearance selector and an about section.

Three things are worth recording because they are not obvious from the plan text:

- **The library is registered through its own `AddEnigmaLicensing()`, not service by service.** The
  application therefore inherits the library's composition wholesale — including the explicit factory that
  decides which `ILicenseValidator` the store validates with (`FEATURE-03A9-PHASE04`). Re-registering the
  four by hand would have worked today and quietly diverged the moment the library changed its own wiring.
  `AddEnigmaLicensingDesktop_Twice_LeavesTheLibrarysServicesRegisteredOnce` pins the consequence: the
  library's extension is `TryAdd` throughout, so calling the app's registration twice does not duplicate it.
- **`App.axaml.cs` imports only the *library's* `Services` namespace and reaches the app's own theme
  service through an `AppTheming` alias**, and the two hosting extensions live in separate files for the
  same reason. Neither is necessary yet — nothing collides at PHASE01. Both are there because PHASE02
  introduces an application `IFileDialogService` wrapping the library's interface of the same name, and
  without the split that phase would begin with a `CS0104` and a rewrite of the composition root. The
  comment in each file says so, so neither gets "simplified" away in the meantime.
- **`GenerateDocumentationFile` is on for the app**, which the exemplar (Enigma.HardCopy) does not do. It
  makes a missing XML comment on a public member a build error under `TreatWarningsAsErrors`, the same
  discipline the library is held to. It cost one real fix: Avalonia declares `DataContext` on
  `StyledElement`, not on `Control`/`Window`, so every view's code-behind cref names `StyledElement`
  (`CS1574` otherwise). The app is never packed, so this is a discipline setting, not a packaging one.

## Files/modules touched

**Created — application (`src/Enigma.Licensing.Desktop/`)**

- `Enigma.Licensing.Desktop.csproj` — WinExe, net10.0, compiled bindings, `app.manifest`, `ApplicationIcon`,
  `AvaloniaResource Include="Assets/**"`, the publish target that drops *native* `.pdb` symbols.
- `app.manifest` — per-monitor-v2 DPI awareness; `assemblyIdentity` version `1.0.0.0`.
- `Assets/appicon.ico` — the predecessor's icon, reused as placeholder branding (see *Deviations*).
- `Program.cs` — synchronous `Main`, `UsePlatformDetect` + `WithInterFont` + `#if DEBUG WithDeveloperTools`.
- `App.axaml` / `App.axaml.cs` — `FluentTheme` plus the library's `Fluent.axaml` as a `ResourceInclude`, the
  app's style classes (`page`, `h1`, `h2`, `body`, `hint`, `field`, `value`, `mono`, `card`); host build,
  `RegisterHost` ×3, `SetStorageProvider` ×2, stored-theme application, `NavigationFailed` → `ILogger`,
  first selection, `desktop.Exit` → `StopAsync`.
- `Hosting/EnigmaAvaloniaServiceCollectionExtensions.cs` — `AddEnigmaAvaloniaDesktop()`, the control
  library's six services as `TryAddSingleton`.
- `Hosting/ServiceCollectionExtensions.cs` — `AddEnigmaLicensingDesktop()`: `AddEnigmaLicensing()`, the
  window, the settings store (by factory, so a test can point it at a temporary file), the theme service,
  the four page views (transient) and their ViewModels (singleton), and the shell ViewModel with its rail
  icons passed in.
- `Resources/AppIcons.cs` — the four Phosphor outlines the rail needs, resolved once each.
- `Resources/AppInfo.cs` — title, project URL, and the app/library versions read from the running assemblies.
- `Services/IThemeService.cs` / `ThemeService.cs` — the one place that assigns `RequestedThemeVariant`.
- `Settings/AppTheme.cs`, `AppSettings.cs`, `IAppSettingsStore.cs`, `AppSettingsStore.cs` — the preference
  file under `ApplicationData/Enigma.Licensing/settings.json`, written temp-then-move, never throwing.
- `ViewModels/MainWindowViewModel.cs`, `ThemeOption.cs`, `SettingsViewModel.cs`, and the three placeholder
  page ViewModels (`GenerateKeysViewModel`, `GenerateLicenseViewModel`, `ValidateLicenseViewModel`).
- `Views/MainWindow.axaml(.cs)`, `SettingsView.axaml(.cs)`, `GenerateKeysView.axaml(.cs)`,
  `GenerateLicenseView.axaml(.cs)`, `ValidateLicenseView.axaml(.cs)`.

**Created — tests (`tests/Enigma.Licensing.Desktop.UnitTests/`)**

- `Enigma.Licensing.Desktop.UnitTests.csproj` — net10.0, MTP-native (xunit.v3 + coverlet, no Test.Sdk).
- `Hosting/ServiceCollectionExtensionsTests.cs`, `Settings/AppSettingsStoreTests.cs`,
  `ViewModels/MainWindowViewModelTests.cs`, `ViewModels/SettingsViewModelTests.cs`,
  `ViewModels/ThemeOptionTests.cs`, `Resources/AppInfoTests.cs`.
- `TestDoubles/RecordingLogger.cs`, `FakeAppSettingsStore.cs`, `FakeThemeService.cs`.

**Modified**

- `Enigma.Licensing.slnx` — both new projects, in their existing solution folders.
- `Directory.Packages.props` — the hosting/logging trio and the version-coupled Avalonia + Enigma control
  and icon set, each with the reason for its pin; the placeholder note listing them as future work removed.
- `docs/roadmap.md`, `docs/plan/FEATURE-179C.md` — statuses.

## Deviations & follow-ups

- **The app-service wrappers other than the theme service were deferred to the phase that needs them.** The
  plan's item-level *Folders* bullet lists `Services/` as holding `IThemeService`, `INotificationService`,
  `IConfirmationService` and a file-dialog adapter. PHASE01's own numbered steps name only the theme service
  and the settings store ("app services", step 3), and the other three have no caller until PHASE02–04:
  a notification service with nothing to report and a confirmation service with no question to ask are dead
  code whose method signatures would be guesses at later phases' workflows. The three control-library hosts
  and both storage-provider handovers *are* wired in this phase, exactly as step 4 requires, so each wrapper
  is a new file rather than a change to the composition root. **PHASE02 adds `IFileDialogService` (+
  `IPickedFile`/`ISaveTarget`), the overlay wrapper and the info-bar wrapper**; the `CS0104` alias structure
  they need is already in place.
- **Strings are inline, with no `.resx`** — the plan's validated localization decision. This is the one place
  the app deliberately diverges from the Enigma.HardCopy pattern it otherwise follows exactly; HardCopy is
  the only sibling app with a `.resx`, and Enigma.Backup, Draw and the predecessor desktop app all inline.
  There is consequently no `Strings` class and no reflection test over one.
- **The preference directory is `Enigma.Licensing`, not `EnigmaLicensing`** as the plan's shorthand wrote it
  — matching the assembly name and the house pattern (`Enigma.HardCopy/settings.json`). Asserted by
  `DefaultFilePath_IsTheApplicationsOwnFolderInTheUsersConfiguration`.
- **The about section shows the project URL as selectable text rather than opening it.** Launching a browser
  is a platform action needing a seam of its own, and no other part of this application needs one. The NuGet
  package link is deliberately absent until `FEATURE-57BC` actually publishes the package — a dead link in
  an about box is worse than no link.
- **The app icon is the predecessor's `appicon.ico`**, copied from
  `src/Enigma.LicenseManager.Desktop/Assets/`, as the plan directs. **Follow-up: distinct branding before
  v1.0.0** — it is currently the same artwork as a library the release notes call a clean break from.
- **No `<Version>` in the app csproj**, so the about section reports the SDK default `1.0.0` for both the app
  and the library. `FEATURE-57BC-PHASE01` sets the real numbers, and must bump `app.manifest`'s
  `assemblyIdentity` in the same change (Windows requires the 4-part form there).
- **`net8.0` in `Enigma.Licensing.UnitTests` remains the known `NETSDK1138` time bomb** recorded in
  CLAUDE.md; the new desktop test project targets net10.0 only and is unaffected.
- No line-ending (CRLF) inconsistency was observed in any file touched by this phase.

## Build/test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release --no-incremental
  → Build succeeded.  0 Warning(s)  0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal
  → 0 lines matching AVLN (XAML warnings bypass TreatWarningsAsErrors, so they are counted by hand)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → total: 918   failed: 0   succeeded: 918   skipped: 0   (12.9 s)
```

918 cases, up from 836: **82 new** across the desktop suite — the container wiring and its lifetimes, the
preference file and its nine ways of being useless, the rail's composition, the appearance selector's
apply-and-persist-exactly-once contract, and the about section's shape.

**Manual smoke (Linux/Wayland, Release, `dotnet run`):**

- The app starts and shuts down cleanly. `Application started` / `Application is shutting down` from
  `Microsoft.Hosting.Lifetime`, and **no `Navigation failed` line** — which is the evidence that the window
  resolved, the three hosts registered, the page factory built the first page and its ViewModel, and
  `desktop.Exit` stopped the host.
- **Both variants render from the stored preference.** Seeding `~/.config/Enigma.Licensing/settings.json`
  with `"theme": "Dark"` and then `"Light"` produced the shell in each variant, confirming the theme is
  applied before the window is shown rather than flashing and correcting.
- **The Settings page renders end to end**: the `SettingsCard` with the appearance `ComboBox` showing the
  stored variant, and the `SettingsCardExpander` open on the app version, the library version and the
  project URL. Both `{ei:IconGeometry}` icons (Palette, Info) resolve.
- **Visiting the Settings page neither repaints nor rewrites the preference file** — verified in the running
  app, not just in the unit test: the file's mtime was unchanged after a run that started on that page.
- The interactive theme *change* was not clicked (no input-automation tooling on this machine); its
  behaviour — apply once, persist once, ignore a re-selection and a null — is covered by
  `SettingsViewModelTests`, and both stored variants were confirmed visually as above.

## Acceptance criteria

| Criterion | Met |
|---|---|
| Solution builds zero-warning (AVLN counted manually) | Yes — 0 warnings, 0 AVLN |
| Tests green | Yes — 918/918 |
| App launches with themed pages | Yes — screenshots in Dark and Light |
| Working theme toggle | Stored variant applied at startup in both variants (manual); the toggle's apply/persist contract covered by unit tests |
