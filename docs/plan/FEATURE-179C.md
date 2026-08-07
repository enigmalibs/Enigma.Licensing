# FEATURE-179C — Desktop application

**Status:** IN PROGRESS
**Type:** multi-phase (4 phases, one branch per phase: `feature/feature-179c-phaseNN-<slug>`)
**Depends on:** FEATURE-03A9 (the app consumes the library's public services)

## Objective

An Avalonia desktop app (`src/Enigma.Licensing.Desktop`, net10.0) for the issuer workflows —
generate key pairs, generate/sign licenses, validate licenses — rebuilt at predecessor parity on
**Enigma.Avalonia.Desktop 1.0.0** + **Enigma.Icons.Avalonia 1.0.0**, following the Enigma.HardCopy
app pattern exactly.

## Design decisions (validated 2026-08-06)

- **Scope: predecessor parity.** Four NavigationView pages — Keys, Generate (with reusable
  license-profile `.json` files), Validate, Settings (persisted light/dark theme + about).
- **Stack & wiring (HardCopy pattern):** minimal synchronous `Program.cs`
  (`BuildAvaloniaApp().StartWithClassicDesktopLifetime`, `UsePlatformDetect` + `WithInterFont` +
  `#if DEBUG WithDeveloperTools`); `App.axaml.cs` owns an `IHost` built with
  `Host.CreateApplicationBuilder()` in `OnFrameworkInitializationCompleted`; app-local
  `AddEnigmaAvaloniaDesktop()` extension registering the six library services (TryAddSingleton —
  the control library ships no extension), plus `AddEnigmaLicensingDesktop()` (MainWindow singleton,
  page **Views transient**, page **ViewModels singleton**, settings store, app services) and the
  library's `AddEnigmaLicensing()`; before showing the window: `RegisterHost` ×3
  (ContentDialog/Overlay/InfoBar hosts as last children of the root panel) and
  `SetStorageProvider` ×2; `desktop.Exit` stops the host.
- **Theme:** keep `<FluentTheme/>` in `Application.Styles`; merge
  `avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` as a **ResourceInclude** in
  `Application.Resources`; window `Background="{DynamicResource EnigmaBackgroundBrush}"`;
  `Enigma*` brushes always via `DynamicResource`; app style classes (page/h1/h2/body/hint/card)
  per HardCopy.
- **MVVM:** CommunityToolkit.Mvvm (`ObservableObject`/`ObservableValidator`, `RelayCommand`/
  `AsyncRelayCommand`, semi-auto properties `set => SetProperty(ref field, value)`).
  Compiled bindings on (`AvaloniaUseCompiledBindingsByDefault`), `x:DataType` everywhere.
- **Icons:** Enigma.Icons.Avalonia (`{ei:IconGeometry ...}` for NavigationItem icons,
  `<ei:Icon Kind="..."/>` where themed/bound).
- **Logging:** Microsoft.Extensions.Logging Console + Debug (house pattern; predecessor's NLog is
  not carried over). Library stays log-free.
- **Localization:** English only, no `.resx` (consistent with all sibling apps).
- **App icon:** reuse the predecessor's `appicon.ico` as placeholder branding
  (`src/Enigma.LicenseManager.Desktop` asset) until distinct branding exists — note it in the
  completion doc as a follow-up.
- **Packages (CPM pins added in PHASE01, coupled Avalonia set bumped only as a unit):**
  Avalonia/Avalonia.Desktop/Avalonia.Themes.Fluent/Avalonia.Fonts.Inter 12.1.1,
  AvaloniaUI.DiagnosticsSupport 2.2.3 (Debug-only IncludeAssets/PrivateAssets condition),
  Enigma.Avalonia.Desktop 1.0.0, Enigma.Icons/Enigma.Icons.Phosphor/Enigma.Icons.Avalonia 1.0.0
  (pinned explicitly even where transitive — house convention), CommunityToolkit.Mvvm 8.4.2,
  Microsoft.Extensions.Hosting 10.0.10, Microsoft.Extensions.Logging.Console/.Debug 10.0.10.
- **csproj:** OutputType WinExe, net10.0, `ApplicationManifest` app.manifest, `ApplicationIcon`,
  `AvaloniaResource Include="Assets/**"`, BuiltInComInteropSupport, trim-native-symbols publish
  target (HardCopy precedent).
- **Folders:** `Assets/`, `Hosting/`, `Services/` (app-own wrappers: IThemeService,
  INotificationService over InfoBar, IConfirmationService over ContentDialog, file-dialog adapter),
  `Settings/` (AppSettings, IAppSettingsStore/AppSettingsStore with injectable file path, AppTheme
  enum), `Models/` (LicenseProfile), `ViewModels/`, `Views/`.
- **Tests:** `tests/Enigma.Licensing.Desktop.UnitTests` (net10.0, MTP-native xunit.v3) covering
  ViewModels, settings store, and profile persistence — no UI automation.
- **Error UX:** failures surface via InfoBar (transient) and ContentDialog (blocking decisions);
  empty/invalid-file states produce a clear message, never a crash.

---

## PHASE01 — App shell, hosting, theme & settings — **DONE**

Branch: `feature/feature-179c-phase01-shell`

1. `src/Enigma.Licensing.Desktop` project (csproj per design) + slnx entry; CPM pins above.
2. `Program.cs`, `App.axaml`(+`.cs`) with IHost wiring, theme merge, style classes; `app.manifest`;
   Assets (icon).
3. `Hosting/` extensions (`AddEnigmaAvaloniaDesktop`, `AddEnigmaLicensingDesktop` with lifetime
   rationale comments) — registers `AddEnigmaLicensing()` from the library too.
4. `MainWindow.axaml`: NavigationView (Keys/Generate/Validate items + Settings footer item, icon
   geometries via `{ei:IconGeometry}`), host controls last in the root panel (`x:Name`d), window
   background brush; `RegisterHost`/`SetStorageProvider` calls; `NavigationFailed` → ILogger;
   initial `SelectedItem`.
5. `Settings/`: AppSettings (theme), AppSettingsStore (JSON file under
   `Environment.SpecialFolder.ApplicationData`/EnigmaLicensing, injectable path), IThemeService
   applying + persisting the variant; stored theme applied before the window shows.
6. `SettingsView` + VM: theme toggle (Light/Dark/System), about section (app version, library
   version, links). Placeholder content pages for Keys/Generate/Validate (filled by later phases).
7. `tests/Enigma.Licensing.Desktop.UnitTests` project + slnx entry: settings-store round-trip
   (temp path), theme service behavior, MainWindowViewModel basics, DI resolution smoke test of
   the app service graph (headless — no Avalonia lifetime needed for VM/services).

Acceptance: solution builds zero-warning (check AVLN counts manually — XAML warnings bypass
TreatWarningsAsErrors); tests green; app launches with themed empty pages and working theme toggle
(manual smoke, noted in completion doc).

## PHASE02 — Keys page — **TODO**

Branch: `feature/feature-179c-phase02-keys-page`

1. `GenerateKeysViewModel` + view: algorithm picker (ML-DSA-44/65/87, RSA-SHA256/384/512 — default
   **ML-DSA-65**), RSA key-size picker shown for RSA only (2048/3072/4096/8192 — default **4096**,
   predecessor desktop default), optional private-key password + confirmation (validated match),
   output via `IFileDialogService` save dialogs (`<name>.public.key.json` / `<name>.private.key.json`
   suggested names).
2. Generation via `IKeyService` on a background task with Overlay progress (RSA-8192 is slow);
   success InfoBar shows the new **keyId fingerprint** with copy action; failures → InfoBar error.
3. VM tests: password mismatch blocks command, algorithm/size coupling, service invoked with
   expected arguments (fake IKeyService), busy-state toggling.

Acceptance: build/tests green; a key pair generated in the app loads back through the library.

## PHASE03 — Generate page & license profiles — **TODO**

Branch: `feature/feature-179c-phase03-generate-page`

1. `GenerateLicenseViewModel` + view: form for ProductId (required), Owner, DeviceId (+ "use this
   machine" button calling `DeviceIdentifier.Generate()`), optional ValidFrom / ValidUntil date
   pickers (each individually optional; ValidFrom ≤ ValidUntil validated), Features editor
   (add/remove string chips or list), Claims editor (key/value grid add/remove), private key file
   picker + password field, generate & save via `ILicenseGenerator` (suggested name
   `<product>.license.json`).
2. **License profiles** (predecessor parity): `Models/LicenseProfile` — form fields + signing-key
   *path* + algorithm, **never the password**; save/load as `.json` via file dialogs; loading
   prefills the form.
3. Validation UX: `ObservableValidator` annotations; generate command disabled until valid;
   InfoBar success (with license id) / error.
4. VM tests: request mapping (form → `LicenseRequest`, empty-optional handling), window-order
   validation, profile round-trip excludes password, fake `ILicenseGenerator` invocation, device-id
   fill.

Acceptance: build/tests green; a license generated in the app validates in the app and via the
library API.

## PHASE04 — Validate page — **TODO**

Branch: `feature/feature-179c-phase04-validate-page`

1. `ValidateLicenseViewModel` + view: pick license file + public-key file (file dialogs), optional
   expected ProductId / DeviceId inputs ("use this machine" helper), validate via
   `ILicenseValidator`.
2. Result display: prominent verdict (valid / failure reason with `LicenseFailure` mapped to a
   human message + InfoBarSeverity), and a read-only field card of the parsed license — id,
   product, owner, window, device, features, claims, algorithm, keyId — shown whenever the payload
   parsed, even for invalid licenses.
3. VM tests: verdict mapping per `LicenseFailure`, parsed-license display on failure, fake
   validator interactions, missing-file handling → message not crash.

Acceptance: build/tests green; end-to-end manual pass documented in the completion doc — generate
keys → generate license → validate (both algorithms), plus one expired and one tampered case
showing correct verdicts.

## Item-level acceptance criteria

- Full solution builds zero-warning; all test projects green.
- App runs on Linux (dev machine) with all four pages functional; theme persists across restarts.
- No ViewModel references a `Window` or Avalonia dialog type directly — all through the six
  library services / app service wrappers.
