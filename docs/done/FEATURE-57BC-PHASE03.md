# FEATURE-57BC-PHASE03 — README, release notes & community files

**Branch:** `feature/feature-57bc-phase03-readme-notes`
**Status:** DONE

## Summary

The three release documents a published package needs are written, and the last packaging property
is filled in. `README.md` and `RELEASENOTES.md` stop being zero-byte placeholders, `SECURITY.md` is
new, and `<PackageReleaseNotes>` lands in the library csproj beside them — written in the same unit
of work as `RELEASENOTES.md`, which is the whole reason PHASE01 deferred it.

**The package can now be packed.** PHASE01 recorded that `dotnet pack` failed **NU5040 — the readme
file is empty**, and that a real pack could not succeed before this phase. It does now: a pack into a
throwaway directory produced `Enigma.Licensing.1.0.0.nupkg` with a 6 148-byte `README.md` and the
1 063-byte `LICENSE.md` embedded, and the nuspec carrying the correct `version`, `title`, `readme`,
`license type="file"`, `projectUrl`, `repository` and a 907-character `releaseNotes` ending in
`See RELEASENOTES.md for the full details.` The directory was deleted; **PHASE04 still owns
pack-verify** with its full assertion list (this was a metadata sanity check that the phase's own
acceptance criterion — "README renders correctly as packed" — required).

The plan's step 5 (re-run the snippet gate for the README quick start) was run as a compiler, like
PHASE02's: the quick start compiles against the real library, and the five guides were re-run with it
to prove nothing drifted. 36 snippets, 0 errors, 0 warnings, plus a negative control.

## Files/modules touched

**Created**

- `SECURITY.md` — from the `dotnet-release` template. Supported versions `1.0.x`; GitHub private
  vulnerability reporting rather than an email address (the template's deliberate choice); the
  opening paragraph states the concrete consequence of a defect here — a forged licence accepted as
  valid defeats the product's licensing outright. The scope section names the formats, the
  sign/verify paths, the validation order, password handling and the public surface, adds the desktop
  app, and routes upstream issues to **Enigma.Core** and the **BouncyCastle** beneath it, since this
  library performs no cryptography of its own.

**Modified**

- `README.md` (was zero bytes) — the packed nuget.org landing page, from the package-README template:
  title, the two house badges, a one-paragraph intro built around the load-bearing idea (the
  signature covers the stored payload bytes exactly, so signer and verifier cannot disagree), the
  what's-new-in-1.0 callout, six Features bullets — one per library category — plus a
  registration/trim/AOT subsection, Installation with the TFM line, a Quick start, a prose-only
  Documentation pointer, a short desktop-app section, and License.
- `RELEASENOTES.md` (was zero bytes) — the first-release variant: positioning paragraph, Feature
  overview at more depth than the README's bullets, Compatibility, Version. Compatibility carries the
  TFM set, Enigma.Core 1.1.0 and the other runtime dependencies with their floors, and the **clean
  break** the plan asks for: Enigma.LicenseManager licences and keys are not readable here, no reader
  or converter is planned, and migrating means reissuing from a new pair.
- `src/Enigma.Licensing/Enigma.Licensing.csproj` — `<PackageReleaseNotes>` added, the twelfth and
  last packaging property. Prose summary of `RELEASENOTES.md`'s v1.0.0 section ending
  `See RELEASENOTES.md for the full details.`, per the skill. The comment that stood in its place was
  rewritten rather than deleted: it still records why there are no symbol properties and no
  `GeneratePackageOnBuild`, and now says the two release-note texts are written together so they
  cannot disagree.
- `docs/roadmap.md` — PHASE03 `TODO` → `IN PROGRESS` → `DONE`.
- `docs/plan/FEATURE-57BC.md` — the same, on the phase heading.
- `CLAUDE.md` — the documentation-freshness sweep, accepted. Five places: *Current state* now says
  PHASE03 landed and PHASE04 is last; the PHASE01 *Shipped* bullet about NU5040 is put in the past
  tense, with pack-verify still named as PHASE04's; the same section's closing line stops calling
  `<PackageReleaseNotes>` absent; the layout tree lists `README.md`, `RELEASENOTES.md` and the new
  `SECURITY.md` with what each is for, in place of the "Empty — written at release time" marker; and
  *Conventions → Documentation* gains **the packed-README link rule**, which is the one constraint in
  this phase that a future edit could silently break. The dependency-chain line at the foot reads
  `PHASE04 next`.

**Not touched**

No source file, no test, no `docs/guides/` file. The guides were re-verified, not edited.

## The packed-README link rule

The skill calls this a correctness constraint rather than a style preference, and it is the one thing
about this README that is easy to get wrong later: it is **packed**, so it renders on nuget.org where
the repository tree does not exist and a relative link into `docs/` is dead. What the file contains:

| Link | Kind | Why it is allowed |
|---|---|---|
| `https://img.shields.io/nuget/v/Enigma.Licensing.svg` → `nuget.org/packages/Enigma.Licensing` | absolute, badge | The NuGet badge; tracks the published version by itself, no per-release edit. |
| `LICENSE.md` (badge target and *License* section) | relative, **packed** | Packed to the package root by the csproj `ItemGroup`. |
| `RELEASENOTES.md` (what's-new callout) | relative, repo-root | The template's one permitted prose link. |
| `nuget.org/packages/Enigma.Core` (intro) | absolute, package page | A dependency's package page, not a repository-tree path. |

`docs/guides/` and `docs/guides/README.md` appear **as prose in backticks, never as links** — the
asymmetry with `docs/guides/README.md` itself, where relative links between the guides *are* correct,
is deliberate and is recorded in PHASE02's completion doc too.

## Snippet-verification gate

Re-run per the plan's step 5. The harness is PHASE02's, rebuilt in a scratch project outside the
repository: every ```` ```csharp ```` fence whose first line is a `using` **directive** is extracted,
wrapped in `internal static async Task RunAsync()` inside its own namespace, and compiled against the
real `Enigma.Licensing` project with `Nullable=enable`, `ImplicitUsings=disable` and
`TreatWarningsAsErrors=true`.

| File | Fences | Compiled | Prose/other | Symbol refs | Distinct symbols | Mismatches | Uncertain |
|---|---|---|---|---|---|---|---|
| `README.md` (quick start) | 2 | 1 | 1 (`bash`) | 30 | 28 | 0 | 0 |
| `docs/guides/license-format.md` | 6 | 5 | 1 signature listing | — | — | 0 | 0 |
| `docs/guides/keys.md` | 9 | 8 | 1 signature listing | — | — | 0 | 0 |
| `docs/guides/generating-licenses.md` | 8 | 7 | 1 signature listing | — | — | 0 | 0 |
| `docs/guides/validating-licenses.md` | 10 | 9 | 1 signature listing | — | — | 0 | 0 |
| `docs/guides/device-identity.md` | 7 | 6 | 1 signature listing | — | — | 0 | 0 |
| **Total** | **42** | **36** | **6** | — | — | **0** | **0** |

The guides' per-file symbol counts are PHASE02's and are not restated; they were not edited this
phase, and re-compiling them is the stronger check anyway — it proves the surface they describe still
exists after PHASE01's `ReadLicense()` addition and this phase's csproj edit.

```
python3 extract.py README.md docs/guides/*.md
  → 36 snippet(s) extracted, 5 fence(s) skipped (the JSON-adjacent signature listings)

dotnet build SnippetVerify.csproj -c Debug
  → Build succeeded. 0 Warning(s) 0 Error(s)

negative control: GenerateKeyPair → GenerateKeyPairNope in the README snippet, rebuild
  → error CS1061: 'KeyService' does not contain a definition for 'GenerateKeyPairNope'
```

Two harness notes worth keeping for PHASE04 or a later release, both about the *harness* and not the
docs:

- The extractor must split the leading `using` block at the **first non-directive line**, not the
  last line beginning `using ` — several guide snippets use `using var stream = …`, and treating that
  as a directive leaves the statements above it at file scope (`CS8802`). It must also emit a fence's
  type declarations as nested types rather than inside the method, since C# has no local class.
- The scratch project needs a `Microsoft.Extensions.DependencyInjection` reference — the DI snippet in
  `validating-licenses.md` calls `BuildServiceProvider()`, which lives in the concrete container. The
  library itself references only the abstractions, correctly; a consumer doing DI has both.

## Deviations & follow-ups

- **The TFM line is written in prose form** — `Targets **.NET Standard 2.0**, **.NET 8.0** and
  **.NET 10.0**; built on **Enigma.Core 1.1.0**.` — rather than the plan's literal
  `Targets **netstandard2.0;net8.0;net10.0**`. Same fact; the template's `{{TFM_LIST}}` guidance asks
  for the prose form, and a semicolon-separated MSBuild property reads as a build detail on a landing
  page. `RELEASENOTES.md`'s *Compatibility* uses the TFM identifiers themselves, where they are the
  precise thing a reader wants.
- **A real `dotnet pack` was run**, into the scratchpad and then deleted, rather than PHASE01's
  `-t:GenerateNuspec`. That target produced no file on this SDK when invoked standalone, and the
  phase's acceptance criterion is about the packed README specifically — which only a pack can show.
  This is the skill's permitted local pack-verify and it cleaned up after itself; **PHASE04's
  pack-verify is unaffected** and still owns the full assertion list (`.nupkg` only, dependency floors
  per TFM, and the rest).
- **`docs/RELEASE.md` is not written here** — it is PHASE04's step 1, and the runbook is not printed
  by this phase either.
- **The GitHub repository does not exist yet**, so `SECURITY.md`'s reporting flow (the Security tab's
  *Report a vulnerability*) is inert until the user creates the repo **and** enables *Private
  vulnerability reporting* in Settings → Security. Worth doing before the package is published, since
  the file tells readers that route works.
- **Line endings:** no CRLF/LF inconsistency observed in the files created or modified by this dev.

## Build/test evidence

The csproj changed, so this is not a documentation-only phase: the build is the check that
`<PackageReleaseNotes>` is well-formed XML and that nothing else moved.

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  Build succeeded.
      0 Warning(s)
      0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  Test run summary: Passed!
    Enigma.Licensing.UnitTests.dll (net8.0|x64)          passed (13s 448ms)
    Enigma.Licensing.UnitTests.dll (net10.0|x64)         passed (12s 854ms)
    Enigma.Licensing.Desktop.UnitTests.dll (net10.0|x64) passed (2s 834ms)
    total: 1164   failed: 0   succeeded: 1164   skipped: 0
```

Plus the pack sanity check and the snippet harness above. **No test was added.** Nothing in this
phase ships executable code; the README's quick start is the only new code, and the gate compiles it
against the real assembly, which is a stronger check than a unit test over prose would be.

## Acceptance criteria

| Criterion | Status |
|---|---|
| `README.md` packed-page shape: badges, intro, what's-new callout, Features, Installation + TFM line, Quick start, Documentation, License | ✅ |
| Documentation section points at `docs/guides/` **in prose only** — no relative doc links | ✅ — table above |
| Short desktop-app section, prose only | ✅ |
| `RELEASENOTES.md` first-release variant with the clean-break note | ✅ |
| `<PackageReleaseNotes>` consistent with `RELEASENOTES.md`, ending `See RELEASENOTES.md for the full details.` | ✅ — read back out of the packed nuspec |
| `SECURITY.md` present, upstream scope note covering Enigma.Core/BouncyCastle | ✅ |
| Snippet gate re-run for the README quick start, recorded | ✅ — compiled, not read; 36 snippets, 0 errors |
| Release build zero-warning, whole suite green | ✅ — 0 warnings, 1164/1164 |
