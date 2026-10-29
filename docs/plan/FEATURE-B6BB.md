# FEATURE-B6BB — Release 2.1.0

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-b6bb-release-2-1-0`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Prepare **Enigma.Licensing 2.1.0** and **Enigma.Licensing.Desktop 2.1.0**, carrying this run's items, the
way FEATURE-82E5 prepared 2.0.0: every in-repo edit a release needs, the artifacts verified locally, and
the tag, the push and the NuGet publish left to the maintainer (`docs/RELEASE.md`).

## Context & constraints

- **Version rule (the user's):** `x.y.z → x.(y+1).0`. Both projects declare `<Version>2.0.0</Version>`
  and 2.0.0 is published (nuget.org answers `["1.0.0","2.0.0"]`; tag `2.0.0` exists) → **2.1.0** for both.
  - The library changed in this run (FEATURE-A0EB drops `net8.0`; FEATURE-9083 raises the DI abstractions
    floor to 10.0.14 and System.Text.Json to 10.0.14) — no public API moved, so a minor is right under
    SemVer too: a net8.0 consumer falls back to `netstandard2.0`.
  - The app changed in every other item.
- **The spec's "Remove splash screen" produced no item**: this app has no splash screen. The release notes
  say nothing about it.
- **2.1.0 carries** this run's items `DONE` on the run branch when this dev starts; a quarantined item is
  left out of the notes.
- Release surface (FEATURE-57BC / 82E5): `<Version>` ×2, `app.manifest`'s four-part version,
  `RELEASENOTES.md` (newest first), `<PackageReleaseNotes>` (rewritten whole, mirrors the top section),
  the README 2.x callout and NuGet badge text, `SECURITY.md` supported line, `docs/RELEASE.md` (dependency
  table, §7 zip contents — `app.ico` is now a fifth entry), `CLAUDE.md` current state.
- **`CLAUDE.md` is stale** about 2.0.0: it says no tag exists and nothing was published; both are false
  now. Corrected here.
- **Release gates** (CLAUDE.md): the snippet gate — every C# fence in the README and the five guides
  compiled against the real 2.1.0 surface in a scratchpad project; the pack-verify against a real `.nupkg`
  in the scratchpad; the licence audit re-read for the changed graph (DI abstractions 10.x on
  netstandard2.0 pulls `Microsoft.Bcl.AsyncInterfaces` / `System.Threading.Tasks.Extensions` at new versions).
- `scripts/publish.ps1` runs under Windows PowerShell 5.1 here; `scripts/publish.sh` needs Info-ZIP `zip`.
- Never tag, push, publish, or merge outside the run branch.

## Steps

1. `git tag --list 2.1.0` is empty; nuget.org has no 2.1.0.
2. `<Version>2.1.0</Version>` in both csproj (and their comments); `app.manifest` → `2.1.0.0`.
3. `RELEASENOTES.md`: a `2.1.0` section on top in 2.0.0's shape — summary, library changes (targets,
   dependency floors), application changes (one entry per item), *Upgrading from 2.0* (nothing to do for
   consumers; net8.0 consumers now get the netstandard2.0 asset); the current-version table if present.
4. `<PackageReleaseNotes>` rewritten from that section.
5. README callout and badges → 2.1.0; `SECURITY.md` → 2.1.x supported.
6. `docs/RELEASE.md`: the dependency-floor table (two TFMs, new floors), §7's zip contents.
7. `CLAUDE.md`: a 2.1.0 paragraph in *Current state*, the 2.0.0 facts corrected (tag and NuGet).
8. Gates: clean Release build warning-free with 0 `AVLN`; the whole suite green; the snippet gate;
   `dotnet pack` into the scratchpad and the pack-verify (version, single `.nupkg`, README/LICENSE,
   nuspec fields, XML doc per TFM, dependency groups netstandard2.0 + net10.0 only, PolySharp and ILLink
   absent); `scripts/publish.ps1` builds both zips and their entries are listed; the built app reports 2.1.0.
9. Licence audit: the changed packages' licences read from their nuspecs; the table in the completion doc.

## Acceptance criteria

- Both assemblies carry 2.1.0; `app.manifest` 2.1.0.0.
- `RELEASENOTES.md` covers every `DONE` item of this run; README, SECURITY, RELEASE and CLAUDE.md name 2.1.0.
- Release build warning-free; the whole suite green; snippet gate zero mismatches; pack-verify passes.
- Nothing in the dev tags, pushes, publishes or merges outside the run branch.

## Out of scope

- Tagging, merging to `main`, `dotnet nuget push`, attaching zips to a GitHub release.
- A migration guide (no breaking change).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 2.1.0 for both | The user's rule; no public API break, so SemVer agrees | 2.0.1; 3.0.0 |
| Library too | Yes | Its targets and dependency floors changed — a consumer-visible package change | App-only release |
| Release-ready, not released | Prepare and verify; maintainer runs RELEASE.md | The repo's permanent boundary and the run's no-push rule | Tagging locally |
| Breakdown | One dev | A routine release, as 2.0.0 | Splitting docs from version |
