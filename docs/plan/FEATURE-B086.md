# FEATURE-B086 — Remove licence features & claims

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-b086-drop-features-claims`
**Run:** feature/2026-09-11-icon-trim-release

## Objective

Delete feature flags and claims from the library and from the desktop application. Validation has never
read either — `ILicenseValidator`'s documented check order goes format → algorithm → signature → payload
→ window → product → device and stops there — so the two fields are carried, signed, displayed and
documented without anything ever deciding on them.

## Context & constraints

What goes, on the library's public surface:

| Member | Type |
|---|---|
| `License.Features` | `IReadOnlyCollection<string>` |
| `License.Claims` | `IReadOnlyDictionary<string, string>` |
| `License.HasFeature(string)` | `bool` |
| `LicenseRequest.Features` | `IReadOnlyCollection<string>?` |
| `LicenseRequest.Claims` | `IReadOnlyDictionary<string, string>?` |

**This is a breaking change to a package that is published.** `Enigma.Licensing` 1.0.0 is on nuget.org,
so removed public members are a major-version event; `FEATURE-82E5` is what turns that into a release.
`CLAUDE.md`'s claim that nothing has ever been pushed is stale and is corrected there, not here.

**Two compatibility facts decide the shape of this dev, and both were established by reading the code
rather than assumed:**

1. **Licences signed by 1.0.0 keep validating.** Their payload carries `"features": [...]` and
   `"claims": {...}`, and the payload is deserialized through `PayloadJsonContext` with System.Text.Json's
   **default** unmapped-member handling — unknown members are skipped, not rejected. Nothing re-serializes
   a licence in order to check one, so the signature still covers the same bytes it always did. The two
   removed fields simply stop being visible.
2. **Profiles saved by the app keep loading.** `LicenseProfileStore` deserializes with a plain
   `JsonSerializerOptions` carrying no `UnmappedMemberHandling`, so a 1.0.0 profile's `features` and
   `claims` are ignored the same way. `LicenseProfile.CurrentFormatVersion` therefore **stays 1**: the
   loader refuses only a *higher* version, and bumping it would make this build reject files it reads
   perfectly.

Both are load-bearing and neither is currently pinned by a test, so this dev adds one for each.

Reach, counted: library 4 files, desktop application 6 files, tests 15 files, prose docs 5 files.

## Decisions

1. **One dev, not two phases.** The obvious split — library first, application second — cannot be built:
   `GenerateLicenseViewModel` assigns `request.Features` and `request.Claims`, so a library-only phase
   leaves the solution red and fails the Definition of Done on its own. A removal is atomic by nature; the
   diff is large but is almost entirely deletion.
2. **Deleted outright — no `[Obsolete]`, no shim, no reader.** This repository's standing rule for the
   predecessor's formats ("no compatibility with, reader for, or converter from") is the same instinct: a
   deprecated member that nothing reads is a member that has to be explained forever. The major version is
   what carries the break, and the migration guide (`FEATURE-82E5`) is what explains it.
3. **`LicenseClaimEntry` is deleted, not repurposed.** It exists only to give the claims list a row type.
   Both its users — the generate page's editable list and `LicenseSummary`'s read-only projection — go with
   it.
4. **`LicenseSummary` keeps its shape minus two properties.** `Features`, `Claims`, `HasFeatures` and
   `HasClaims` go; every other projection (the instants, the words for absent optionals) is untouched. The
   type's remarks currently justify its existence partly by the dictionary-templating problem, which
   disappears with the claims — the remaining reasons (formatted instants, words for absent fields) still
   hold and the prose is trimmed to them rather than rewritten.
5. **The profile format keeps `formatVersion: 1`** — see *Context* above. The `Features` and `Claims`
   properties are removed from `LicenseProfile`; a file that still carries them loads with them ignored.
6. **Two new tests pin the forward-compatibility**, because it is the one behaviour a reader would
   reasonably doubt: a hand-written `enigma-license/1` payload carrying `features`/`claims` must still
   validate end to end, and a hand-written profile carrying them must still load. Both are cheap — the
   licence one uses the existing `SignedLicenseFixture` rather than generating a key.
7. **Tests for removed members are deleted, never adapted.** A test that no longer has an API to call is
   not evidence of anything. The suites lose cases; the completion record states the before/after counts.
8. **The two pages lose whole sections, not disabled controls.** The generate page drops its *Features*
   and *Claims* expanders entirely (the page then reads Profile → Licence → Validity → Signing → Output),
   and the validate page's licence card drops the two conditional blocks. `xmlns:sys` stops being used on
   the generate page once the feature list's `DataTemplate` goes, and is removed with it.

## Steps

1. **Library.** Remove `Features`, `Claims`, `HasFeature` and the two backing fields from `License.cs`,
   and `Features`/`Claims` from `LicenseRequest.cs`; drop the two assignments in `LicenseGenerator.cs`;
   fix the `ILicenseStore` XML doc that names both members in a `<see cref>` (a dangling cref is a build
   error here). Prune the now-unused `using`s in each file.
2. **Desktop — ViewModels.** Strip the feature and claim commands, the `NewFeature`/`NewClaimKey`/
   `NewClaimValue` properties, the two `ObservableCollection`s and their helpers from
   `GenerateLicenseViewModel`; remove the two properties from `BuildRequest`, `CaptureProfile` and
   `ApplyProfile`. Delete `LicenseClaimEntry.cs`. Remove the four members from `LicenseSummary` and trim
   its remarks.
3. **Desktop — model.** Remove `Features` and `Claims` (and their backing fields and `EmptyClaims`) from
   `LicenseProfile`; add a remark recording that a 1.0.0 profile carrying them still loads, with its
   fields ignored, which is why `CurrentFormatVersion` does not move.
4. **Desktop — views.** Delete the two `SettingsCardExpander`s from `GenerateLicenseView.axaml` and the
   two conditional `StackPanel`s from `ValidateLicenseView.axaml`; remove any namespace declaration left
   with no user.
5. **Tests.** Delete every case over a removed member across both suites; adjust the shared fixtures
   (`LicenseTestData`, `LicenseSamples`) and any assertion that counted properties or round-tripped a
   form including the two lists.
6. **Tests — new.** Add the two forward-compatibility cases of decision 6.
7. Build the solution in Release (zero warnings) and check the desktop project's `AVLN` count is 0 — the
   `.axaml` edits make that check mandatory rather than cheap insurance.
8. Run the whole suite; record the before/after case counts.
9. Documentation sweep: `README.md`, `docs/guides/generating-licenses.md`,
   `docs/guides/validating-licenses.md`, `docs/guides/license-format.md`, `docs/guides/README.md` and
   `CLAUDE.md` all describe features and claims as shipped behaviour. `RELEASENOTES.md` is **not** touched
   here — it is a released document, and `FEATURE-82E5` prepends the new section.

## Acceptance criteria

1. No `Features`, `Claims`, `HasFeature` or `LicenseClaimEntry` remains anywhere under `src/` or `tests/`
   (the word "claims" surviving in prose that means *asserts* is fine and expected).
2. `LicenseProfile.CurrentFormatVersion` is still `1`.
3. A licence whose signed payload carries `features` and `claims` still reaches `LicenseFailure.None`
   through the real validator, and a profile file carrying both still loads — each pinned by a test.
4. `dotnet build Enigma.Licensing.slnx -c Release` succeeds with zero warnings; the desktop project's
   `AVLN` count is 0.
5. `dotnet test --solution Enigma.Licensing.slnx -c Release` is green, and the completion record states
   the case count before and after.
6. The guides and the README no longer document either concept, and no code fence references a removed
   member.
7. `RELEASENOTES.md` is unchanged by this dev.

## Out of scope

- The version bump, the release notes, the migration guide and `PackageReleaseNotes` — all `FEATURE-82E5`.
- Every other field of `License` and `LicenseRequest`, the validity window, the product wildcard and the
  device binding.
- The envelope and key formats: `enigma-license/1` and `enigma-key/1` are **unchanged** — a payload
  simply stops carrying two optional members.
- Any replacement mechanism for per-feature entitlement. If one is ever wanted it is a new work item with
  its own design, not a rename of what is being removed here.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Remove, or keep and document as unvalidated? | Remove | The draft asks for it, and a signed field nothing decides on is a field every reader has to be told to ignore | Keep with a doc note (the confusion that prompted this); keep only `Claims` (the same objection applies to both) |
| Deprecate first, remove later? | Remove outright | The consumer is the repo's own app; a major version is the honest signal, and this repo already refuses shims for the predecessor's formats | `[Obsolete]` for one release (two releases to explain instead of one) |
| One dev or library/app phases? | One | A library-only phase does not compile, and the Definition of Done needs a green build per dev | Two phases (guaranteed red first phase) |
| Bump the profile `formatVersion`? | No, stays 1 | A 1.0.0 profile still loads with the two fields ignored; the loader refuses only *higher* versions | Bump to 2 (would make this build reject files it reads perfectly) |
| Old licences carrying the fields? | Still valid, and pinned by a test | Unknown members are skipped by default and nothing re-serializes a licence to verify it | Reject them (would break every licence ever issued); leave it untested (the one thing a reader would doubt) |
| What happens to `LicenseClaimEntry`? | Deleted | Both users go with the claims | Keep as a generic key/value row for later use (a type with no caller) |
| Touch `RELEASENOTES.md` here? | No | It is a released document; the release dev prepends the new section so the break is described once | Edit the 1.0.0 section (rewriting history of a shipped release) |
