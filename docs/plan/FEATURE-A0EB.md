# FEATURE-A0EB — Drop the net8.0 targets

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-a0eb-drop-net8`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Remove every `net8.0` target from the solution — .NET 8 leaves support on 2026-11-10 — and keep every
other framework exactly as it is: `netstandard2.0` and `net10.0` for the library, `net10.0` for the
desktop app and both test suites.

## Context & constraints

Read on `develop` @ `6d1c16e` (2026-10-29):

- `src/Enigma.Licensing/Enigma.Licensing.csproj` — `<TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>`;
  its comments say "net8.0/net10.0 get the modern runtime surface", and the `System.Buffers` /
  `System.Text.Json` comment says both are framework-provided "on net8.0+".
  `IsAotCompatible` is conditioned on `!= 'netstandard2.0'`, so it follows the remaining modern TFM by itself.
- `tests/Enigma.Licensing.UnitTests/Enigma.Licensing.UnitTests.csproj` — `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`,
  plus the `NETSDK1138` comment that anticipated exactly this removal ("Fix then: drop net8.0 here").
- `Directory.Packages.props` — two comments phrase framework-provided packages against "net8.0+".
- Docs naming the TFM set: `CLAUDE.md` (*Target frameworks & dependencies*, the `IsAotCompatible` bullet,
  the *Build & test* paragraph about the missing .NET 8 runtime and the roll-forward recipe, the
  `NETSDK1138` bullet, the *Conventions* serialization bullet), `docs/RELEASE.md` (pre-flight TFM line,
  the dependency-floor table). `RELEASENOTES.md`'s past sections and `docs/migrations/1.0.0-to-2.0.0.md`
  are history and stay.
- The desktop csproj says "Enigma.Avalonia.Desktop's own floor is net8.0" — a fact about another
  package, not a target of ours: it stays.
- A net8.0 consumer of the package is not excluded: NuGet falls back to the `netstandard2.0` asset.

## Steps

1. Library csproj: `<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>`; reword the TFM comment
   (net10.0 is the current LTS; netstandard2.0 keeps .NET Framework and every older runtime — net8.0
   included — reachable) and the "net8.0+" package comment to "net10.0".
2. Library suite: a single `<TargetFramework>net10.0</TargetFramework>`; the TFM comment rewritten (the
   suite runs the library's net10.0 build; netstandard2.0 has no runtime to execute on); the
   `NETSDK1138` comment removed — it no longer applies.
3. `Directory.Packages.props`: the two "net8.0+" comments reworded; no version changes (FEATURE-9083).
4. Docs: `CLAUDE.md` and `docs/RELEASE.md` name `netstandard2.0` and `net10.0` only; the .NET 8 runtime
   paragraph and the `NETSDK1138` bullet go, since neither can happen any more.
5. Clean Release build, warning-free; the whole suite.

## Acceptance criteria

- `git grep -n net8` finds nothing outside history (`RELEASENOTES.md`'s past sections,
  `docs/migrations/`, `docs/plan|done/`) and the desktop csproj's note about Enigma.Avalonia.Desktop.
- `src/Enigma.Licensing/bin/Release/` holds `netstandard2.0/` and `net10.0/` only after a clean build.
- The Release build has zero warnings; the whole suite is green. The library suite now runs once
  (net10.0), so the total test count drops by the former net8.0 share — recorded.

## Out of scope

- Any package version change (FEATURE-9083).
- A test leg exercising the netstandard2.0 asset (e.g. net472) — new infrastructure; noted as a follow-up.
- The version bump and release notes (FEATURE-B6BB).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which projects | Library and library suite | They are the only `net8.0` targets; the spec keeps every other framework | Library only (would leave a suite testing a target that no longer exists) |
| Suite's TFM element | `TargetFramework` (singular) | One target left; the plural form with one value is noise | Keeping `TargetFrameworks` |
| `CheckEolTargetFramework` | Not set | No EOL target remains to silence | Setting it to false |
| Release notes | Written by FEATURE-B6BB, past sections untouched | Notes are history | Editing the 2.0.0 section |
