# FEATURE-57BC-PHASE01 — Package metadata & third-party license audit

**Branch:** `feature/feature-57bc-phase01-package-metadata`
**Status:** DONE

## Summary

The library is now a packable NuGet project: all twelve packaging properties and the packing
`ItemGroup` are in place, `Enigma.Licensing` 1.0.0, MIT, README and LICENSE packed from the repository
root. The audit of what actually ships found nothing that blocks redistribution.

Two decisions `CLAUDE.md` parked on this phase were **missing from the plan file** and were surfaced
before implementation started; both were confirmed into scope and the plan was amended in place with
the reason:

- **The library is now trim- and AOT-safe.** Serialization was reflection-based, which meant a consumer
  publishing with `PublishAot` — or with `JsonSerializerIsReflectionEnabledByDefault=false` — got an
  `InvalidOperationException` on the first serialize. Two source-generated `JsonSerializerContext`
  classes replace it, and `IsAotCompatible` on the net8.0/net10.0 legs turns the trim and AOT analyzers
  into build errors so it cannot regress. This was the last phase that touches library *code*.
- **`LicenseEnvelope.ReadLicense()` exists**, so the generator's promise — that it hands back the
  envelope "so the caller can read back the license identifier it issued" — is now keepable. The
  desktop generate page drops the private `JsonDocument` parse it had been using instead.

The desktop app also gained an explicit `<Version>1.0.0</Version>`, which its own csproj comment
promised `FEATURE-57BC` would set alongside the library's metadata, and which PHASE04's publish scripts
read back to name the distribution zips.

## Files/modules touched

**Created**

- `src/Enigma.Licensing/Internal/EnvelopeJsonContext.cs` — source-generated metadata for the
  `enigma-license/1` envelope and the `enigma-key/1` key file (indented).
- `src/Enigma.Licensing/Internal/PayloadJsonContext.cs` — the same for the signed license payload
  (compact). Two contexts rather than one because a `JsonSerializerContext` is bound to the
  `JsonSerializerOptions` it was built with and refuses to resolve types for any other instance — so
  the two documented option sets cannot share one.
- `tests/Enigma.Licensing.UnitTests/Internal/LicenseJsonTests.cs` — 9 tests pinning the resolver, the
  format settings that moved off `JsonSerializerOptions` onto `[JsonSourceGenerationOptions]`, and —
  the one that matters — that a context answers **only** for what it was given, since a context that
  answered for an unregistered type would mean the resolver had silently fallen back to reflection.
- `docs/done/FEATURE-57BC-PHASE01.md` — this file.

**Modified**

- `src/Enigma.Licensing/Enigma.Licensing.csproj` — the twelve packaging properties, the README/LICENSE
  packing `ItemGroup`, and `IsAotCompatible` on the non-`netstandard2.0` legs.
- `src/Enigma.Licensing/Internal/LicenseJson.cs` — now hands out three `JsonTypeInfo<T>` instances
  (`Envelope`, `KeyFile`, `Payload`) instead of two `JsonSerializerOptions`. See *Deviations* below for
  why the type changed.
- `src/Enigma.Licensing/Internal/LicensePayload.cs` — gained `TryDeserialize`, the non-throwing
  counterpart of `Deserialize` that backs `ReadLicense`.
- `src/Enigma.Licensing/LicenseEnvelope.cs` — new public `ReadLicense()`.
- `src/Enigma.Licensing/Keys/KeyService.cs` — the two key-file call sites now use `LicenseJson.KeyFile`.
- `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` — explicit `<Version>1.0.0</Version>`.
- `src/Enigma.Licensing.Desktop/ViewModels/GenerateLicenseViewModel.cs` — the private `ReadLicenseId`
  `JsonDocument` parse deleted in favour of `envelope.ReadLicense()?.Id`; the `System.Text.Json` using
  went with it.
- `tests/Enigma.Licensing.UnitTests/Api/LicenseEnvelopeTests.cs` — 6 tests for `ReadLicense`.
- `tests/Enigma.Licensing.Desktop.UnitTests/TestDoubles/FakeLicenseGenerator.cs` — its default payload
  is now a **complete** licence document. See *Deviations*.
- `docs/roadmap.md`, `docs/plan/FEATURE-57BC.md` — statuses, plus the plan amendment.

## Third-party license audit

**Scope: runtime dependencies only** — what is redistributed inside a consumer's application.
Compile-only and test-only packages do not ship and are out of scope. Read from each package's own
`.nuspec` in the restored package cache, not from memory.

### Direct runtime dependencies

| Dependency | Version | TFMs | License | Verdict |
|---|---|---|---|---|
| Enigma.Core | 1.1.0 | all three | MIT (`LICENSE.md` in the package, MIT text, same author) | ✅ |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.18 | all three | MIT | ✅ |
| Ulid | 1.4.1 | all three | MIT | ✅ |
| DeviceId | 6.11.0 | all three | MIT | ✅ |
| System.Buffers | 4.6.1 | netstandard2.0 only | MIT | ✅ |
| System.Text.Json | 10.0.10 | netstandard2.0 only | MIT | ✅ |

### Transitive runtime dependencies

`net8.0` and `net10.0` close over exactly **one** transitive package; everything else below is the
`netstandard2.0` leg only, where the modern TFMs get the same functionality inbox.

| Dependency | Version | Arrives via | TFMs | License | Verdict |
|---|---|---|---|---|---|
| BouncyCastle.Cryptography | 2.7.0 | Enigma.Core | all three | MIT | ✅ |
| Microsoft.Bcl.AsyncInterfaces | 10.0.10 | DI.Abstractions, System.Text.Json | ns2.0 | MIT | ✅ |
| System.Threading.Tasks.Extensions | 4.6.3 | DI.Abstractions, System.Text.Json | ns2.0 | MIT | ✅ |
| System.Memory | 4.6.3 | Ulid, System.Text.Json | ns2.0 | MIT | ✅ |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | Ulid, System.Memory | ns2.0 | MIT | ✅ |
| System.Numerics.Vectors | 4.6.1 | System.Memory | ns2.0 | MIT | ✅ |
| System.Text.Encodings.Web | 10.0.10 | System.Text.Json | ns2.0 | MIT | ✅ |
| System.IO.Pipelines | 10.0.10 | System.Text.Json | ns2.0 | MIT | ✅ |
| Microsoft.DotNet.PlatformAbstractions | 3.1.6 | DeviceId | ns2.0 | MIT (`LICENSE.TXT`, .NET Foundation) | ✅ |
| Microsoft.NETCore.Platforms | 1.1.0 | Microsoft.DotNet.PlatformAbstractions | ns2.0 | **MICROSOFT .NET LIBRARY** (legacy `licenseUrl`) | ✅ — see note |

**The one non-MIT entry ships no code.** `Microsoft.NETCore.Platforms` 1.1.0 predates SPDX license
expressions and carries the deprecated `licenseUrl` form of the MICROSOFT .NET LIBRARY license (a
`dotnet_library_license.txt` in the package). Its entire content is a `runtime.json` RID graph and a
zero-byte `lib/netstandard1.0/_._` placeholder — it contributes **no assembly** to any consumer's
output, so nothing under that license is redistributed. It reaches the graph three levels down
(DeviceId → Microsoft.DotNet.PlatformAbstractions → here) and only on `netstandard2.0`. That license
does permit redistribution in any case; the newer versions of the package are MIT. No action.

`CLAUDE.md` flagged `Microsoft.DotNet.PlatformAbstractions` 3.1.6 as "a legacy package worth a look at
this audit": it is MIT and .NET Foundation-authored, so the concern is staleness rather than licensing,
and staleness is a DeviceId decision rather than one this repo can make.

### Out of scope (not redistributed)

| Package | Kind | Why |
|---|---|---|
| PolySharp 1.16.0 | compile-only (`PrivateAssets=all`) | Emits source; nothing in the package ships. MIT anyway. |
| Microsoft.NET.ILLink.Tasks 8.0.24 / 10.0.3 | SDK build task, auto-referenced | Pulled in by `IsAotCompatible`. Build-time only — **verified absent from the generated nuspec's dependency groups**. |
| NETStandard.Library 2.0.3 | auto-referenced targeting pack (ns2.0) | Framework reference assemblies. |
| xunit.v3 3.2.2, coverlet.collector 10.0.1, Microsoft.Extensions.DependencyInjection 9.0.18 | test-only | Never in the package. |

### Package license file

`LICENSE.md` (MIT, © 2026 Josué Clément) is present at the repository root, named by
`<PackageLicenseFile>`, and packed to the package root by the `ItemGroup`. Confirmed in the generated
nuspec: `<license type="file">LICENSE.md</license>` plus `<file src=".../LICENSE.md" target="/LICENSE.md" />`.

## Deviations & follow-ups

- **`LicenseJson` now exposes `JsonTypeInfo<T>`, not `JsonSerializerOptions`.** The first attempt kept
  the options shape and simply attached a source-generated resolver — and `IsAotCompatible` rejected it
  with 30 IL2026/IL3050 errors. Handing `JsonSerializer` a bare options object is not statically
  analyzable *even when* the options carry a source-generated resolver, so those overloads are
  attributed `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. Only the `JsonTypeInfo<T>` overloads
  are clean. The seam kept its name and its documentation; it gained a third member (`KeyFile`) because
  `KeyEnvelope` and `LicenseEnvelope` had been sharing one options instance and now need one type info
  each. This is a strictly better outcome than planned — the analyzer, not a reviewer, is what enforces
  it from here.
- **`IsAotCompatible` was not in the plan text and is on.** It is what makes the fix verifiable rather
  than asserted, and the library builds clean under it on both modern legs — including the Enigma.Core,
  Ulid, DeviceId and `Regex` call paths. It is deliberately **off** for `netstandard2.0`, which is never
  published AOT. Note the limit of the guarantee: the analyzers see this assembly's code, so a
  reflection-using dependency that is not annotated would still surface at a consumer's publish rather
  than here.
- **`FakeLicenseGenerator`'s default payload had to become a complete licence.** It was
  `{"id":"…"}` — enough for the old one-property hand-parse, not enough for `ReadLicense()`, which
  deserializes the whole record and so needs the three required fields. Two desktop tests failed on
  this and passed once the double emitted a real document. That is the double catching up with the
  production change, not a behaviour regression: the real generator always writes a complete licence.
  The double now echoes the request's `productId` back, which makes it more faithful than before.
- **The package cannot be packed yet, by design.** `dotnet pack` fails with **NU5040 — "The readme file
  'README.md' is empty"**, because `README.md` is a zero-byte placeholder until PHASE03 writes it. The
  metadata itself was verified by generating the nuspec directly (`-t:GenerateNuspec`) and reading it:
  version 1.0.0, title, `<license type="file">`, `<readme>`, repository/project URLs, tags, copyright,
  and dependency floors correct in all three groups with no ILLink and no PolySharp. **PHASE04's
  pack-verify is the first point at which a real pack can succeed** — it is blocked on PHASE03, which
  is the order the phases already run in.
- **`<PackageReleaseNotes>` is absent**, as the plan specifies — PHASE03 writes it with
  `RELEASENOTES.md` so the two say the same thing. A csproj comment records that.
- **Line endings:** no CRLF/LF inconsistency observed in any file touched. No action taken or needed.
- **Follow-up for PHASE02/PHASE03:** the guides and README quick-start should mention that the library
  is AOT- and trim-safe, since that is now a real, testable property and a differentiator worth stating
  on the nuget.org landing page. `RELEASENOTES.md`'s *Compatibility* section is the natural home for
  the `netstandard2.0`-is-excluded-from-that-claim caveat.

## Build/test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded.  0 Warning(s)  0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal | grep -c AVLN
  → 0        (XAML warnings bypass TreatWarningsAsErrors, so this is checked by hand)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → Test run summary: Passed!
    total: 1164   failed: 0   succeeded: 1164   skipped: 0   duration: 12s 920ms
```

**1134 → 1164 tests, +30**: 15 new tests × 2 TFMs in the library suite (9 in `LicenseJsonTests`, 6 in
`LicenseEnvelopeTests`). The desktop suite stays at 298 — its two failures were the test double, not
new coverage.

The zero-warning build is the acceptance criterion, and it now carries more weight than it did: with
`IsAotCompatible` on, the trim and AOT analyzers run inside it, so "builds clean" has come to mean
"nothing in this library needs runtime reflection". The broader proof is the suite itself — the two
source-generated contexts are the *only* resolver these documents have, so every save, load, sign and
verify in all 1164 tests ran through source generation. A document type that lost its metadata would
throw rather than quietly reflect.

## Acceptance criteria

| Criterion | Status |
|---|---|
| All 12 packable-library properties + the packing `ItemGroup` present | ✅ verified in the generated nuspec |
| `GeneratePackageOnBuild` off; no symbol properties | ✅ absent (grepped across every csproj and props file) |
| Third-party license audit recorded in the completion doc | ✅ above — 16 shipped packages, 15 MIT, 1 ships no code |
| Release build stays zero-warning | ✅ 0 warnings, 0 errors, and now with the trim/AOT analyzers live |
| *(amendment)* Trim/AOT: source-generated contexts, whole suite green through them | ✅ |
| *(amendment)* `LicenseEnvelope.ReadLicense()`; desktop drops its hand-parse | ✅ |
