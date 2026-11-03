# FEATURE-FF4B — Release 2.2.0

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-ff4b-release-2-2-0`
**Run:** feature/2026-11-03-icons-msi-release

## Objective

Prepare **Enigma.Licensing.Desktop 2.2.0** — a release of the application, carrying this run's items — the
way FEATURE-B6BB prepared 2.1.0 and following the `dotnet-release` skill: the version, the release notes,
`SECURITY.md`, `docs/RELEASE.md`, a dependency check and a Release pre-flight. The tag and anything
outward-facing stay the maintainer's.

## Context & constraints

- **The version rule is the user's:** `x.y.z` → `x.(y+1).0`. The app is at **2.1.0**
  (`src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` `<Version>`, mirrored by hand in
  `app.manifest`'s `assemblyIdentity` as `2.1.0.0`) → **2.2.0** / `2.2.0.0`.
- **The release is the app's.** "Create a new release of the app". The library is untouched by this run
  (icons, installer) and versions independently (`docs/RELEASE.md`, CLAUDE.md). It stays at **2.1.0**, and
  its `<PackageReleaseNotes>` keep describing 2.1.0 — they describe the package, not the repository.
- **Registry and tags, checked while planning (2026-11-03):** tags `2.0.0` and `2.1.0` exist (`2.1.0` on
  `main` @ `812ecd8`); nuget.org answers `["1.0.0","2.0.0"]` for `enigma.licensing`. So 2.1.0 is tagged but
  its package is not (yet) on nuget.org. That is the maintainer's to act on, and it does not change this
  release: no `2.2.0` tag exists, and the library is not being released.
- **2.2.0 carries** the items of this run that are `DONE` on the run branch before this dev starts:
  FEATURE-BD63 (the icon without the tile) and FEATURE-6D42 (the Windows installer). An item that ended
  quarantined is left out of the notes.
- `RELEASENOTES.md` is the repository's newest-first notes (both components); 2.1.0's section is the shape.
- `SECURITY.md` lists supported versions as one table; with the two components on different lines it has to
  say which line is whose.
- The Avalonia set moves only as a whole, and only with what Enigma.Avalonia.Desktop is built against.
- Never tag, push, pack into `artifacts/` for publishing, or merge into `develop`/`main` inside the run.

## Steps

1. Check `git tag --list 2.2.0` is empty.
2. `<Version>2.2.0</Version>` in the app csproj (and its comment); `app.manifest` `2.2.0.0` (and its comment).
3. `RELEASENOTES.md`: prepend `# Enigma.Licensing v2.2.0 Release Notes` — an application release, the
   library unchanged at 2.1.0 — with *New Features* (the icon, the installer), *Fixes*, *Dependencies*,
   *Compatibility*, *Version*. Every path it quotes is checked against the files.
4. The library csproj's comment above the packaging properties: `<PackageReleaseNotes>` mirror the newest
   section that released the **library** (2.1.0), not simply the top one.
5. `SECURITY.md`: per component — the library's 2.1.x, the application's 2.2.x supported.
6. `docs/RELEASE.md`: say how an application-only release runs (steps 4–6 skipped); any version-specific
   line brought up to date.
7. Dependency check (`dotnet list package --outdated`), logged. Non-coupled patch/minor bumps of **app- or
   test-only** packages applied; the library's dependencies held (bumping one would change the library,
   which is not being released); the Avalonia set held unless Enigma.Avalonia.Desktop moved with it.
8. Pre-flight in Release: a clean build with zero warnings and an `AVLN` count of 0; the whole suite green;
   the app assembly carries 2.2.0; `scripts/publish.ps1` builds the win-x64 zip named `-2.2.0-`.

## Acceptance criteria

- The Release build is clean with zero warnings and `AVLN` 0; the whole suite passes against the run's
  baseline.
- The built app assembly's informational version starts with `2.2.0`; the library's is still `2.1.0`.
- `RELEASENOTES.md` covers every `DONE` item of this run and its quoted paths exist; `SECURITY.md` supports
  the app's 2.2.x and the library's 2.1.x.
- Nothing in the dev runs `git tag`, `git push`, `dotnet nuget push`, or installs the MSI.

## Out of scope

- A library release (version, `<PackageReleaseNotes>`, README callout, pack-verify): nothing in it changed.
- Publishing 2.1.0's package to nuget.org.
- Building the 2.2.0 MSI for distribution (the maintainer runs `buildmsi` after merging).
- Moving the Avalonia set on its own.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version number | App **2.2.0** | The user's rule (y+1, z reset to 0) | 2.1.1; 3.0.0 |
| The library | Stays **2.1.0**; no pack, no `<PackageReleaseNotes>` change | "a new release of the app"; the run changes nothing in the library, and a NuGet version with no change is noise | Moving both to 2.2.0 (an empty library release) |
| The README callout | Left on 2.1 | The README is the package's nuget.org page and the callout describes the package; the installer gets its sentence in the *Desktop application* section (FEATURE-6D42) | "What's new in 2.2" (would describe an app change on the library's page) |
| Release-notes heading | `Enigma.Licensing v2.2.0`, opening with "an application release; the library is unchanged at 2.1.0" | One newest-first file for the repository, as before; the opening removes the ambiguity | A separate notes file for the app (a second chronology) |
| `SECURITY.md` | One table, a row per component line | The two components now sit on different lines; one version column would be wrong for one of them | Listing only 2.2.x |
| Order in the run | Last | The notes describe the icon and the installer, built first | First |
| Committed MSI profile | None | FEATURE-6D42's script generates the profile per build from the version | `msiProfiles/<App>.2.2.0.msipkg.json` |
| Dependencies | App/test-only non-coupled bumps applied; library dependencies and the Avalonia set held | A library dependency bump would change an unreleased package; house rule for coupled sets | Bumping everything |
| Breakdown | One phase | A routine release | Splitting docs from the version |
