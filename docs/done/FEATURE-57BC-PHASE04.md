# FEATURE-57BC-PHASE04 — Release runbook, publish scripts & pack-verify

**Branch:** `feature/feature-57bc-phase04-runbook`
**Status:** DONE

## Summary

The last phase of the release, and the one that turns three phases of metadata and prose into
something a maintainer can actually run. `docs/RELEASE.md` is the human checklist from pre-flight to
post-publish verification; `scripts/publish.sh` and `scripts/publish.ps1` build the desktop app's
distribution zips; and the pack-verify pass — packed into a throwaway directory, inspected, then
deleted — confirms that the artifact this repository would push to nuget.org is the one PHASE01–03
described.

**Everything outward-facing stays with the user.** No tag was created, nothing was pushed, and no
`.nupkg` was written into the repository's `artifacts/`. The runbook is printed, not executed.

Pack-verify passed every assertion: `Enigma.Licensing.1.0.0.nupkg` (227 115 bytes), **that file and
nothing else** in the verify directory, a 6 148-byte `README.md` and a 1 063-byte `LICENSE.md`
embedded, three `lib/` groups each carrying its `.dll` and `.xml`, and dependency groups matching the
floors the csproj declares — including no `Microsoft.NET.ILLink.Tasks`, confirming PHASE01's claim
that `IsAotCompatible`'s build task never reaches the nuspec. Both app zips build, expand into their
own directory, and carry the executable plus `README.md` and `LICENSE.md` and nothing else.

## Files/modules touched

**Created**

- `docs/RELEASE.md` — the release runbook, from the `dotnet-release` template with its five
  placeholders filled (`Enigma.Licensing`, `Enigma.Licensing.slnx`,
  `src/Enigma.Licensing/Enigma.Licensing.csproj`, `src/Enigma.Licensing/`, `main`). Three
  repo-specific corrections to the template, all recorded under *Deviations* below: the
  `dotnet test --solution` form, the `DOTNET_ROOT=~/.dotnet` prefix, and the `AVLN` XAML check. It
  also gains a **step 7** for the desktop artifacts, which the library-shaped template has no place
  for but the plan's printed runbook requires.
- `scripts/publish.sh` — the bash publisher, mode `755`. Reads the app version, publishes
  self-contained single-file per RID, copies `README.md`/`LICENSE.md` into the staging directory, and
  zips from the staging *parent* so the archive expands into its own folder. Defaults to both RIDs;
  takes an explicit list as arguments.
- `scripts/publish.ps1` — its PowerShell twin, same artifacts, so a release can be cut from Windows.
  `Compress-Archive` does not carry the Unix executable bit, which is why the linux-x64 artifact built
  on Windows needs `chmod +x` after extraction; the script's comment and `docs/RELEASE.md` step 7 both
  say so, and Info-ZIP (the bash script) preserves it.

**Modified**

- `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` — the
  `TrimNativeSymbolsFromPublish` comment only. See *The `DebugType=none` conflict* below.
- `docs/roadmap.md` — PHASE04 `TODO` → `IN PROGRESS` → `DONE`, and **`FEATURE-57BC` itself → `DONE`**:
  this is the item's last phase.
- `docs/plan/FEATURE-57BC.md` — the same on the phase heading and the item's `**Status:**` line, plus
  the amendment note recording the `DebugType` decision.
- `CLAUDE.md` — the documentation-freshness sweep, accepted. Seven places: *Current state* now says
  `FEATURE-57BC` is complete and, with it, every planned work item — and states plainly that **nothing
  outward-facing has happened** (no tag, no push, no repository yet); the PHASE01 *Shipped* bullet stops
  deferring pack-verify to this phase; a new *Shipped* subsection covers the runbook, the two scripts,
  the four template departures, the MSBuild version read, the `DebugType=none` reversal and the
  pack-verify assertions; *Build & test* gains a pointer saying its two commands are the release
  pre-flight and that `docs/RELEASE.md` is the maintainer's to run; the layout tree replaces
  `scripts/ planned` with the three real files; its preamble stops saying some directories are planned;
  and the dependency chain at the foot reads *every item and every phase is `DONE`*, with a note that
  the next work item is a new one.

**Not touched**

No source file and no test. The csproj edit is a comment.

## The version is asked of MSBuild, not scraped

The plan specifies `dotnet msbuild -getProperty:Version`, and the reason is worth keeping: HardCopy's
scripts `sed` the value out of `Directory.Build.props` because HardCopy has one solution-wide
`<Version>`. This repository deliberately does not — the library and the app version independently, so
the app's version lives in its own csproj, and evaluating the project is both correct and immune to a
future `Directory.Build.props` default. The scripts take **no version argument**, so an artifact
cannot be named something the executable disagrees with.

## The `DebugType=none` conflict

The plan's step 2 flag list and the desktop csproj disagreed, and the disagreement was surfaced before
any code was written rather than resolved silently:

- The csproj's `TrimNativeSymbolsFromPublish` comment (shipped in `FEATURE-179C-PHASE01`) said the
  *managed* `.pdb` files "are deliberately kept: they are ~60 KB and are what turns a user's crash
  report into a file and a line number".
- `-p:DebugType=none` suppresses exactly those, and the HardCopy scripts the step names as its pattern
  do not pass it.

**The maintainer chose the plan**, so the scripts pass `-p:DebugType=none` and the csproj comment was
corrected in the same change: it now says the managed symbols are left to the invoking build — the
release scripts suppress them, an ordinary publish keeps them. The target itself is unchanged and
still earns its place: the *native* `libSkiaSharp.pdb`/`libHarfBuzzSharp.pdb` come from the runtime
packages rather than from `DebugType`, so nothing but the target drops them. Verified on the built
artifacts — **zero `.pdb` and zero `.xml` entries in either zip.**

`CLAUDE.md` turned out not to repeat the managed-pdb claim anywhere, so no correction was needed
there; `docs/done/FEATURE-179C-PHASE01.md` mentions only the *native* symbols and remains accurate.

## Pack-verify

Packed with `dotnet pack src/Enigma.Licensing/Enigma.Licensing.csproj -c Release -o <scratch>`, into
the session scratchpad rather than a repository directory — `artifacts/` is gitignored but
`artifacts-verify/` would not have been. **The directory was deleted after inspection.**

| Assertion | Result |
|---|---|
| `.nupkg` version matches the release | ✅ `Enigma.Licensing.1.0.0.nupkg` |
| The directory holds the `.nupkg` and nothing else — no `.snupkg` | ✅ one file, 227 115 bytes |
| `README.md` embedded and **non-empty** | ✅ 6 148 bytes |
| `LICENSE.md` embedded | ✅ 1 063 bytes |
| nuspec `<version>` | ✅ `1.0.0` |
| nuspec `<title>` | ✅ `Enigma.Licensing — .NET License Generation & Validation` |
| nuspec `<license type="file">` | ✅ `LICENSE.md` |
| nuspec `<readme>` | ✅ `README.md` |
| nuspec `<releaseNotes>` | ✅ present, ends `See RELEASENOTES.md for the full details.` |
| Dependency floors per TFM | ✅ table below |
| XML documentation ships per TFM | ✅ `lib/{netstandard2.0,net8.0,net10.0}/Enigma.Licensing.xml` |

```
netstandard2.0  DeviceId 6.11.0 · Enigma.Core 1.1.0 · M.E.DependencyInjection.Abstractions 9.0.18
                Ulid 1.4.1 · System.Buffers 4.6.1 · System.Text.Json 10.0.10
net8.0          DeviceId 6.11.0 · Enigma.Core 1.1.0 · M.E.DependencyInjection.Abstractions 9.0.18 · Ulid 1.4.1
net10.0         DeviceId 6.11.0 · Enigma.Core 1.1.0 · M.E.DependencyInjection.Abstractions 9.0.18 · Ulid 1.4.1
```

Exactly what the csproj declares: `System.Buffers` and `System.Text.Json` on `netstandard2.0` only,
**PolySharp absent** everywhere (compile-only, `PrivateAssets=all`), and **no
`Microsoft.NET.ILLink.Tasks`** in any group. `<licenseUrl>` carries NuGet's own
`https://aka.ms/deprecateLicenseUrl` redirect, which the SDK emits automatically alongside a
`<license type="file">` — not a metadata error.

## App artifacts

`scripts/publish.sh` run for both RIDs on this machine (`pwsh` is not installed here, so the
PowerShell twin was reviewed rather than executed — see *Deviations*):

| Artifact | Size | Contents |
|---|---|---|
| `Enigma.Licensing.Desktop-1.0.0-win-x64.zip` | 48 714 267 B (47 MB) | `…-win-x64/` → `Enigma.Licensing.Desktop.exe` (54.4 MB), `README.md`, `LICENSE.md` |
| `Enigma.Licensing.Desktop-1.0.0-linux-x64.zip` | 46 589 155 B (45 MB) | `…-linux-x64/` → `Enigma.Licensing.Desktop` (53.9 MB, mode `755`), `README.md`, `LICENSE.md` |

Four entries each — the directory, the single-file executable, and the two documents. No `.pdb`, no
`.xml`, no loose runtime assemblies. The Linux binary's executable bit survives the zip
(`-rwxr-xr-x`), as the Info-ZIP path is meant to. Both live under `artifacts/`, which is gitignored;
the staging directories beside them account for a further ~104 MB and are safe to delete (the scripts
clear a stale stage on the next run).

## Deviations & follow-ups

- **`docs/RELEASE.md` diverges from the template in four places, all deliberate.** (1) The test command
  is `dotnet test --solution Enigma.Licensing.slnx -c Release` — the .NET 10 SDK in Microsoft Testing
  Platform mode rejects the template's `dotnet test <Solution>.slnx`. (2) Every command carries the
  `DOTNET_ROOT=~/.dotnet` prefix this repository's `CLAUDE.md` documents, with a line saying to drop it
  elsewhere. (3) A pre-flight item for the `AVLN` XAML-warning count, because those bypass
  `TreatWarningsAsErrors` and a desktop build can print *Build succeeded* while carrying them. (4) A
  **step 7** for the desktop zips, which the library-only template does not model.
- **Three additions to the plan's flag list, all from the "HardCopy pattern" the step invokes**:
  `-p:GenerateDocumentationFile=false` (the app turns `GenerateDocumentationFile` on for build-time
  discipline; the XML files mean nothing to someone running the app), the `README.md`/`LICENSE.md` copy
  into the staging directory, and the stale-artifact cleanup at the top of each RID loop.
- **`scripts/publish.ps1` was not executed** — `pwsh` is not installed on this machine. It is a
  line-by-line twin of the bash script, which was run; the one behavioural difference (the executable
  bit) is documented in both the script and the runbook. Worth a run on Windows before the first
  release that ships from there.
- **No git remote and no tags exist yet.** `git remote -v` is empty and `git tag` lists nothing, so the
  runbook's bare-`X.Y.Z` convention is a house default rather than a match against history, and steps
  2–3 and 5 are inert until the user creates `github.com/enigmalibs/Enigma.Licensing`. The same
  precondition applies to `SECURITY.md`'s reporting flow, as PHASE03 noted.
- **The pack-verify directory was the session scratchpad, not `./artifacts-verify`.** The skill's
  suggested name is not covered by this repository's `.gitignore` (`artifacts/` is), and a verify
  artifact must not be committable even briefly.
- **Line endings:** no CRLF/LF inconsistency observed in the files created or modified by this dev.
  Both scripts are LF, consistent with `.gitattributes` (which reserves CRLF for `.bat`/`.cmd`).

## Build/test evidence

Only a csproj comment changed, so the suite proves nothing moved; the substantive evidence is the
pack and the two publishes above.

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  Build succeeded.
      0 Warning(s)
      0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal | grep -c AVLN
  0

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  Test run summary: Passed!
    Enigma.Licensing.UnitTests.dll (net10.0|x64)         passed (12s 423ms)
    Enigma.Licensing.UnitTests.dll (net8.0|x64)          passed (13s 328ms)
    Enigma.Licensing.Desktop.UnitTests.dll (net10.0|x64) passed (2s 303ms)
    total: 1164   failed: 0   succeeded: 1164   skipped: 0

DOTNET_ROOT=~/.dotnet scripts/publish.sh
  Enigma.Licensing.Desktop 1.0.0
  ==> win-x64    → artifacts/Enigma.Licensing.Desktop-1.0.0-win-x64.zip   (47M)
  ==> linux-x64  → artifacts/Enigma.Licensing.Desktop-1.0.0-linux-x64.zip (45M)
```

**No test was added.** This phase ships no executable code: two shell scripts and a runbook, verified
by running them and inspecting what they produced, which is stronger than a unit test over either
would be.

## Acceptance criteria

| Criterion | Status |
|---|---|
| `docs/RELEASE.md` from the template, placeholders filled | ✅ — with four documented repo-specific corrections |
| `scripts/publish.sh` + `scripts/publish.ps1`, version via `dotnet msbuild -getProperty:Version` | ✅ |
| Zips named `artifacts/Enigma.Licensing.Desktop-<version>-<rid>.zip`, `artifacts/` gitignored | ✅ — `git status` clean of them |
| Pre-flight: Release build zero-warning, whole suite green | ✅ — 0 warnings, 0 `AVLN`, 1164/1164 |
| Pack-verify: every assertion passes, verify directory deleted | ✅ — table above |
| Runbook printed, never run | ✅ — no tag, no push, no pack into `./artifacts` |
| Scripts produce both zips locally | ✅ — win-x64 and linux-x64, contents verified |
