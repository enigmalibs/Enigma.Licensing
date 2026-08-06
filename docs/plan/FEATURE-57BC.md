# FEATURE-57BC — First release (v1.0.0)

**Status:** TODO
**Type:** multi-phase (4 phases per the dotnet-release first-release shape, one branch per phase:
`feature/feature-57bc-phaseNN-<slug>`)
**Depends on:** FEATURE-03A9 and FEATURE-179C complete

## Objective

Release **Enigma.Licensing 1.0.0** to nuget.org (library) and cut the desktop app's v1.0.0
zip distribution, with the full house documentation set. Per dotnet-release: the skill's execution
boundary applies — in-repo edits + local pack-verify only; tag/pack/push are printed for the user.

## Fixed release facts (validated 2026-08-06)

- **PackageId** `Enigma.Licensing`, **Version 1.0.0** (in the library csproj), MIT.
- **Repository** `https://github.com/enigmalibs/Enigma.Licensing` (RepositoryUrl + PackageProjectUrl;
  the GitHub repo is created by the user before/at release).
- **Tags:** bare `X.Y.Z` (no `v` prefix). **No symbol packages** (house rule). **No CHANGELOG.md** —
  RELEASENOTES.md is the single source. **No CI** (consistent with all sibling repos).
- **App distribution: zip publish scripts** (win-x64 + linux-x64, self-contained single-file) —
  MSI explicitly declined, matching Enigma.Backup/HardCopy.

---

## PHASE01 — Package metadata & third-party license audit — **TODO**

Branch: `feature/feature-57bc-phase01-package-metadata`

1. Add all **12** packable-library properties to `src/Enigma.Licensing/Enigma.Licensing.csproj`:
   PackageId, Version 1.0.0, Title ("Enigma.Licensing — .NET License Generation & Validation"),
   Description (one paragraph: signed licenses, RSA + ML-DSA post-quantum, envelope format,
   features/claims, device binding), PackageTags (license licensing signature rsa ml-dsa fips-204
   post-quantum dotnet), PackageReadmeFile README.md, PackageLicenseFile LICENSE.md, RepositoryUrl,
   RepositoryType git, PackageProjectUrl, PackageReleaseNotes (written in PHASE03 — placeholder
   comment until then), GenerateDocumentationFile true (already on) — plus the packing ItemGroup
   (`..\..\README.md`, `..\..\LICENSE.md`, Pack=true, PackagePath=`\`).
2. Confirm `GeneratePackageOnBuild` is **off** and no symbol properties exist.
3. **Third-party license audit** (runtime deps only — first release requirement); expected set,
   verify against the actual csproj/CPM at build time:
   | Dependency | Kind | License |
   |---|---|---|
   | Enigma.Core 1.1.0 (→ BouncyCastle.Cryptography 2.7.0) | runtime, all TFMs | MIT (both) |
   | System.Text.Json (per final TFM strategy) | runtime | MIT |
   | Ulid | runtime | MIT |
   | DeviceId | runtime | MIT |
   | Microsoft.Extensions.DependencyInjection.Abstractions | runtime | MIT |
   | System.Buffers | runtime, netstandard2.0 only | MIT |
   | PolySharp | compile-only (PrivateAssets=all) | out of scope |
   | xunit.v3, coverlet.collector | test-only | out of scope |
   Record findings in the completion doc.

Acceptance: Release build stays zero-warning with the metadata in place; audit table in
`docs/done/FEATURE-57BC-PHASE01.md`.

## PHASE02 — Usage guides & index — **TODO**

Branch: `feature/feature-57bc-phase02-guides`

One guide per library category (the count follows the library), from the dotnet-release guide
template (intro → operations table → key/types table → `###`-per-scenario samples → notes):

1. `docs/guides/license-format.md` — the `enigma-license/1` envelope, payload fields, why
   signature-over-payload-bytes (no canonicalization), algorithm identifiers.
2. `docs/guides/keys.md` — the `enigma-key/1` format, generation (all algorithms + RSA sizes),
   password encryption, keyId fingerprints, IKeyService usage.
3. `docs/guides/generating-licenses.md` — LicenseRequest, features/claims, validity window,
   ILicenseGenerator scenarios.
4. `docs/guides/validating-licenses.md` — ILicenseValidator, LicenseFailure semantics, wildcard
   productId rules, ILicenseStore / HasValidLicense, embedding checks in a consuming app.
5. `docs/guides/device-identity.md` — DeviceIdentifier helper, its documented weaknesses, plugging
   custom schemes.
6. `docs/guides/README.md` index (relative links are correct here — repo-only file).

Guides may be delegated one-per-sub-agent (they split cleanly); the owner re-verifies every
snippet. **Snippet-verification gate:** cross-check every code fence against the real public
surface in `src/`; record the per-file coverage table (snippets · symbols · mismatches · uncertain)
in the completion doc.

Acceptance: all guides present, indexed, snippets verified with the table recorded.

## PHASE03 — README, release notes & community files — **TODO**

Branch: `feature/feature-57bc-phase03-readme-notes`

1. `README.md` (packed — nuget.org landing page, from the package-README template): title, the two
   house badges (NuGet version + MIT), intro, "What's new in 1.0" callout, Features, Installation
   (incl. `Targets **netstandard2.0;net8.0;net10.0**; built on Enigma.Core 1.1.0.` line),
   Quick start (generate keys → generate license → validate, copy-pasteable), Documentation section
   pointing at `docs/guides/` **in prose only** (no relative doc links — they die on nuget.org),
   License. Also a short Desktop-app section (what it is, zip distribution) — prose only.
2. `RELEASENOTES.md` — first-release variant: Feature overview · Compatibility (TFM set; explicit
   note: **clean break** — licenses/keys from Enigma.LicenseManager are not readable, successor
   project) · Version 1.0.0.
3. `<PackageReleaseNotes>` in the csproj — prose summary ending
   `See RELEASENOTES.md for the full details.`
4. `SECURITY.md` from the template (supported versions = 1.0.x, GitHub private vulnerability
   reporting, scope note covering upstream Enigma.Core/BouncyCastle).
5. Re-run the snippet gate for the README quick start (record in completion doc).

Acceptance: README renders correctly as packed (no doc-tree links), notes/callout/PackageReleaseNotes
consistent, SECURITY.md present.

## PHASE04 — Release runbook, publish scripts & pack-verify — **TODO**

Branch: `feature/feature-57bc-phase04-runbook`

1. `docs/RELEASE.md` from the dotnet-release template (placeholders: Enigma.Licensing,
   Enigma.Licensing.slnx, src/Enigma.Licensing/Enigma.Licensing.csproj, main).
2. `scripts/publish.ps1` + `scripts/publish.sh` (HardCopy pattern): read the app `<Version>` via
   `dotnet msbuild -getProperty:Version`, `dotnet publish -c Release -r {win-x64,linux-x64}
   --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
   -p:EnableCompressionInSingleFile=true -p:DebugType=none` →
   `artifacts/Enigma.Licensing.Desktop-<version>-<rid>.zip` (`artifacts/` stays gitignored).
3. Pre-flight: `dotnet build` + `dotnet test --solution Enigma.Licensing.slnx -c Release`
   (zero warnings, all green).
4. **Pack-verify** (run locally, then delete): pack into a throwaway dir; assert nupkg version
   1.0.0, `.nupkg` only (no `.snupkg`), README + LICENSE embedded and non-empty, nuspec
   version/title/license/readme/releaseNotes correct, dependency floors per TFM as expected.
5. Print the runbook (never run): merge to `main` → `git tag 1.0.0` + push → `dotnet pack -o
   ./artifacts` → `dotnet nuget push` (nuget.org, key never echoed) → run publish scripts for the
   app zips → post-publish verification list.

Acceptance: pre-flight green, pack-verify assertions all pass (verify dir deleted), runbook printed,
scripts produce both zips locally.

## Item-level acceptance criteria

- Library ready to publish as Enigma.Licensing 1.0.0 with complete, verified metadata and docs.
- App zips build for win-x64 + linux-x64.
- Everything outward-facing (tag, push, publish) left to the user, printed as commands.
