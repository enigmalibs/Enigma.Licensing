# FEATURE-2FE8 — Repository & solution bootstrap

**Status:** DONE
**Branch:** `feature/feature-2fe8-bootstrap`
**Type:** single-phase

## Objective

Bootstrap the Enigma.Licensing repository and solution skeleton following the house
`dotnet-solution-setup` ordered checklist, so every later dev starts from a zero-warning,
test-running baseline. `git init -b main` was already performed by the planning flow;
this item covers everything after it.

## Context

Enigma.Licensing is the successor of Enigma.LicenseManager (`/home/jo/Dev/Enigma.LicenseManager`),
rebuilt on Enigma.Core 1.1.0. Reference repos for conventions: Enigma.Core (library discipline),
Enigma.HardCopy / Enigma.Backup (solution shape with a desktop app), Enigma.DataEncryption
(successor-library precedent, multi-target packable lib).

## Scope

Create, in checklist order (owners: git-repo-hygiene → dotnet-solution-config →
dotnet-solution-setup → xunit-v3 → dev-workflow):

1. `.gitignore`, `.gitattributes` — LF everywhere (CRLF for `.bat`/`.cmd`), binary pins,
   `artifacts/` ignored, `.idea/` ignored (git-repo-hygiene templates / Enigma.Core precedent).
2. `.editorconfig` — the full C# house file (Enigma.Core's 216-line shape): root=true, LF,
   spaces (2 for csproj/props/json/xml/slnx, 4 for .cs), file-scoped namespaces, usings outside
   namespace with System first, analyzer severities.
3. `Directory.Build.props` — Authors `Josué Clément`, Copyright `Copyright © 2026 Josué Clément`,
   `LangVersion` 14, `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors` true,
   `EnforceCodeStyleInBuild` true. No solution-wide `<Version>` (the library and the app version
   independently, in their own csproj).
4. `Directory.Packages.props` — CPM (`ManagePackageVersionsCentrally=true`). Initial pins only for
   what the skeleton references: `PolySharp` 1.16.0, `System.Buffers` 4.6.1, `xunit.v3` 3.2.2,
   `coverlet.collector` 10.0.1. Later devs add their pins when the reference lands (comment saying so).
5. `global.json` — SDK `10.0.100`, `rollForward: latestFeature`, `test.runner: Microsoft.Testing.Platform`.
6. `LICENSE.md` — MIT, year 2026, Josué Clément (dotnet-solution-setup template).
7. `README.md`, `RELEASENOTES.md` — created **empty** (0-byte placeholders; filled by FEATURE-57BC).
8. `Enigma.Licensing.slnx` — `/src/` and `/tests/` solution folders.
9. `src/Enigma.Licensing/Enigma.Licensing.csproj` — library, `netstandard2.0;net8.0;net10.0`
   (multi-target reason: published NuGet library, DataEncryption precedent — document in a csproj
   comment), conditional `System.Buffers` + `PolySharp` (PrivateAssets=all) ItemGroups on
   netstandard2.0 only. **No packaging metadata** (added at release by FEATURE-57BC).
   One placeholder source file that compiles (removed by FEATURE-03A9-PHASE01).
10. `tests/Enigma.Licensing.UnitTests/Enigma.Licensing.UnitTests.csproj` — `net8.0;net10.0`
    (mirrors the lib's modern targets so the netstandard2.0 polyfill surface is exercised via net8.0),
    `OutputType=Exe`, MTP-native xunit.v3 + coverlet.collector, `<Using Include="Xunit"/>` as the
    only implicit using, fixture copy-glob (`**/*.json`, `**/*.txt` PreserveNewest) ready for later
    phases. One smoke test.
11. `docs/` structure — already created by the planning flow (roadmap.md, plan/, done/.gitkeep);
    verify only.
12. `CLAUDE.md` — repo agent guide modeled on Enigma.Core's: what the project is, solution layout,
    TFMs/deps, build & test commands (`dotnet build Enigma.Licensing.slnx` /
    `dotnet test --solution Enigma.Licensing.slnx`, `DOTNET_ROOT=~/.dotnet` hint), conventions
    (no ImplicitUsings, error contract, dev-workflow pointer). Kept current by later devs via the
    doc-freshness sweep.

Out of scope: any licensing domain code (FEATURE-03A9), the desktop app project (FEATURE-179C),
packaging metadata, guides, publish scripts (FEATURE-57BC).

## Acceptance criteria

- `dotnet build Enigma.Licensing.slnx -c Release` succeeds with **zero warnings**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release` runs the smoke test green on both TFMs.
- Repo layout matches the structure above; `git status` shows no unintended files (bin/obj ignored).
- README.md and RELEASENOTES.md exist and are empty.
