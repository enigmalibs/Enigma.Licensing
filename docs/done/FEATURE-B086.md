# FEATURE-B086 — Remove licence features & claims

**Status:** DONE
**Branch:** `feature/feature-b086-drop-features-claims`
**Run:** feature/2026-09-11-icon-trim-release

## Summary

Feature flags and claims are gone from the library, the desktop application, the tests and the guides.
They were signed, stored, displayed and documented, and nothing in the library ever decided anything
from them: `ILicenseValidator`'s check order settles authenticity, then the validity window, the
product identifier and the device identifier, and stops.

Removed from the public surface — five members, deleted outright with no `[Obsolete]` and no shim:

| Member | Was |
|---|---|
| `License.Features` | `IReadOnlyCollection<string>` |
| `License.Claims` | `IReadOnlyDictionary<string, string>` |
| `License.HasFeature(string)` | `bool` |
| `LicenseRequest.Features` | `IReadOnlyCollection<string>?` |
| `LicenseRequest.Claims` | `IReadOnlyDictionary<string, string>?` |

This is a **breaking change against a package that is published** — `Enigma.Licensing` 1.0.0 is on
nuget.org. `FEATURE-82E5` turns that into the 2.0.0 release and writes the migration guide.

**Neither document format changed.** `enigma-license/1` and `enigma-key/1` are what they were; a
payload simply stops carrying two optional members.

## Backward compatibility — the part that had to be proved, not assumed

Two claims decide whether this removal is safe, and both were established by reading the code and then
pinned by a new test each:

1. **A licence 1.0 signed still validates.** Its payload carries `"features"` and `"claims"`; the
   payload is deserialized through `PayloadJsonContext`, which carries no `UnmappedMemberHandling`, so
   System.Text.Json's default applies and an unmapped member is **skipped**. Nothing re-serializes a
   licence in order to check one — the design's founding invariant — so the signature still covers
   exactly the bytes it was made over. Pinned by
   `LicensePayloadTests.Deserialize_IgnoresTheFeaturesAndClaimsA10LicenseCarries`.
2. **A profile the app saved still loads.** `LicenseProfileStore` uses a plain `JsonSerializerOptions`
   with the same default, so a 1.0 profile's two extra fields are ignored. Pinned by
   `LicenseProfileStoreTests.AProfileFrom10_LoadsWithItsFeaturesAndClaimsIgnored`.

The standing consequence, recorded in `CLAUDE.md`: **never set `JsonUnmappedMemberHandling.Disallow` on
these documents.** One line would invalidate every licence 1.0 issued.

## Files/modules touched

**Library — modified**

- `License.cs` — the two properties, `HasFeature`, both backing fields and `EmptyClaims` deleted; two
  `using`s pruned.
- `LicenseRequest.cs` — the two properties deleted; one `using` pruned.
- `LicenseGenerator.cs` — the two assignments in the license it builds.
- `ILicenseStore.cs` — the `<see cref>` naming both removed members, which would have been a build
  error (`GenerateDocumentationFile` plus `TreatWarningsAsErrors`), now names `License.Owner`.

**Desktop — deleted**

- `ViewModels/LicenseClaimEntry.cs` — its only two users went with the claims.

**Desktop — modified**

- `ViewModels/GenerateLicenseViewModel.cs` — four commands, three `NewX` properties, the two
  `ObservableCollection`s, `HasClaim`, `ClaimsSnapshot`, and the two fields in `BuildRequest`,
  `CaptureProfile` and `ApplyProfile`. Two `using`s pruned. 735 → 566 lines.
- `ViewModels/LicenseSummary.cs` — `Features`, `Claims`, `HasFeatures`, `HasClaims` and `BuildClaims`;
  the remarks' three reasons for the type existing are now the two that survive.
- `Models/LicenseProfile.cs` — the two properties, their backing fields and `EmptyClaims`; a remark
  added recording why `CurrentFormatVersion` did **not** move.
- `Views/GenerateLicenseView.axaml` — the *Features* and *Claims* `SettingsCardExpander`s. The page now
  reads Profile → Licence → Validity → Signing → Output file.
- `Views/ValidateLicenseView.axaml` — the two conditional blocks on the licence card.
- Both views — `xmlns:sys` removed, having lost its only user with the feature list's `DataTemplate`.

**Tests — modified**

- Library: `LicenseTestData.cs`, `Model/LicenseTests.cs`, `Model/LicenseRequestTests.cs`,
  `Internal/LicensePayloadTests.cs`, `Api/LicenseEnvelopeTests.cs`, `Api/LicenseGeneratorTests.cs`,
  `Api/LicenseStoreTests.cs`, `DependencyInjection/EnigmaLicensingServiceCollectionExtensionsTests.cs`.
- Desktop: `LicenseSamples.cs`, `Models/LicenseProfileStoreTests.cs`,
  `ViewModels/GenerateLicenseViewModelTests.cs`, `ViewModels/LicenseSummaryTests.cs`,
  `ViewModels/GenerateLicenseRoundTripTests.cs`, `ViewModels/ValidateLicenseRoundTripTests.cs`.

**Documentation sweep**

- `README.md` — the *Generating licenses* bullet no longer lists feature flags and claims.
- `docs/guides/generating-licenses.md` — two rows out of the `LicenseRequest` table, and the whole
  *Features and claims* section with its snippet.
- `docs/guides/validating-licenses.md` — *Gating features* became *Reading what a valid license says*,
  with a snippet over `Owner` and `ValidUntil`; the store's "wherever a feature needs gating" reworded.
- `docs/guides/license-format.md` — the two fields out of the payload example, and a **new paragraph**
  stating that an unknown payload member is ignored rather than rejected, which is what keeps a 1.0
  licence valid. The `ReadLicense()` warning's closing line no longer ends on gating a feature.
- `docs/guides/README.md` — the index entry.
- `CLAUDE.md` — a new *Shipped* section, a line in *Current state*, and the four historical *Shipped*
  passages that had become factually wrong (the PHASE01 type list, the generate page's description, the
  claims-are-immutable bullet, `LicenseSummary`'s dictionary-flattening clause, and two project-layout
  rows).
- `RELEASENOTES.md` — **deliberately untouched**, per the plan: it is a released document, and
  `FEATURE-82E5` prepends the 2.0.0 section so the break is described once.

## Decisions taken at build time

1. **Tests over removed members were deleted; tests whose *concern* survived were retargeted rather
   than dropped.** Three cases earned retargeting instead of deletion, because the thing they guard is
   still real:
   - `LicensePayloadTests.RoundTrip_SurvivesAwkwardClaimAndFeatureText` → `RoundTrip_SurvivesAwkwardText`.
     The escaping guarantee is about opaque strings in the payload, and `ProductId`, `Owner` and
     `DeviceId` are still opaque strings.
   - `LicensePayloadTests.Deserialize_TreatsNullCollectionsAsEmpty` → the new forward-compatibility
     case. The old test was about the never-null guarantee that went with the collections; the file is
     the right home for what replaced it.
   - `GenerateLicenseViewModelTests.Generate_SignsTheFormAsItStoodWhenTheButtonWasPressed` used
     `Features.Add` as its second "edited during signing" field; it now edits `Owner`. Deleting the
     second field would have weakened a test about snapshotting, not simplified it.
2. **`LoadingAHandEditedProfile_DropsWhatCannotBeUsed` was deleted rather than retargeted.** Every
   filter it asserted — blank feature dropped, duplicate key dropped — lived in `ApplyProfile`'s two
   loops, and those loops went with the lists. The remaining fields are assigned straight through, so
   there is nothing left to filter and the test would have asserted a tautology.
3. **`LoadingAProfile_ReplacesWhateverWasThere` was strengthened, not weakened.** It proved "replaces"
   through the two lists; it now proves it through `Owner`, `DeviceId`, both dates and
   `PrivateKeyPath` — a wider assertion than the one it replaced.
4. **The round-trip suites kept their field count by substituting `DeviceId`.** Both had used a feature
   and a claim as the "and it really carried what was typed" evidence; dropping them would have thinned
   an end-to-end test to product and owner alone. `DeviceId` is the natural stand-in — a real optional
   field, exercised through the same path, and left unasserted at validation time so the device check
   still skips.
5. **`LicenseProfile` keeps `formatVersion: 1`**, as planned, and the reason now lives in the type's own
   remarks rather than only in the plan — that is where the next person to consider bumping it will look.
6. **A paragraph was added to the format guide** rather than only to `CLAUDE.md`. "Unknown payload
   members are ignored" is a property of `enigma-license/1` that a consumer can rely on, so it belongs
   in the format's own documentation, not just in the agent guide.

## Deviations & follow-ups

- **No deviation from the plan.** All seven acceptance criteria met as written, including that
  `RELEASENOTES.md` is unchanged by this dev.
- **Follow-up owned by `FEATURE-82E5`:** the version bump, the release notes, `PackageReleaseNotes`, the
  migration guide, and `CLAUDE.md`'s *Current state* block rewritten for the release as a whole.
- **Not done, deliberately:** no replacement entitlement mechanism. If per-feature gating is ever wanted
  it is a new work item with its own design, not a rename of what was removed — recorded in `CLAUDE.md`
  so it cannot creep back in unplanned.
- **Line endings:** no CRLF/LF churn appeared in this dev's diff, and nothing was done about line
  endings either way, per the workflow's recommendation-only rule.

## Build/test evidence

- `DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release` — **succeeded, 0 warnings,
  0 errors**, across all three library target frameworks and both applications.
- `dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal | grep -c AVLN`
  → **0**. Mandatory here rather than precautionary: two `.axaml` files changed, and XAML warnings
  bypass `TreatWarningsAsErrors`.
- `DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release` — **712 passed,
  0 failed** on the net10.0 legs (426 library + 286 desktop). The `error: 1` and non-zero exit are this
  machine's missing .NET 8 runtime, the condition `CLAUDE.md` documents.
- The net8.0 leg, run the documented way (`DOTNET_ROLL_FORWARD=LatestMajor` from `bin/Release/net8.0`) —
  **Total: 426, Errors: 0, Failed: 0, Skipped: 0**.

**Case counts, before → after:**

| Suite | Before | After |
|---|---|---|
| Library, net8.0 | 438 | 426 |
| Library, net10.0 | 438 | 426 |
| Desktop, net10.0 | 298 | 286 |
| **Total** | **1174** | **1138** |

Thirty-eight cases were deleted with the API they exercised and two were added — the forward-compatibility
pair. Three more were retargeted rather than deleted (decision 1), so they appear in both columns.
