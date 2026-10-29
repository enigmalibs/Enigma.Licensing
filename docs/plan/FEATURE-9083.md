# FEATURE-9083 — Update NuGet packages

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-9083-update-packages`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every package pinned in `Directory.Packages.props` at its latest stable version on nuget.org, the solution
still building warning-free and its whole suite still green.

## Context & constraints

`dotnet list Enigma.Licensing.slnx package --outdated` on 2026-10-29:

| Package | Pinned | Latest |
|---|---|---|
| Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent | 12.1.1 | 12.1.4 |
| Enigma.Avalonia.Desktop | 1.0.0 | 1.3.0 |
| Microsoft.Extensions.Hosting, .Logging.Console, .Logging.Debug | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection.Abstractions (library) | 9.0.18 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection (library tests) | 9.0.18 | 10.0.14 |
| System.Text.Json (netstandard2.0 only) | 10.0.12 | 10.0.14 |
| xunit.v3 | 3.2.2 | 4.0.2 (major) |
| coverlet.collector | 10.0.1 | 10.1.0 |

Already latest: Enigma.Core 2.0.0, Ulid 1.4.1, DeviceId 6.11.0, PolySharp 1.16.0, System.Buffers 4.6.1,
CommunityToolkit.Mvvm 8.4.2, AvaloniaUI.DiagnosticsSupport 2.2.3, Enigma.Icons / .Phosphor / .Avalonia 1.0.0
(re-checked at build time).

- **The Avalonia set is coupled** (CLAUDE.md): Avalonia, the Fluent theme, Inter, DiagnosticsSupport,
  CommunityToolkit.Mvvm, Enigma.Avalonia.Desktop and Enigma.Icons.* move as a unit. Enigma.Avalonia.Desktop
  1.3.0 is built against Avalonia 12.1.4 (Enigma.Msi's run read its nuspec today), so the set moves to
  12.1.4 + 1.3.0 together. Its nuspec is re-read at build time for the CommunityToolkit.Mvvm and
  Enigma.Core floors.
- 1.0.0 → 1.3.0 may change behaviour the app relies on: `ContentDialog`, `Overlay`, `InfoBar` (the
  `NotificationService` timing contract — `ShowAsync` completing on dismissal), `SettingsCard(Expander)`,
  `NavigationView`, the picker services, and the theme keys `App.axaml` names. The build, the AVLN count
  and the suite (`NotificationServiceTests`, `BusyOverlayServiceTests`) are the check.
- **The DI abstractions floor is a recorded decision** (CLAUDE.md, the props comment, FEATURE-82E5): 9.x,
  so the package does not drag a consumer's `Microsoft.Extensions.*` set forward. See the decision below.
- xunit.v3 4.x is a major; the house `xunit-v3` skill and the xUnit1051 `Token` pattern are re-checked
  against it.

## Steps

1. Re-run `dotnet list package --outdated` to confirm the table.
2. Bump every row in `Directory.Packages.props`; refresh the comments that name versions (the coupled-set
   block, the DI-abstractions floor rationale, the System.Text.Json servicing line, the tests block).
3. Restore and build Release warning-free; fix any API break or new analyzer finding at its cause. Count
   `AVLN` diagnostics on the desktop project (must be 0).
4. Run the whole suite.
5. A package that cannot move without work outside this item's scope is held back at its newest
   compatible version, with the reason in the props comment and the completion doc.
6. `CLAUDE.md` (*Target frameworks & dependencies*, the DI-floor bullets in *Shipped*) and the
   `docs/RELEASE.md` dependency table name the new versions and the new floor's reason.

## Acceptance criteria

- `dotnet list package --outdated` reports nothing, or only rows recorded as held back with a reason.
- Release build warning-free with zero `AVLN` diagnostics; the whole suite green.
- `PublicSurfaceIsolationTests` still green (the allowlisted DI assembly is the same assembly name).

## Out of scope

- Adopting new Enigma.Avalonia.Desktop 1.3.0 features the app does not need.
- The `global.json` SDK pin.
- Re-running the licence audit (FEATURE-B6BB does it on the final graph).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| DI abstractions floor | Raised to 10.0.14 (and the tests' concrete container with it) | The spec asks for latest; the floor existed to avoid dragging consumers onto 10.x, and .NET 8 and .NET 9 (with their `Microsoft.Extensions` 8.x/9.x lines) leave support on 2026-11-10 — 10.x is the only supported line left, on .NET Framework (netstandard2.0) too. Raising a dependency floor breaks no API | Holding 9.0.18 (contradicts the spec for a rationale that expires in 12 days) |
| Majors (xunit.v3 4.x) | Included | "Latest versions" is the spec; GitClient and Msi run 4.0.2; no headless adapter here to block it | Patch/minor only |
| Coupled Avalonia set | Moved as a whole to 12.1.4 with Enigma.Avalonia.Desktop 1.3.0 | The house coupling rule | Avalonia alone; holding the set |
| Incompatible package | Held back, recorded | No unrelated rewrite under a dependency bump | Quarantining the whole item |
| Order in the run | Second, before any UI work | Later devs are written against the libraries that ship | Last, before the release |
