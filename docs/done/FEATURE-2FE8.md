# FEATURE-2FE8 — Repository & solution bootstrap — **DONE**

## Summary

Stood up the Enigma.Licensing repository and solution skeleton so every later dev starts from a
zero-warning, test-running baseline. Nothing here is licensing code: the deliverable is the
scaffolding — root configuration, a multi-targeted library project with a single throwaway
placeholder type, an MTP-native xUnit v3 test project with one smoke test, and the repo's agent
guide.

Everything was built in the house checklist order (git-repo-hygiene → dotnet-solution-config →
dotnet-solution-setup → xunit-v3 → dev-workflow). `.editorconfig`, `.gitignore` and `.gitattributes`
were copied **byte-identical** from `Enigma.Core` (verified by `md5sum`; `Enigma.Core`'s and
`Enigma.DataEncryption`'s copies are themselves identical, so there is one canonical house template).
The rest follows `Enigma.DataEncryption`'s own bootstrap commit (`554d5fd`,
`chore(FEATURE-67FD): bootstrap repository & solution`) — the closest precedent in the ecosystem for
this exact dev — with the values the plan specifies.

## Files/modules touched

**Created**

| Path | Notes |
|---|---|
| `.gitignore` | Verbatim house template (`artifacts/`, `.idea/`, `bin/`, `obj/` ignored) |
| `.gitattributes` | Verbatim house template — LF everywhere, CRLF for `.bat`/`.cmd`, binary pins |
| `.editorconfig` | Verbatim house template (216 lines) — LF, file-scoped namespaces, usings outside namespace with `System` first, analyzer severities |
| `Directory.Build.props` | Authors/Copyright (Josué Clément, 2026), `LangVersion` 14, `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`. No solution-wide `<Version>` |
| `Directory.Packages.props` | CPM on; pins only what the skeleton references — `System.Buffers` 4.6.1, `PolySharp` 1.16.0, `xunit.v3` 3.2.2, `coverlet.collector` 10.0.1 — with a comment naming the pins later devs add |
| `global.json` | SDK `10.0.100` / `rollForward: latestFeature`; `test.runner: Microsoft.Testing.Platform` |
| `LICENSE.md` | MIT, 2026, Josué Clément |
| `RELEASENOTES.md` | Empty (0 bytes) — filled by FEATURE-57BC-PHASE03 |
| `Enigma.Licensing.slnx` | SLNX solution with `/src/` and `/tests/` folders |
| `src/Enigma.Licensing/Enigma.Licensing.csproj` | Library, `netstandard2.0;net8.0;net10.0` (reason in a csproj comment), `GenerateDocumentationFile`, netstandard2.0-only `System.Buffers` + `PolySharp` (`PrivateAssets=all`). No packaging metadata |
| `src/Enigma.Licensing/BootstrapPlaceholder.cs` | The one placeholder source file — `internal static`, no members. Deleted by FEATURE-03A9-PHASE01 |
| `tests/Enigma.Licensing.UnitTests/Enigma.Licensing.UnitTests.csproj` | `net8.0;net10.0`, `OutputType=Exe`, MTP-native `xunit.v3` + `coverlet.collector`, `<Using Include="Xunit"/>` as the only implicit using, `**/*.json` + `**/*.txt` `PreserveNewest` fixture glob |
| `tests/Enigma.Licensing.UnitTests/SmokeTest.cs` | One `[Fact]`: the library assembly loads on every test TFM |
| `CLAUDE.md` | Repo agent guide — current skeleton vs planned architecture (kept explicitly separated), load-bearing invariants, TFMs/deps, build & test commands with the `DOTNET_ROOT` hint, the two no-repo-change build traps below, conventions, dev-workflow pointer |
| `docs/done/FEATURE-2FE8.md` | This record |

**Modified**

| Path | Notes |
|---|---|
| `README.md` | Truncated to 0 bytes (see *Deviations*) |
| `docs/roadmap.md` | `FEATURE-2FE8` → `IN PROGRESS` → `DONE`; Status column widened so all rows align |
| `docs/plan/FEATURE-2FE8.md` | Status header → `IN PROGRESS` → `DONE` |

**Deleted** — none.

`docs/` (roadmap, `plan/`, `done/.gitkeep`) already existed from the planning flow; scope item 11 was
verification only.

## Deviations & follow-ups

- **`README.md` arrived non-empty.** The repo's root commit (`bab3bca`, authored outside this dev)
  created `README.md` containing `# Enigma.Licensing`, which contradicts scope item 7 and the
  acceptance criterion requiring it to be empty. Raised before building; the maintainer chose to
  follow the plan, so it was truncated to 0 bytes. The heading is preserved in history at `bab3bca`,
  and FEATURE-57BC-PHASE03 writes the real packed README anyway.
- **Branch cut from `develop`, not `main`.** `HEAD` was on `develop` (both branches at the same
  commit); per the dev-workflow rule the dev branch is cut from the current `HEAD` and never from the
  default branch. `feature/feature-2fe8-bootstrap` therefore descends from `develop`.
- **Preconditions needed a maintainer action first.** The repo had zero commits and the planning
  artifacts were uncommitted, so there was no `HEAD` to branch from and the tree was not clean. The
  maintainer committed the planning docs (`498e936`) before this dev started.
- **`GenerateDocumentationFile=true` set at bootstrap** although scope item 9 does not list it.
  FEATURE-03A9-PHASE04 states it "stays on from bootstrap, so missing docs fail", i.e. that phase
  depends on it already being on; `Enigma.DataEncryption`'s bootstrap did the same. Enabling it now
  is free — the library has no public types yet, so `CS1591` cannot fire.
- **The placeholder's name and shape were unspecified.** Chosen: `BootstrapPlaceholder.cs`, an
  `internal static` class. `internal` keeps it off the public surface (so no XML-doc requirement) and
  `static` makes it immune to `CA1812` (uninstantiated internal class) should that rule ever be
  switched on. Its doc comment names FEATURE-03A9-PHASE01 as the deleter.
- **`CLAUDE.md` is longer than `Enigma.Core`'s** (244 vs 114 lines) because it carries the planned
  architecture of three unbuilt work items — the load-bearing invariants a later agent must not
  violate (no canonicalization step before signing, no legacy-format reader, no `Enigma.Core.*` type
  on the public surface, validation never throws). Later devs are expected to trim the "planned"
  sections into "current" ones as phases land.
- **No `InternalsVisibleTo` was declared.** FEATURE-03A9 has internal types, and
  `Enigma.DataEncryption` declared friend access at its own bootstrap — but this plan does not ask
  for it and no test needs it yet. Left to FEATURE-03A9-PHASE01 to add if and when its tests
  actually need internal access.
- **Line endings:** nothing to report — `.gitattributes` enforces `* text=auto eol=lf` from the first
  commit and every file created here is LF, so no CRLF recommendation applies.
- **Follow-up (FEATURE-179C):** the app's publish output goes to `artifacts/`, which `.gitignore`
  already covers; that phase should still check `.editorconfig` gains nothing for `.axaml` beyond the
  existing 2-space `[*.{xml,axaml,xaml}]` section (it does not — the section is already there).
- **Follow-up — `net8.0` EOL is a scheduled build break (~2026-11-10).** The SDK's EOL-TFM check
  (`NETSDK1138`) is skipped for `Library` projects but live for `Exe`, so it applies to the test
  project only. net8.0 leaves support on 2026-11-10; the first SDK that adds `8.0` to
  `_EolNetCoreTargetFrameworkVersions` turns the test project's net8.0 leg into an error under
  `TreatWarningsAsErrors`. A csproj comment now records the fix (drop net8.0 there, or set
  `CheckEolTargetFramework=false`). The library is unaffected.
- **Follow-up — `NuGetAudit` can turn this build red with no repo change.** Restore warnings are
  errors here (the SDK passes `TreatWarningsAsErrors` into the restore tasks), and auditing defaults
  to on at `low` severity, in `all` (transitive) mode on the net10.0 leg. Any new advisory against
  the four pinned packages or their transitive graph fails the build. Correct response then is a
  targeted `NuGetAuditSuppress` or `WarningsNotAsErrors`, never a blanket `NoWarn`.
- **Follow-up — `coverlet.collector` may be inert under the MTP-native runner (unverified).** Its
  only MSBuild hook registers a **VSTest** data collector, and MTP-native `dotnet test` does not run
  the VSTest pipeline; MTP coverage normally needs `Microsoft.Testing.Extensions.CodeCoverage`. The
  reference is kept because the plan mandates it and `Enigma.Core` pins the identical pair, but
  nobody should assume coverage is actually being collected until it is measured.
- **Note on the plan's wording:** deliverable 2 describes "usings outside namespace with System
  first" as enforced. Only the first half is: `csharp_using_directive_placement` and
  `csharp_style_namespace_declarations` are `:warning` (so build errors here), whereas
  `dotnet_sort_system_directives_first` carries no severity and `IDE0055` is `silent` — System-first
  ordering is a convention in this repo, not a build gate. No action; recorded so a later dev does
  not mistake it for one.
- **Documentation freshness sweep:** nothing was made stale (this dev created every doc in the repo;
  `README.md`/`RELEASENOTES.md` are empty by design and there is no `CONTRIBUTING.md`, `CHANGELOG.md`
  or `SECURITY.md` yet — FEATURE-57BC owns those). The maintainer accepted one enrichment: the two
  no-repo-change build traps and the coverage caveat above were added to `CLAUDE.md`'s *Build & test*
  section, in this dev's commit.
- **Independent verification.** Beyond the acceptance criteria, four review passes (literal plan
  compliance, house-convention consistency against `Enigma.Core`/`Enigma.DataEncryption`/
  `Enigma.HardCopy`, compliance with the four owning convention skills, and forward-compatibility
  against the three downstream plans) were run over the finished skeleton, each finding then put
  through an adversarial refutation pass. Plan compliance returned zero findings. Six findings were
  raised in total; four were refuted (three as plan-mandated or scope-deferred, one as a stale
  bundled skill template needing no repo change) and the two that survived were both this record
  claiming the `DONE` status flip before it had been made — corrected above.

## Build/test evidence

Verified from a clean tree on `feature/feature-2fe8-bootstrap`, with `DOTNET_ROOT=~/.dotnet`
(SDK 10.0.103, satisfying the `10.0.100` + `latestFeature` pin):

```
$ dotnet build Enigma.Licensing.slnx -c Release
  Enigma.Licensing -> .../bin/Release/netstandard2.0/Enigma.Licensing.dll
  Enigma.Licensing -> .../bin/Release/net10.0/Enigma.Licensing.dll
  Enigma.Licensing -> .../bin/Release/net8.0/Enigma.Licensing.dll
  Enigma.Licensing.UnitTests -> .../bin/Release/net8.0/Enigma.Licensing.UnitTests.dll
  Enigma.Licensing.UnitTests -> .../bin/Release/net10.0/Enigma.Licensing.UnitTests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test --solution Enigma.Licensing.slnx -c Release
  ...net10.0|x64) passed (503ms)
  ...net8.0|x64) passed (525ms)
Test run summary: Passed!  total: 2  failed: 0  succeeded: 2  skipped: 0
```

**Acceptance criteria**

| Criterion | Result |
|---|---|
| `dotnet build Enigma.Licensing.slnx -c Release` succeeds with zero warnings | ✅ 0 warnings, 0 errors, 3 library TFMs + 2 test TFMs |
| `dotnet test --solution Enigma.Licensing.slnx -c Release` runs the smoke test green on both TFMs | ✅ 2/2 passed (net8.0 + net10.0) |
| Repo layout matches the plan; `git status` shows no unintended files (bin/obj ignored) | ✅ `git status --short` lists only the intended files; `bin/`/`obj/` appear solely under `--ignored` |
| `README.md` and `RELEASENOTES.md` exist and are empty | ✅ both 0 bytes |

Tests added: 1 (`SmokeTest.LibraryAssembly_Loads`), executed on both test TFMs.

Two additional checks beyond the criteria: `<Using Include="Xunit"/>` was confirmed to work with
`ImplicitUsings=disable` (the smoke test compiles with no `using Xunit;`), and `dotnet test` against
the `.slnx` without `--solution` is rejected by the SDK — hence the `--solution` form documented in
`CLAUDE.md`.
