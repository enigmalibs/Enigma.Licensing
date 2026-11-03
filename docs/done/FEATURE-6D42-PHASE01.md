# FEATURE-6D42-PHASE01 — Vendor the Enigma.Msi worker CLI

**Item:** FEATURE-6D42 — Windows MSI installer
**Branch:** `feature/feature-6d42-phase01-vendor-msi-worker`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

`packaging/windows/Enigma.Msi.Worker/` now holds the headless worker of Enigma.Msi — the tool every Enigma
app builds its MSI with — copied from Enigma.GitClient's vendored folder: the Enigma.Msi **1.7.0** build
(`a7a1bfb`, WixSharp_wix4 2.14.2), 20 files (3.3 MB) plus a `README.md`. PHASE02's `buildmsi.ps1` runs it.

- The 20 binaries and the `.exe.config` are GitClient's git blobs exactly (`git hash-object` equal for
  each). The `README.md` names this repository's MSI (`artifacts/Enigma.Licensing.X.Y.Z.msi`) and gains one
  *Source* row saying where the copy came from; the rest — source, requirements, `.wixsharp/`, licences,
  refresh steps — is GitClient's text.
- `.gitignore` ignores `packaging/windows/Enigma.Msi.Worker/.wixsharp/`, the folder WixSharp writes beside the
  worker on its first build, with GitClient's comment.

## Files/modules touched

- Created: `packaging/windows/Enigma.Msi.Worker/` (21 files), `docs/done/FEATURE-6D42-PHASE01.md`.
- Modified: `.gitignore`, `docs/roadmap.md`, `docs/plan/FEATURE-6D42.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Provenance in the README | One *Source* row, "Copied from Enigma.GitClient's folder, the same git blobs" | A reviewer can check the copy with `git hash-object` instead of rebuilding Enigma.Msi 1.7.0 |
| `.gitignore`'s missing final newline | Ended the last existing line before appending | Otherwise the new comment would have joined `FodyWeavers.xsd` on one line |

## Deviations & follow-ups

- No CLAUDE.md or README change in this phase: nothing they say became wrong, and PHASE02 documents the
  worker together with the script that uses it.
- Enigma.Msi has tagged 1.8.0 since; refreshing the worker family-wide is a separate decision (the README's
  *Refreshing it* section is the procedure).

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BD63.md` (0 warnings, `AVLN` 0, 783/783).
- `git hash-object` of each of the 21 copied files against `git ls-files -s` in Enigma.GitClient: **21 same**
  (before the README edit).
- `git status --porcelain --ignored`: `packaging/windows/Enigma.Msi.Worker/` untracked (to be added);
  `git check-ignore` matches no vendored file; a probe file under `.wixsharp/` showed as `!!` (ignored),
  then was removed.
- `Enigma.Msi.Worker.exe` with no arguments: prints `No arguments were given.` and its usage, **exit 2**. It
  wrote no `.wixsharp/` folder.
- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **783/783 passed**.
- Fix budget: 0 cycles used.

## Documentation sweep

- Nothing stale (see *Deviations*).
