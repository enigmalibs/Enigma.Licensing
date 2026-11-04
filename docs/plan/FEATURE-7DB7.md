# FEATURE-7DB7 — Release 2.3.0

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-7db7-release-2-3-0`
**Run:** feature/2026-11-04-final3-icons-release

## Objective

Prepare **Enigma.Licensing.Desktop 2.3.0**, an application release carrying this run's icon, following the
`dotnet-release` skill and FEATURE-FF4B's application-only shape: the version, the release notes,
`SECURITY.md`, a dependency check and a Release pre-flight. Make sure the MSI `packaging/windows/buildmsi.ps1`
builds is named and versioned 2.3.0. The tag and anything outward-facing are left to the maintainer.

## Context & constraints

- **The version rule is the user's:** `x.y.z` → `x.(y+1).z`. The app is at **2.2.0**
  (`src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` `<Version>`, mirrored by hand in
  `app.manifest`'s `assemblyIdentity` as `2.2.0.0`), so it becomes **2.3.0** / `2.3.0.0`.
- **The release is the app's.** The run changes only the application's icon. The library versions
  independently and stays at **2.1.0**: its `<Version>`, `<PackageReleaseNotes>` and the README callout
  describe the package (`docs/RELEASE.md`, *application-only release*).
- **Registry and tags, checked while planning (2026-11-04):** tags `2.0.0`, `2.1.0` and `2.2.0` exist (`2.2.0`
  on `main` @ `417e490`), and no `2.3.0` tag exists. nuget.org still answers `["1.0.0","2.0.0"]` for
  `enigma.licensing`, so 2.1.0's package is still unpublished. That is the maintainer's to act on, and this
  release does not depend on it.
- **`buildmsi.ps1` carries no literal version.** It reads `<Version>` from the app csproj with `XmlDocument`
  and names the MSI `Enigma.Licensing.<version>.msi`, the way the family reference (Enigma.GitClient) reads
  its own project file. The csproj bump *is* the script's version update. The only thing left to do is to
  verify that the script resolves 2.3.0.
- **`buildmsi.ps1` runs `git clean -dxf`.** It is never run in this repository from an agent. It is verified
  end to end in a scratch clone with this dev's tree committed there (FEATURE-6D42-PHASE02's method).
- **2.3.0 carries** the run items that are `DONE` on the run branch before this dev starts: FEATURE-1A5D. An item
  that ended quarantined is left out of the notes.
- The Avalonia set only moves as a whole, and only with what Enigma.Avalonia.Desktop is built against.
- Never tag, push, pack, install the MSI, or merge into `develop`/`main` inside the run.

## Steps

1. Check that `git tag --list 2.3.0` is empty.
2. Set `<Version>2.3.0</Version>` in the app csproj (and update its comment), and `app.manifest` to `2.3.0.0`
   (and its comment).
3. `RELEASENOTES.md`: prepend `# Enigma.Licensing v2.3.0 Release Notes`. It is an application release, so it
   opens by saying the library is unchanged at 2.1.0, then has *New Features* (the icon), *Fixes*,
   *Dependencies*, *Compatibility* and *Version*.
4. `SECURITY.md`: the application's 2.3.x is supported and 2.2.x is not. The library row is unchanged.
5. `docs/RELEASE.md`: bring any version-specific line up to date. Change nothing else.
6. Dependency check (`dotnet list package --outdated`), logged. Apply non-coupled patch/minor bumps of
   **app- or test-only** packages. Hold the library's dependencies (it is not being released), and hold the
   Avalonia set unless Enigma.Avalonia.Desktop moved with it.
7. Pre-flight in Release:
   - a clean build with zero warnings and an `AVLN` count of 0;
   - the whole suite green;
   - the app assembly carries 2.3.0 and the library's still 2.1.0;
   - `scripts/publish.ps1 win-x64` builds the zip named `-2.3.0-` with the new `app.ico`.
8. `buildmsi.ps1`, end to end in a scratch clone:
   - it builds `artifacts/Enigma.Licensing.2.3.0.msi`;
   - the MSI's tables show `ProductVersion` 2.3.0 and the unchanged upgrade code;
   - its `app.ico` is FEATURE-1A5D's.

   Then delete the clone.

## Acceptance criteria

- The Release build is clean, with zero warnings and `AVLN` 0, and the whole suite passes against the run's
  baseline.
- The built app assembly's informational version starts with `2.3.0`, and the library's is still `2.1.0`.
- `buildmsi.ps1` produces `Enigma.Licensing.2.3.0.msi` with `ProductVersion` 2.3.0, upgrade code
  `{4BAD4F52-DD12-4B57-8D94-52FE1936F034}`, and the new `app.ico`.
- `RELEASENOTES.md` covers every `DONE` item of this run, and every path it quotes exists. `SECURITY.md`
  supports the app's 2.3.x and the library's 2.1.x.
- Nothing in the dev runs `git tag`, `git push`, `dotnet pack` or `dotnet nuget push`, installs the MSI, or
  runs `buildmsi` in this repository.

## Out of scope

- A library release (version, `<PackageReleaseNotes>`, README callout, pack-verify), since nothing in it
  changed.
- Publishing 2.1.0's package to nuget.org.
- Distributing the 2.3.0 MSI. The maintainer runs `buildmsi` after merging.
- Moving the Avalonia set on its own.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version number | App **2.3.0** | The user's rule: y+1, and z is already 0 | 2.2.1, 3.0.0 |
| The library | Stays **2.1.0**. No pack, no `<PackageReleaseNotes>` change | The run changes nothing in the library, and a NuGet version with no change is noise. FF4B precedent | Moving both to 2.3.0, an empty library release |
| "Update the version in the buildmsi script" | No literal edit: the script reads the app csproj's `<Version>`. The bump in step 2 is the update, and step 8 proves the script picks it up | A hardcoded version in the script would be a second source of truth that drifts from the executable's. FEATURE-6D42 chose the lookup deliberately, and GitClient, the family reference, does the same | `$Version = '2.3.0'` in the script; a `-Version` parameter (lets an MSI name a version the executable disagrees with) |
| Verifying the MSI | End to end in a scratch clone, then deleted | The draft names the installer, and only a built MSI proves its version and icon. `git clean -dxf` forbids running it here | Reading the script, or running only its version lookup |
| The README callout | Left on 2.1 | It is the package's nuget.org page and describes the package | "What's new in 2.3", which would describe an app change on the library's page |
| Release-notes heading | `Enigma.Licensing v2.3.0`, opening with "the library is unchanged at 2.1.0" | One newest-first file for the repository, as for 2.2.0 | A separate notes file for the app (a second chronology) |
| Committed MSI profile | None | The script generates the profile on every build from the version (FEATURE-6D42) | `msiProfiles/<App>.2.3.0.msipkg.json` |
| Dependencies | App- and test-only non-coupled bumps applied. The library's dependencies and the Avalonia set held | A library dependency bump would change an unreleased package, and the house rule holds coupled sets together | Bumping everything |
| Order in the run | Last | The notes describe the icon, which is built first | First |
| Breakdown | One phase | A routine release | Splitting the docs from the version |
