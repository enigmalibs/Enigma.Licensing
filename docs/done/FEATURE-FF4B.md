# FEATURE-FF4B — Release 2.2.0

**Branch:** `feature/feature-ff4b-release-2-2-0`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

**Enigma.Licensing.Desktop 2.2.0** is prepared: an application-only release carrying this run's two items,
FEATURE-BD63 (the icon without its tile) and FEATURE-6D42 (the Windows installer). The library is untouched and
stays at **2.1.0**.

- App `<Version>` 2.1.0 → **2.2.0**; `app.manifest` `assemblyIdentity` 2.1.0.0 → **2.2.0.0**; both comments say
  why.
- `RELEASENOTES.md`: a `# Enigma.Licensing v2.2.0 Release Notes` section on top, opening with "the library is
  unchanged and stays at 2.1.0", then *New Features*, *Fixes*, *Dependencies*, *Compatibility*, *Version*.
- The library csproj: only its comment — `<PackageReleaseNotes>` mirror the newest section that released the
  **library**, and an application-only release leaves them and the version alone.
- `SECURITY.md`: a supported line per component — library 2.1.x, application 2.2.x.
- `docs/RELEASE.md`: how an application-only release runs (steps 4–6 skipped, the tag still marks it), and the
  pre-flight now names `app.manifest` and the MSI (step 8).

## Files/modules touched

- Modified: `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj`, `src/Enigma.Licensing.Desktop/app.manifest`,
  `src/Enigma.Licensing/Enigma.Licensing.csproj` (comment only), `RELEASENOTES.md`, `SECURITY.md`, `docs/RELEASE.md`,
  `CLAUDE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-FF4B.md`.
- Created: `docs/done/FEATURE-FF4B.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The Avalonia set (12.1.4 → 12.1.5 available) | Held | Coupled set; Enigma.Avalonia.Desktop 1.3.0, the newest, is built against 12.1.4 |
| Other packages | Nothing to apply | `dotnet list package --outdated` reported no other update in any project |
| `SECURITY.md` shape | One table with a *Component* column, the application's rows back to 1.0.x | The two lines differ now; one version column would be wrong for one component |
| The README callout | Left on 2.1 | It is the package's nuget.org page; the installer already has its sentence in *Desktop application* (FEATURE-6D42) |
| Launching the 2.2.0 zip | Not done | The built assembly's version was read directly (2.2.0); the About dialog reads that same attribute (`AppInfoTests`) |

## Deviations & follow-ups

- **2.1.0's package is not on nuget.org.** Tag `2.1.0` is on `main` (`812ecd8`), but the registry answered
  `["1.0.0","2.0.0"]` on 2026-11-03. Publishing it (`docs/RELEASE.md` steps 4–6, from the `2.1.0` tag) is the
  maintainer's call; this release does not need it.
- The repository tag for this release will be `2.2.0` while the library inside reads 2.1.0 — consistent with
  independent versions, and said in `docs/RELEASE.md`.
- No pack-verify and no snippet gate: neither the package nor the guides or the README quick start moved.
- `artifacts/` (the zip built for the pre-flight) was deleted afterwards.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BD63.md` (0 warnings, `AVLN` 0, 783/783).
- `git tag --list 2.2.0`: empty.
- `dotnet build Enigma.Licensing.slnx -c Release --no-incremental`: **0 warnings, 0 errors**; `AVLN` count **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **786/786 passed**.
- Built versions: `Enigma.Licensing.Desktop.dll` file 2.2.0.0, product `2.2.0+5b616a1…`; `Enigma.Licensing.dll`
  file 2.1.0.0, product `2.1.0+5b616a1…`; `app.manifest` 2.2.0.0.
- `scripts/publish.ps1 win-x64`: exit 0, `artifacts/Enigma.Licensing.Desktop-2.2.0-win-x64.zip` (48.8 MB) with the
  executable, `app.ico` (16,965 bytes — FEATURE-BD63's), `README.md` and `LICENSE.md`.
- `RELEASENOTES.md`'s quoted paths (`packaging/windows/buildmsi.ps1`, `buildmsi.bat`, `publish.ps1`,
  `Enigma.Msi.Worker/`, `scripts/publish.*`) all exist.
- Nothing ran `git tag`, `git push`, `dotnet pack`, `dotnet nuget push`, or installed the MSI.
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md`: *Current state* — 2.1.0 is tagged (no longer "not released"), its package is not on nuget.org, and
  this run's paragraph; a *Shipped* section for this release; *Conventions → Packaging metadata* — the app moved
  alone; *Dev workflow* — this run's items.
