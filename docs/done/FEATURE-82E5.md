# FEATURE-82E5 — Release v2.0.0

**Status:** DONE
**Branch:** `feature/feature-82e5-release-v2`
**Run:** feature/2026-09-11-icon-trim-release

## Summary

`Enigma.Licensing` and `Enigma.Licensing.Desktop` both read **2.0.0**. Every in-repo edit a release
needs is made, the artifact was packed and inspected locally, and both application zips were built.
**Nothing outward-facing happened**: no tag, no push, no `dotnet nuget push`.

**The version was decided by a fact, not a preference.** `Enigma.Licensing` **1.0.0 is published on
nuget.org** — `https://api.nuget.org/v3-flatcontainer/enigma.licensing/index.json` answers
`{"versions":["1.0.0"]}`. A consumer therefore exists who can be broken, and `FEATURE-B086` removed five
public members. Under SemVer that is a major release, and nothing smaller is defensible: a minor would
tell a consumer the upgrade is safe when it does not compile.

This also **corrected a stale statement in `CLAUDE.md`**, which said no tag existed, nothing was pushed
and `github.com/enigmalibs/Enigma.Licensing` was still to be created. The remote exists and is `origin`,
and the package is live. The *tag* half is still true — there is no tag here or on the remote — so the
repository's convention remains the bare `X.Y.Z` that `docs/RELEASE.md` declares.

## Files/modified

**Versions**

- `src/Enigma.Licensing/Enigma.Licensing.csproj` — `<Version>` 1.0.0 → 2.0.0; `<Description>` no longer
  advertises feature flags and claims (it is the nuget.org description, so this was a correctness fix,
  not a polish one); `<PackageReleaseNotes>` rewritten whole to mirror the new top of `RELEASENOTES.md`,
  ending at the migration guide; the metadata comment updated.
- `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` — `<Version>` 1.0.0 → 2.0.0, with the
  comment rewritten to say *why* it lands on the library's number.
- `src/Enigma.Licensing.Desktop/app.manifest` — `assemblyIdentity version="2.0.0.0"`, and its comment
  now states that the two are kept in step by hand because nothing derives one from the other.

**Release documents**

- `RELEASENOTES.md` — a prepended 2.0.0 section in the skill's sub-section order (*Breaking Changes &
  Migration · Fixes · Dependencies · Compatibility · Version*). The 1.0.0 section is untouched, including
  its description of feature flags and claims: it is a record of what shipped then.
- `docs/migrations/1.0.0-to-2.0.0.md` — **new**, in `Enigma.Core`'s house shape.
- `README.md` — the what's-new callout now reads 2.0, and the *Documentation* section points at the
  migration guide **in prose** (the README is packed, so a relative `docs/…` link would be dead on
  nuget.org).
- `SECURITY.md` — supported versions: `2.0.x` ✅, `1.0.x` ❌.

**Dependencies**

- `Directory.Packages.props` — `System.Text.Json` 10.0.10 → 10.0.12, `Microsoft.Extensions.Hosting` /
  `.Logging.Console` / `.Logging.Debug` 10.0.10 → 10.0.12, and the System.Text.Json comment now records
  which release took the patch.

**Documentation sweep**

- `CLAUDE.md` — the stale "nothing is pushed" paragraph corrected; a *Current state* paragraph for the
  release; a new *Shipped* section; the `System.Text.Json` and `Microsoft.Extensions.*` pins; the DI
  abstractions' 9.x floor reaffirmed with this release named; the packaging-conventions bullet's "both
  read 1.0.0"; the `docs/migrations/` row in the project layout; the dev-workflow paragraph's item list;
  and `FEATURE-57BC-PHASE04`'s note about `pwsh` (see *Deviations* below).

## The dependency refresh — one bump taken, three holds

`dotnet list package --outdated` was the input. Every decision is recorded in `RELEASENOTES.md` too.

| Package | Requested | Latest | Decision |
|---|---|---|---|
| `System.Text.Json` (netstandard2.0 only) | 10.0.10 | 10.0.12 | **Taken.** Servicing patch; moves nothing else in the graph. |
| `Microsoft.Extensions.Hosting` / `.Logging.Console` / `.Logging.Debug` (app only) | 10.0.10 | 10.0.12 | **Taken.** Same. Not packed — the app is distributed as a zip. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 9.0.18 | 10.0.12 | **Held.** The 9.x floor is a documented repository decision: NuGet resolves to the highest floor in the graph, so raising it drags a consumer's whole `Microsoft.Extensions.*` set forward. This library uses `IServiceCollection` and `TryAddSingleton` and nothing else. |
| `Microsoft.Extensions.DependencyInjection` (tests only) | 9.0.18 | 10.0.12 | **Held**, to stay in step with the abstractions above — the point of the test reference is to build a real provider over the floor the library declares. |
| `Avalonia` / `.Desktop` / `.Themes.Fluent` / `.Fonts.Inter` | 12.1.1 | 12.1.2 | **Held as a set.** House rule: bump the version-coupled desktop group as a unit or not at all. `Enigma.Avalonia.Desktop` 1.0.0 pins 12.1.1 itself and did not move, and a release dev is the wrong place to absorb a control-library regression. |
| `xunit.v3` (test-only) | 3.2.2 | 4.0.0 | **Held.** A *major* test-framework bump, never shipped, with zero release value and a real chance of a red suite. |

No dependency was added or removed, so `FEATURE-57BC-PHASE01`'s third-party licence audit stands as
written and was not re-run — the correct scope for a routine release.

## The snippet-verification gate

Required this release because `FEATURE-B086` edited the guides and the README. There is no compile
harness for doc snippets in this repository, so this pass is the only thing that catches drift.

Method: every ```` ```csharp ```` fence was extracted mechanically from the six documents. The 35
executable ones were wrapped (namespace directives hoisted, body wrapped in an `async Task`) into a
scratch project referencing the **real** `Enigma.Licensing` 2.0.0 project plus the concrete DI container,
and compiled. The 5 signature listings — which are declarations, not code — were diffed against their
actual declarations in `src/`.

| File | Snippets | Distinct library symbols | Symbol references | Mismatches | Uncertain |
|---|---|---|---|---|---|
| `README.md` | 1 | 19 | 20 | 0 | 0 |
| `docs/guides/keys.md` | 9 | 16 | 69 | 0 | 0 |
| `docs/guides/license-format.md` | 6 | 19 | 37 | 0 | 0 |
| `docs/guides/generating-licenses.md` | 7 | 16 | 80 | 0 | 0 |
| `docs/guides/validating-licenses.md` | 10 | 36 | 90 | 0 | 0 |
| `docs/guides/device-identity.md` | 7 | 28 | 50 | 0 | 0 |
| **Total** | **40** | — | **346** | **0** | **0** |

The 35-snippet compile **succeeded with zero warnings**. The five signature listings match their
sources exactly:

| Listing | Verified against |
|---|---|
| `device-identity.md` — `DeviceIdentifier.Generate()` | `DeviceIdentifier.cs:53` |
| `generating-licenses.md` — three `ILicenseGenerator` members | `ILicenseGenerator.cs:61, 75, 111` |
| `keys.md` — five `IKeyService` members | `IKeyService.cs:57, 78, 105, 134, 162` |
| `license-format.md` — `CurrentFormat`, `ReadLicense`, `SaveAsync`, `LoadAsync` | `LicenseEnvelope.cs:64, 132, 142, 179` |
| `validating-licenses.md` — three `ILicenseValidator` members | `ILicenseValidator.cs:80, 104, 134` |

One nuance worth recording: `keys.md` writes the RSA default as the literal `3072` where the source says
`KeyFormat.DefaultRsaKeySizeBits`. That is **correct, not a mismatch** — `KeyFormat` is internal, so a
public guide could not name it, and the constant is `3072` (`KeyFormat.cs:75`).

## Pack-verify

Packed into the session scratchpad, **not** `./artifacts-verify` (which `.gitignore` does not cover), and
the directory was deleted afterwards. Every assertion held:

| Assertion | Result |
|---|---|
| `.nupkg` version | `Enigma.Licensing.2.0.0.nupkg` ✅ |
| Output holds the `.nupkg` and nothing else (no `.snupkg`) | ✅ — one file, 222,127 bytes |
| `README.md` embedded and non-empty | ✅ — 6,282 bytes, carrying the 2.0 callout |
| `LICENSE.md` embedded | ✅ — 1,063 bytes |
| nuspec `<version>` | `2.0.0` ✅ |
| nuspec `<title>` | `Enigma.Licensing — .NET License Generation & Validation` ✅ |
| nuspec `<license type="file">` | `LICENSE.md` ✅ |
| nuspec `<readme>` | `README.md` ✅ |
| nuspec `<releaseNotes>` | the new breaking-release prose ✅ |
| XML doc file per TFM | 3 of 3 ✅ (net10.0, net8.0, netstandard2.0) |
| No stale `License.Features` / `HasFeature` in any XML doc | ✅ — 0 occurrences in all three |
| Dependency floors, net10.0 / net8.0 | DeviceId 6.11.0, Enigma.Core 2.0.0, DI.Abstractions 9.0.18, Ulid 1.4.1 ✅ |
| Dependency floors, netstandard2.0 | the same four **plus** System.Buffers 4.6.1 and System.Text.Json 10.0.12 ✅ |
| PolySharp absent from every group | ✅ |
| `Microsoft.NET.ILLink.Tasks` absent | ✅ |
| Assembly version of the packed net10.0 DLL | `2.0.0.0` ✅ |

## Decisions taken at build time

1. **`<Description>` was corrected as well as `<PackageReleaseNotes>`.** The plan listed only the notes,
   but the description still advertised "feature flags, arbitrary claims" — and it is the paragraph
   nuget.org renders under the package title. Shipping a description of members the package does not have
   would be the most visible possible instance of the docs-stay-honest rule failing.
2. **`SECURITY.md` lists 1.0.x explicitly as unsupported** rather than dropping its row. A table that
   simply loses a version reads as an oversight; a row saying `:x:` says the policy was applied.
3. **The migration guide carries a "what you should *not* do" section.** The obvious wrong reaction to
   losing `Features` is to smuggle the same data through `ProductId` or `Owner`. Those fields **are**
   validated, so a value crafted to carry entitlements changes what the licence matches — a worse
   outcome than the members that were removed. The guide names the supported alternative instead: a
   distinct product identifier per tier, matched with the wildcard the validator already supports.
4. **The `RELEASENOTES.md` 1.0.0 section was left describing feature flags and claims.** It is a record
   of what 1.0.0 shipped. Editing it would rewrite the history of a released version, and the 2.0.0
   section above it already says they are gone.
5. **The snippet gate compiled against the real project rather than the packed DLL.** Same surface, and
   a project reference gives file-and-line diagnostics on a mismatch instead of a missing-member error.
   The scratch project needed the **concrete** `Microsoft.Extensions.DependencyInjection` added, because
   one `validating-licenses.md` snippet calls `BuildServiceProvider()` — correct for a consumer, and not
   something the library itself references. That is a harness gap, not a snippet defect.
6. **The `1.0.0 → 2.0.0` file name follows `Enigma.Core`'s convention** (`1.1.0-to-2.0.0.md`), naming the
   *released* version it migrates from rather than a range.

## Deviations & follow-ups

- **`scripts/publish.sh` could not be run on this machine; `scripts/publish.ps1` was run instead.** The
  bash script stops with `error: 'zip' is not installed` — Info-ZIP is absent here. This is the exact
  **reverse** of what `FEATURE-57BC-PHASE04` recorded (`pwsh` absent, so the PowerShell twin reviewed but
  not executed): `pwsh` is still absent, but `publish.ps1` runs fine under **Windows PowerShell 5.1**.
  Both facts are now in `CLAUDE.md`. The bash script is unchanged and unverified on this machine; the
  plan's step 11 was satisfied by its twin, which builds the same artifacts.
- **Consequence for the linux-x64 artifact:** built here by `Compress-Archive`, which does not preserve
  the executable bit, so the Linux binary needs `chmod +x` after extraction — already stated in the
  script and in `docs/RELEASE.md`. A release cut on Linux with `publish.sh` would not need it.
- **`docs/RELEASE.md` is unchanged**, as planned: it is written throughout in `X.Y.Z` terms and is not
  versioned.
- **Follow-up candidates, not taken here and not blocking:** the coupled Avalonia set 12.1.1 → 12.1.2
  (the sibling Enigma.MarkdownEditor already runs 12.1.2 with the same control library, so the risk is
  low — it wants its own work item and its own test run), and `xunit.v3` 3.2.2 → 4.0.0.
- **Line endings:** no CRLF/LF churn appeared in this dev's diff, and nothing was done about line endings
  either way, per the workflow's recommendation-only rule.

## Build/test evidence

- `DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release --no-incremental` — **succeeded,
  0 warnings, 0 errors**.
- `dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal | grep -c AVLN`
  → **0**.
- `DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release` — **712 passed,
  0 failed** (426 library + 286 desktop on net10.0). The `error: 1` and non-zero exit are this machine's
  missing .NET 8 runtime, the documented condition.
- The net8.0 leg (`DOTNET_ROLL_FORWARD=LatestMajor` from `bin/Release/net8.0`) — **Total: 426, Errors: 0,
  Failed: 0**. Total across both legs and both suites: **1138 passing**.
- **Snippet gate:** 40 fences, 0 mismatches (table above).
- **Pack-verify:** every assertion passed (table above); the verify directory was deleted.
- **Publish scripts:** `scripts/publish.ps1 win-x64 linux-x64` printed `Enigma.Licensing.Desktop 2.0.0`
  — the version read back from MSBuild, which is the check that the csproj bump reached the artifacts —
  and produced `artifacts/Enigma.Licensing.Desktop-2.0.0-win-x64.zip` (46 MB) and
  `…-2.0.0-linux-x64.zip` (44 MB). Each holds the single-file executable, `README.md` and `LICENSE.md`
  and nothing else: no `.pdb`, no `.xml`, no loose assemblies. `artifacts/` is gitignored and
  `git status` confirms nothing from it is tracked.
- **Not run, deliberately:** `git tag`, `git push`, `dotnet pack -o ./artifacts` and `dotnet nuget push`.
  `docs/RELEASE.md` is the maintainer's.
