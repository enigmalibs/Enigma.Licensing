# FEATURE-03A9-PHASE01 — License model & envelope format — **DONE**

## Summary

The first real licensing code. The `enigma-license/1` signed-envelope format now exists end to end
as a data format: a `License` is serialized to UTF-8 JSON, those exact bytes become the envelope's
payload, and the envelope reads and writes itself to a stream. Nothing signs or verifies yet —
PHASE02 brings keys and PHASE03 the generator and validator — but the bytes a signature will cover
are settled here, and their shape is pinned by tests.

Five public API elements landed (`License`, `LicenseRequest`, `LicenseEnvelope`,
`LicenseSignatureAlgorithm`, and `LicenseEnvelope.CurrentFormat`), backed by three internal seams
(`AlgorithmIds`, `LicenseJson`, `LicensePayload`). `BootstrapPlaceholder.cs` is gone.

The design point the whole format turns on is implemented literally: **there is no canonicalization
step, and no code path re-derives what "should" have been signed.** `LicensePayload.Serialize`
produces bytes, the envelope stores those bytes, and a verifier will check the signature against the
bytes it found. The predecessor's `GetDataForSignature()` string-builder — the source of its critical
v1.0.0 bug — has no counterpart here and must never acquire one. The reasoning is written into the
XML docs of `LicenseEnvelope` and `LicensePayload`, including why algorithm substitution is defeated
by *validating* (the envelope's `algorithm` selects the verifier, so flipping it makes verification
fail) rather than by folding the field into the signed bytes.

## Files/modules touched

**Created**

| Path | Notes |
|---|---|
| `src/Enigma.Licensing/LicenseSignatureAlgorithm.cs` | The six-member enum — `RsaSha256/384/512`, `MLDsa44/65/87` |
| `src/Enigma.Licensing/License.cs` | Sealed record, `init`-only. `Features`/`Claims` are never null: an assigned null (what `"features": null` in a payload produces) coerces to empty via a backing field. `HasFeature` is ordinal, throws `ArgumentNullException` on null |
| `src/Enigma.Licensing/LicenseRequest.cs` | Sealed record, generator input. Only `ProductId` is required; the collections stay nullable here precisely because "unset" means "the generator decides" |
| `src/Enigma.Licensing/LicenseEnvelope.cs` | Sealed record + `CurrentFormat` const + `SaveAsync`/`LoadAsync`. Carries the format's load-bearing XML docs |
| `src/Enigma.Licensing/Internal/AlgorithmIds.cs` | The single enum ↔ wire-string table. `ToIdentifier` → `string?`, `TryParse` → `bool`; ordinal, case-sensitive |
| `src/Enigma.Licensing/Internal/LicenseJson.cs` | The two `JsonSerializerOptions` instances — camelCase, nulls omitted, everything else left strict |
| `src/Enigma.Licensing/Internal/LicensePayload.cs` | The payload codec. Throws `JsonException`; the envelope translates it for public callers |
| `src/Enigma.Licensing/Properties/AssemblyInfo.cs` | `InternalsVisibleTo("Enigma.Licensing.UnitTests")`, with the reason in a comment (DataEncryption precedent) |
| `tests/Enigma.Licensing.UnitTests/LicenseTestData.cs` | Deterministic fixtures — fixed timestamps, no `UtcNow`, because several tests pin serialized output byte-for-byte |
| `tests/Enigma.Licensing.UnitTests/Model/LicenseTests.cs` | 13 tests: defaults, null coercion, `HasFeature` ordinality, `with` semantics, value equality, offset retention |
| `tests/Enigma.Licensing.UnitTests/Model/LicenseRequestTests.cs` | 4 tests: required-field shape, nullable collections, `with`, equality |
| `tests/Enigma.Licensing.UnitTests/Internal/AlgorithmIdsTests.cs` | 25 tests: both directions for all six, round-trip over `Enum.GetValues`, an enum-size guard, undefined enum → null, 10 near-miss identifiers rejected |
| `tests/Enigma.Licensing.UnitTests/Internal/LicensePayloadTests.cs` | 21 tests: exact-JSON shape, date-format stability, determinism, round-trips, null collections, case sensitivity, 9 unreadable payloads |
| `tests/Enigma.Licensing.UnitTests/Api/LicenseEnvelopeTests.cs` | 31 tests: envelope shape and field order, byte-for-byte blob round-trip, `leaveOpen`, 5 rejected format versions, 11 unparseable documents, inner-exception preservation, argument guards |
| `docs/done/FEATURE-03A9-PHASE01.md` | This record |

**Modified**

| Path | Notes |
|---|---|
| `Directory.Packages.props` | Added `System.Text.Json` 10.0.10; comment extended to say why it is netstandard2.0-only |
| `src/Enigma.Licensing/Enigma.Licensing.csproj` | `System.Text.Json` joins `System.Buffers` in the netstandard2.0-only `ItemGroup` |
| `tests/Enigma.Licensing.UnitTests/SmokeTest.cs` | Doc comment only — it claimed the library "has no public types yet". Rewritten to say why the test still earns its place |
| `docs/roadmap.md` | `FEATURE-03A9` → `IN PROGRESS`; `PHASE01` → `IN PROGRESS` → `DONE` |
| `docs/plan/FEATURE-03A9.md` | Item status → `IN PROGRESS`; PHASE01 heading → `IN PROGRESS` → `DONE` |

**Deleted**

| Path | Notes |
|---|---|
| `src/Enigma.Licensing/BootstrapPlaceholder.cs` | Scope item 1 — its own doc comment named this phase as the deleter |

## Dependency decision (plan scope item 4)

The plan left the System.Text.Json per-TFM strategy to this phase and asked for it to be logged.

**Referenced on `netstandard2.0` only, pinned at 10.0.10.** `net8.0` and `net10.0` take it from the
shared framework; adding the package there would raise `NU1510` and fail the zero-warning build, so
the reference sits in the same conditional `ItemGroup` as `System.Buffers`, on the pattern the
bootstrap already established. 10.0.10 is the newest stable version and still ships a
`netstandard2.0` asset; its own netstandard2.0 dependency group asks for `System.Buffers` 4.6.1,
which is exactly what was already pinned, so the graph closes with no second version of anything.
The library's JSON usage is confined to what System.Text.Json 8.0 supports (required members,
`IReadOnly*` collection interfaces, `DateTimeOffset`), so the inbox versions on the modern TFMs are
sufficient — nothing needs a 9.x or 10.x API.

`Ulid` was **not** added: the plan predicted `License` would not need it, and it does not. Identifier
defaulting happens in the generator, so the pin lands with PHASE03. No Enigma.Core reference yet
either — nothing in this phase does cryptography.

## Format decisions made here (not pre-specified by the plan)

- **Optional fields are omitted, not written as `null`** (`JsonIgnoreCondition.WhenWritingNull`), so
  an absent value and an explicit null are the same document.
- **The payload is compact, the envelope is indented.** Indentation is cosmetic in both — the
  signature covers the payload bytes as stored, whatever they are — so the choice is purely about who
  reads which: nobody reads a base64-embedded payload in place, and a human does open the envelope.
- **Everything else is left strict**: case-sensitive property matching, no comments, no trailing
  commas. These documents are machine-written and signature-protected; leniency only widens what
  counts as conforming.
- **`LoadAsync` checks `format` but not `algorithm`.** The format version decides whether the
  document shape is interpretable at all, so it is a parse concern. An unknown algorithm is a
  *verdict* the validator must be able to report as `UnknownAlgorithm`, which it cannot do for a
  document that never parsed. A test pins each half of this.
- **Unknown envelope fields are ignored, not rejected.** They fall outside the signature either way,
  so refusing them protects nothing and would rule out a future optional field within format 1.
- **`LoadAsync` throws `ArgumentException` with the `JsonException` as `InnerException`**, rather
  than letting System.Text.Json's exception escape — it keeps the error contract stated in
  `CLAUDE.md` and keeps a third-party exception type off the public contract. Matches the
  `Enigma.DataEncryption` 1.1.0 precedent of preserving the underlying failure as an inner exception.

## Deviations & follow-ups

- **No deviation from the plan's scope.** All five scope items are implemented as written.
- **`AlgorithmIds` and the JSON/codec helpers live in an `Internal/` folder** (namespace
  `Enigma.Licensing.Internal`). The plan names the class but not its location; this follows
  `Enigma.DataEncryption`, satisfies `dotnet_style_namespace_match_folder`, and keeps the internal
  seam visibly separate from the public model.
- **`InternalsVisibleTo` was added**, which FEATURE-2FE8 deliberately deferred to "if and when its
  tests actually need internal access". They do: `AlgorithmIds` and `LicensePayload` are the format's
  two load-bearing seams and both need direct tests.
- **A `LicenseRequest` test file was added** although the plan's test list does not mention one. It
  is cheap and pins the shape PHASE03's generator will code against.
- **`SmokeTest.cs`'s doc comment was corrected.** It asserted the library has no public types — false
  as of this dev, and `CLAUDE.md` requires docs not to describe shipped code inaccurately. The test
  body is untouched.
- **Follow-up — the library is not trim/AOT-safe.** Serialization is reflection-based, so a consumer
  publishing with `PublishAot` or `JsonSerializerIsReflectionEnabledByDefault=false` gets
  `InvalidOperationException: Reflection-based serialization has been disabled` at the first
  serialize. Found for real: a .NET 10 file-based scratch app (which defaults that switch off) hit it
  while dumping the envelope shape for this record. The fix is a source-generated
  `JsonSerializerContext` wired into `LicenseJson`, which the System.Text.Json generator supports on
  all three TFMs. Out of scope here — it changes the shape of `LicenseJson` and touches every type
  PHASE02/03 will add, so it belongs with PHASE04's API-polish pass or the release phase, done once
  over the finished model. Worth deciding deliberately rather than discovering at v1.0.0.
- **Follow-up — `xUnit1051` is an error in this repo and will shape every later async test.** The
  xunit.v3 analyzer requires `TestContext.Current.CancellationToken` at any call taking a
  `CancellationToken`; under `TreatWarningsAsErrors` that is a build failure, not advice. The
  envelope suite exposes it as a `private static CancellationToken Token` property — the pattern
  PHASE02/03's stream-heavy suites should copy.
- **Documentation freshness sweep:** the maintainer chose a full `CLAUDE.md` refresh, folded into
  this dev's commit. *Current state* no longer says the repo has no licensing code; the
  `enigma-license/1` section moved out of *Planned architecture* into a new **Shipped** heading and
  gained the concrete envelope shape, the strictness/omission rules, and the format-vs-algorithm
  split at load; *Algorithms* now says the enum and mapping ship while the *Backed by* column does
  not; *Project layout* lists the real files instead of `BootstrapPlaceholder.cs`; *Target frameworks
  & dependencies* records the System.Text.Json pin and drops "no runtime dependency yet"; the
  dependency-chain line shows PHASE01 done. Two forward-looking notes were added: the `xUnit1051`
  build-error pattern under *Build & test*, and the trim/AOT caveat under *Conventions*.
  `README.md`/`RELEASENOTES.md` are empty by design until `FEATURE-57BC-PHASE03`, and there is still
  no `CHANGELOG.md`, `CONTRIBUTING.md` or `SECURITY.md` — nothing else was stale.
- **Line endings:** nothing to report. Every file created here is LF and `.gitattributes` enforces
  `* text=auto eol=lf`; no CRLF churn appears in the diff.
- **Not verified, by design:** the `netstandard2.0` leg is compiled but never executed — test TFMs
  are `net8.0`/`net10.0` per the bootstrap decision, and its polyfill surface is exercised through
  the net8.0 paths. The System.Text.Json 10.0.10 netstandard2.0 assets therefore have build-time
  coverage only.

## Build/test evidence

Verified from a clean tree on `feature/feature-03a9-phase01-model-envelope`, with
`DOTNET_ROOT=~/.dotnet`:

```
$ dotnet build Enigma.Licensing.slnx -c Release
  Enigma.Licensing -> .../bin/Release/netstandard2.0/Enigma.Licensing.dll
  Enigma.Licensing -> .../bin/Release/net8.0/Enigma.Licensing.dll
  Enigma.Licensing -> .../bin/Release/net10.0/Enigma.Licensing.dll
  Enigma.Licensing.UnitTests -> .../bin/Release/net10.0/Enigma.Licensing.UnitTests.dll
  Enigma.Licensing.UnitTests -> .../bin/Release/net8.0/Enigma.Licensing.UnitTests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test --solution Enigma.Licensing.slnx -c Release
  ...net8.0|x64) passed (611ms)
  ...net10.0|x64) passed (635ms)
Test run summary: Passed!  total: 190  failed: 0  succeeded: 190  skipped: 0
```

190 = 95 test cases on each of `net8.0` and `net10.0` (94 new, plus the pre-existing smoke test).

**Acceptance criteria**

| Criterion | Result |
|---|---|
| Build + tests green, zero warnings | ✅ 0 warnings / 0 errors across 3 library TFMs + 2 test TFMs; 190/190 passing |
| Envelope JSON matches the validated preview shape | ✅ verified twice — structurally by `SaveAsync_WritesTheDocumentedEnvelopeShape` (five fields, in order, camelCase, base64 blobs) and by eye against a real generated file, reproduced below |

The envelope a full license actually writes:

```json
{
  "format": "enigma-license/1",
  "algorithm": "RSA-SHA256",
  "keyId": "3F9A1C40D2B87E56",
  "payload": "eyJpZCI6IjAxSllRN1pLM004WDJONFA2UjhUMFYyVzRZIiwiY3JlYXRlZEF0Ijoi…",
  "signature": "cHJldGVuZC1zaWduYXR1cmUtYnl0ZXM="
}
```

…whose payload decodes to:

```json
{"id":"01JYQ7ZK3M8X2N4P6R8T0V2W4Y","createdAt":"2026-08-06T12:34:56+02:00","productId":"MyApp 2.*","deviceId":"DESKTOP-ABC123","validFrom":"2026-01-02T03:04:05+00:00","validUntil":"2027-01-02T03:04:05+00:00","owner":"Contoso Ltd","features":["export","sync"],"claims":{"seats":"25","tier":"pro"}}
```

**Plan test-list coverage** (PHASE01 scope item 5)

| Asked for | Where |
|---|---|
| Envelope JSON round-trip (stream) | `RoundTrip_ReturnsThePayloadAndSignatureBytesUnchanged`, `SaveAsync/LoadAsync_LeavesTheStreamOpen` |
| Payload codec round-trip incl. features/claims/window | `RoundTrip_PreservesEveryFieldOfAFullLicense`, `RoundTrip_SurvivesAwkwardClaimAndFeatureText` |
| Date format stability | `Serialize_WritesTheDocumentedShapeForAFullLicense`, `Serialize_KeepsSubSecondPrecision`, `RoundTrip_KeepsTheWrittenOffsetRatherThanNormalizingToUtc` |
| Unknown `format` rejected at load | `LoadAsync_RejectsAnUnknownFormatVersion` (5 cases) |
| Malformed base64 → load throws | `LoadAsync_ThrowsForADocumentItCannotParse`, `LoadAsync_KeepsTheUnderlyingParseFailureAsTheInnerException` |
| Record immutability (`with` semantics) | `With_ProducesANewInstanceAndLeavesTheOriginalIntact` ×3 (license, request, envelope) |
| `HasFeature` ordinal behaviour | `HasFeature_FindsAGrantedFeature`, `HasFeature_RejectsAnythingButAnExactMatch` (6 cases) |

An additional guard beyond the list: `AlgorithmIdsTests` asserts the wire strings as literals rather
than through the constants that produce them, and pins the enum at six members — so a seventh
algorithm, or a typo in one identifier, fails the suite rather than shipping.
