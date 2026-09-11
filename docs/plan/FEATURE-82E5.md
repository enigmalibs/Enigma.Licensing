# FEATURE-82E5 — Release v2.0.0

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-82e5-release-v2`
**Run:** feature/2026-09-11-icon-trim-release

## Objective

Cut the library's next release. `FEATURE-B086` removes five public members from a package that is
published, which under Semantic Versioning is a major release: **`Enigma.Licensing` 1.0.0 → 2.0.0**. Make
every in-repo edit a release needs, verify the artifact locally, and leave the tag, the push and the
NuGet publish to the maintainer.

## Context & constraints

**The version is decided by a fact, not a preference.** `Enigma.Licensing` **1.0.0 is published on
nuget.org** — `https://api.nuget.org/v3-flatcontainer/enigma.licensing/index.json` answers
`{"versions":["1.0.0"]}`. A consumer therefore exists who can be broken, and `License.Features`,
`License.Claims`, `License.HasFeature`, `LicenseRequest.Features` and `LicenseRequest.Claims` are gone.
Under SemVer §8 that is a major bump, and nothing smaller is defensible: a minor would tell a consumer
the upgrade is safe when it does not compile.

**`CLAUDE.md` is stale on exactly this point.** It states that "no tag exists, nothing is pushed, and
`github.com/enigmalibs/Enigma.Licensing` is still to be created". The remote exists and is configured as
`origin`, and the package is on nuget.org. No tag exists locally or on the remote — that part still
holds, and the repository's tag convention is therefore still the bare `X.Y.Z` that `docs/RELEASE.md`
declares.

This is a **routine release** in `dotnet-release`'s terms, not a first release: the twelve packaging
properties, the packing `ItemGroup`, the licence audit and `docs/RELEASE.md` all landed with
`FEATURE-57BC` and are not redone. What a routine release owes is the version, the notes, the README
callout, a dependency refresh and the runbook.

## Decisions

1. **The library goes to 2.0.0.** See *Context*.
2. **The desktop application goes to 2.0.0 as well.** It versions independently — there is no
   solution-wide `<Version>` — but the same change drives both, and the app's own release is a major one
   on its own terms: it loses two user-facing sections of the generate page, its licence card drops two
   rows, its saved-profile shape loses two fields, and its icon changes. Landing them on the same number
   is a consequence of one change breaking both, not a coupling of the two versions. `app.manifest`'s
   four-part `assemblyIdentity` moves with it, as its own comment requires.
3. **A migration guide, at `docs/migrations/1.0.0-to-2.0.0.md`.** This is the house pattern for a
   breaking release — `Enigma.Core` ships `docs/migrations/1.1.0-to-2.0.0.md` in exactly this shape: an
   agent-consumable "prompt part" stating what breaks, what does *not*, and the mechanical mapping.
   `PackageReleaseNotes` points at it, which `dotnet-release` explicitly allows for a breaking release.
4. **The migration guide leads with what does not break**, because that is the expensive half of a
   consumer's uncertainty: every already-issued licence still validates, every key file still loads, both
   document formats are byte-identical, and the check order and error contract are untouched. The break is
   a compile error at five member names and has no runtime component at all.
5. **`RELEASENOTES.md` gets a prepended 2.0.0 section** in the skill's sub-section order — *Breaking
   Changes & Migration · New Features · Fixes · Dependencies · Compatibility · Version* — with the 1.0.0
   section left exactly as it is.
6. **`SECURITY.md`'s supported-versions table moves to `2.0.x`.** The policy sentence above it already
   says fixes are provided for the latest released version; leaving `1.0.x` as the only supported row
   after shipping 2.0.0 would contradict it.
7. **The dependency refresh is run, and held-back sets are recorded.** `dotnet list package --outdated`
   decides it. The **version-coupled desktop group** (`Avalonia.*`, `AvaloniaUI.DiagnosticsSupport`,
   `CommunityToolkit.Mvvm`, `Enigma.Avalonia.Desktop`, `Enigma.Icons*`) is bumped as a unit or not at all,
   per this repository's own standing rule; a coupled bump is **not** taken on a release dev unless every
   member moves cleanly, because a broken control library is a worse outcome than a release on current
   pins. Whatever is held back is named in the notes with the reason.
8. **Pack-verify runs; nothing outward-facing does.** Packing into the session scratchpad and inspecting
   the artifact is `dotnet-release`'s one sanctioned local exception, and `docs/RELEASE.md` names it as
   one of the two an agent may run. `git tag`, `git push`, the publish `dotnet pack -o ./artifacts` and
   `dotnet nuget push` are **printed only**.
9. **The publish scripts are run, because the runbook allows it and the app's version moved.** They read
   `<Version>` back with `dotnet msbuild -getProperty:Version`, so running them is the check that the bump
   reached the artifact names. `artifacts/` is gitignored, so nothing from them is committed.
10. **The snippet-verification gate runs**, because `FEATURE-B086` edited the guides and the README quick
    start. Every fence is checked against the real public surface and the coverage table goes in the
    completion record.
11. **`docs/RELEASE.md` is not versioned and does not change.** It is written throughout in `X.Y.Z` terms.
12. **No `CHANGELOG.md`, no symbol package, no badge edit.** House rules: one release-notes source; a
    release ships exactly one `.nupkg`; the NuGet badge tracks the published version by itself.

## Steps

1. `<Version>` → `2.0.0` in `src/Enigma.Licensing/Enigma.Licensing.csproj`; `<Version>` → `2.0.0` in
   `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` and `assemblyIdentity version="2.0.0.0"`
   in `src/Enigma.Licensing.Desktop/app.manifest`, with the csproj comments kept truthful.
2. Write `docs/migrations/1.0.0-to-2.0.0.md` in the `Enigma.Core` house shape.
3. Prepend the 2.0.0 section to `RELEASENOTES.md`.
4. Rewrite `<PackageReleaseNotes>` in the library csproj to mirror that section's top, ending by pointing
   at the migration guide.
5. Update the README's what's-new callout to 2.0, and check the *Features*, *Quick start* and
   *Documentation* sections still describe the library as it now is.
6. `SECURITY.md` → supported version `2.0.x`.
7. `dotnet list package --outdated`; apply what is safe, hold the coupled set together, and record every
   `old → new` and every deliberate hold in the notes' *Dependencies* sub-section.
8. Run the snippet-verification gate over `docs/guides/*.md` and the README quick start; build the
   coverage table.
9. Release pre-flight: `dotnet build -c Release` (zero warnings), the whole suite green, and an `AVLN`
   count of 0 for the desktop project.
10. Pack-verify into the session scratchpad: version `2.0.0`, the `.nupkg` and nothing else, a non-empty
    embedded `README.md` and `LICENSE.md`, the nuspec's `version`/`title`/`license type="file"`/`readme`/
    `releaseNotes`, an XML doc file per target framework, and the dependency floors per framework. Delete
    the directory afterwards.
11. Run `scripts/publish.sh` and confirm the zips are named `…-2.0.0-<rid>.zip`.
12. Documentation sweep: `CLAUDE.md` — the release state, the stale "nothing is pushed" paragraph, the
    version numbers and the new `docs/migrations/` entry in the project layout.
13. Print the runbook for the maintainer. Run none of it.

## Acceptance criteria

1. The library csproj reads `<Version>2.0.0</Version>`; the desktop csproj and `app.manifest` read the
   same version in their own forms.
2. `RELEASENOTES.md` opens with a 2.0.0 section naming every removed member and pointing at the migration
   guide, with the 1.0.0 section intact beneath it.
3. `docs/migrations/1.0.0-to-2.0.0.md` exists and states both halves: the five removed members, and the
   explicit list of what did **not** change — documents, formats, algorithms, check order, error contract.
4. `<PackageReleaseNotes>` mirrors the top of `RELEASENOTES.md` and does not contradict it.
5. The README's callout reads 2.0; `SECURITY.md` supports `2.0.x`.
6. Every dependency transition and every deliberate hold-back is in the notes.
7. The snippet-verification gate reports **zero mismatches**, with the per-file coverage table in the
   completion record.
8. Pack-verify passed every assertion in step 10, against a real `.nupkg`, and its directory is gone.
9. `dotnet build -c Release` is warning-free, the suite is green, and `AVLN` is 0.
10. No tag was created, nothing was pushed, and `artifacts/` is untracked.

## Out of scope

- Creating the tag, merging to `main`, pushing, and `dotnet nuget push` — the maintainer's, per
  `docs/RELEASE.md`.
- An MSI profile: `dotnet-release` offers those for apps, and this repository distributes the application
  as a zip built by `scripts/publish.*`. Adding an installer is a work item, not a release step.
- Re-running the first-release third-party licence audit — no runtime dependency is added, and
  `FEATURE-57BC-PHASE01`'s audit stands unless the refresh in step 7 moves one.
- Target-framework normalization: the library is already on `netstandard2.0;net8.0;net10.0`, which is the
  normalized set.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which version? | 2.0.0 | 1.0.0 is on nuget.org and five public members are gone — SemVer says major | 1.1.0 (tells a consumer a breaking upgrade is safe); 1.0.1 (worse still); staying at 1.0.0 (the published version already exists) |
| Does the desktop app bump too? | Yes, to 2.0.0 | It loses user-facing capability and changes its profile shape; the same change drives both, so they land on the same number | 1.1.0 (a removal is not a minor); leave at 1.0.0 (the shipped app would claim to be the previous one) |
| Migration guide, or notes only? | A guide, in the Enigma.Core shape | House pattern for a breaking release, and `PackageReleaseNotes` may point at one | Notes only (no room for the "what did not break" half that costs a consumer the most time) |
| Bump the coupled Avalonia/Enigma desktop set? | Only if every member moves cleanly; otherwise hold and record | The repo's rule is to bump that group as a unit; a release dev is the wrong place to absorb a control-library regression | Bump individually (explicitly forbidden here); bump the set unconditionally |
| `SECURITY.md` supported versions | `2.0.x` | The policy text says the latest released version is supported | Leave `1.0.x` (contradicts the policy above it); list both (the stated policy is latest-only) |
| Run the publish scripts? | Yes | The runbook names them a permitted local action, and they read the version back — which is the check that the bump landed | Skip them (the app version bump would go unverified) |
| Tag and push? | No | `docs/RELEASE.md` is the maintainer's, and pushing is forbidden to a run | Tag locally "ready for the maintainer" (a tag is a release artifact) |
