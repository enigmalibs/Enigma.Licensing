# FEATURE-11BB — Publish scripts for Windows and Linux

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-11bb-publish-scripts`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

One-command publish scripts on both platforms, part of the uniform baseline:

- Windows — `packaging/windows/publish.ps1` and `packaging/windows/publish.bat`, both running
  `dotnet publish <src/Enigma.Licensing.Desktop> -c Release -r win-x64 -p:PublishReadyToRun=true`;
- Linux — `packaging/linux/publish.sh`, next to `install.sh`/`uninstall.sh`, running
  `dotnet publish <src/Enigma.Licensing.Desktop> -c Release -r linux-x64 -p:PublishReadyToRun=true`.

## Context & constraints

- **`scripts/publish.sh` / `scripts/publish.ps1` already exist and do something else**: the release
  pipeline (FEATURE-57BC-PHASE04) — self-contained single-file builds per RID, zipped under `artifacts/`,
  named from the MSBuild version, driven by `docs/RELEASE.md` §7. They are not the spec's command.
- The reference is Enigma.GitClient's `packaging/windows/publish.ps1` / `publish.bat` and
  `packaging/linux/publish.sh` (FEATURE-0D4E): comment header naming the command and the output folder;
  project resolved from the script's own directory; no arguments; exit code passed on (`exit $LASTEXITCODE`,
  `exit /b %ERRORLEVEL%`, `exec`).
- `.gitattributes`: `*.bat` CRLF, `*.sh` LF. `publish.sh` is staged `100755`.
- On the .NET 10 SDK a RID publish is framework-dependent unless told otherwise; output lands in
  `src/Enigma.Licensing.Desktop/bin/Release/net10.0/<rid>/publish/`.

## Steps

1. `packaging/windows/publish.ps1` — the reference adapted; its header says it is the plain publish and
   points at `scripts/publish.ps1` for release zips.
2. `packaging/windows/publish.bat` — `@echo off`, `setlocal`, project from `%~dp0..\..\src\Enigma.Licensing.Desktop`,
   the command, `exit /b %ERRORLEVEL%`; same header; CRLF.
3. `packaging/linux/publish.sh` — `#!/usr/bin/env bash`, `set -euo pipefail`, project from the script dir,
   `exec dotnet publish …`; header like `install.sh`'s; `100755`, LF.
4. `docs/RELEASE.md`: one line telling these apart from the release-zip scripts. README desktop section:
   the three scripts and where their output lands. `CLAUDE.md` *Project layout*.
5. Verify: run `publish.bat` and `publish.ps1` (win-x64) and `publish.sh` through Git Bash (linux-x64)
   from a directory other than the repo root; each exits 0 and leaves its `publish/` folder holding the app
   and `app.ico`; a copy run where the project does not resolve exits non-zero.

## Acceptance criteria

- The three scripts run from any directory and exit with `dotnet`'s exit code.
- `publish.sh` is `100755` in the index; `publish.bat` is CRLF in the working tree.
- `scripts/publish.*` unchanged.
- Release build warning-free; the whole suite green (nothing in it changes).

## Out of scope

- Self-contained publishing, other RIDs, script arguments.
- Changing the release-zip pipeline.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Existing `scripts/publish.*` | Kept unchanged; headers and RELEASE.md tell the two apart | They are the release pipeline (self-contained zips); MarkdownEditor made the same call | Rewriting them to the spec's command (breaks the release); deleting them |
| Where | `packaging/windows/`, `packaging/linux/` | GitClient's layout, next to the Linux installer | `scripts/` (name clash with the release scripts) |
| `publish.bat` | Calls `dotnet` itself | No dependency on PowerShell's execution policy | Wrapping `publish.ps1` |
| Arguments | None | The spec fixes the command | Passing `%*` / `"$@"` through |
| Self-contained | Not set | The spec's command, verbatim | Adding `--self-contained` |
| Tests | Running the three scripts | Shell scripts; no packaging test exists to extend | A new test pinning script text |
