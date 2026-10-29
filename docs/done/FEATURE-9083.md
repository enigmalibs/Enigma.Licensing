# FEATURE-9083 — Update NuGet packages

## Summary

Every package pinned in `Directory.Packages.props` is now at its latest stable version on nuget.org
(2026-10-29). A re-run of `dotnet list package --outdated` reports "no updates" for all four projects.

| Package | From | To |
|---|---|---|
| Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent | 12.1.1 | 12.1.4 |
| Enigma.Avalonia.Desktop | 1.0.0 | 1.3.0 |
| Microsoft.Extensions.Hosting, .Logging.Console, .Logging.Debug | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection.Abstractions (library) | 9.0.18 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection (library tests) | 9.0.18 | 10.0.14 |
| System.Text.Json (netstandard2.0 only) | 10.0.12 | 10.0.14 |
| xunit.v3 | 3.2.2 | 4.0.2 |
| coverlet.collector | 10.0.1 | 10.1.0 |

Nothing needed a code change.

- The coupled Avalonia set moved as one unit. Enigma.Avalonia.Desktop 1.3.0's nuspec pins Avalonia and
  Avalonia.Themes.Fluent 12.1.4, CommunityToolkit.Mvvm 8.4.2 (unchanged) and Enigma.Core ≥ 1.0.0 (still
  closed by the library's 2.0.0).
- Its release notes for 1.1.0 to 1.3.0 are backward compatible. They add `InfoBar.DisplayDuration`
  (default `null`, the 1.0 behaviour), a secondary ContentDialog background and pastel Dark severity
  fills. The last one changes how the validate page's verdict cards look in Dark: those cards use the
  theme's `Enigma*Background/BorderBrush` severity pairs, so they pick up the new tints automatically.

## Files/modules touched

- Modified:
  - `Directory.Packages.props` (versions and the comments that name them)
  - `CLAUDE.md`
  - `docs/RELEASE.md` (dependency-floor table)
  - `docs/roadmap.md`, `docs/plan/FEATURE-9083.md`
- Created: `docs/done/FEATURE-9083.md`

## Deviations & follow-ups

- None from the plan. No package was held back.
- The DI abstractions floor moved from 9.x to 10.x. This reverses a decision CLAUDE.md recorded, and
  FEATURE-82E5 reaffirmed. The reason is written in three places: CLAUDE.md, the props comment and the
  plan's decision table. FEATURE-B6BB's release notes must say it.
- In the same CLAUDE.md area, "the same three TFMs" (Enigma.Core, Ulid/DeviceId) and "all three TFMs"
  were stale since FEATURE-A0EB. They were corrected here.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| History in CLAUDE.md (*Shipped* sections of FEATURE-179C-PHASE01, 6C54, 82E5) | Left as history; one parenthetical on PHASE01 | Those sections record what shipped then. Only the current-state bullets were rewritten |
| The DI floor's transitive pulls | Recorded at the resolved versions (Bcl.AsyncInterfaces 10.0.14, Threading.Tasks.Extensions 4.6.3) | Read from `dotnet list package --include-transitive`. FEATURE-B6BB re-audits licences on this graph |

## Build/test evidence

- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**. That includes the xunit.v3
  4.x analyzers under `TreatWarningsAsErrors`.
- `dotnet build src/Enigma.Licensing.Desktop/… -c Release --no-incremental -v normal | grep -c AVLN`: **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **712/712 passed** (426 + 286). This includes
  `NotificationServiceTests` (the info-bar timing contract), `BusyOverlayServiceTests` and
  `PublicSurfaceIsolationTests` (same DI assembly name).
- `dotnet list Enigma.Licensing.slnx package --outdated`: no updates for any project.

## Documentation sweep

- `CLAUDE.md`: the pinned-versions bullet, the DI-floor bullets (*Shipped* and *Target frameworks*) and the
  desktop pins bullet. Fixed.
- `docs/RELEASE.md`: the dependency-floor table. Fixed.
- README: names no package version beyond Enigma.Core 2.0.0, which is unchanged. Nothing to do.
