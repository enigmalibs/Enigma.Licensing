# FEATURE-03A9-PHASE03 — Generator, validator, store & device identity

**Status:** DONE
**Branch:** `feature/feature-03a9-phase03-services`
**Plan:** `docs/plan/FEATURE-03A9.md` (PHASE03)

## Summary

The library now signs and checks licenses. `ILicenseGenerator` turns a `LicenseRequest` into a signed
`enigma-license/1` envelope; `ILicenseValidator` returns a verdict over one; `ILicenseStore` keeps a
thread-safe registry that re-validates at query time; `DeviceIdentifier.Generate()` produces the string
a device-locked license is matched against. With this, everything the format promised in PHASE01 and
PHASE02 is actually exercised end to end — a license signed with any of the six schemes round-trips
through save → load → validate.

Three decisions are worth recording because they are not obvious from the plan text:

- **The verifier is chosen from the envelope's `algorithm`, not the key's.** `LicenseSignatures.Verify`
  takes the algorithm as an argument separate from the key precisely so a rewritten `algorithm` field is
  checked under the scheme the file claims and fails. That is the mechanism the `LicenseEnvelope` XML
  docs have asserted since PHASE01; it now has tests behind it (`Validate_RejectsASubstitutedAlgorithm`,
  five rows across both families, plus a cross-family row).
- **The license is untrusted input; the public key is not.** Nothing a license can be makes the
  validator throw — malformed JSON, bad base64, unknown format, unknown algorithm, broken signature and
  an expired window are all verdicts. The public key is the opposite: it ships inside the application,
  so a malformed or private-half key file throws. This split is what keeps "never throws" from
  swallowing a deployment fault, and it is documented on `ILicenseValidator`.
- **Signature verification fails closed.** `LicenseSignatures.Verify` catches broadly (excluding
  `OutOfMemoryException`, `StackOverflowException` and `OperationCanceledException`) and returns
  `false`. Enigma.Core wraps BouncyCastle, whose "not a key / not a signature" failures surface as
  several unrelated exception types that are not part of Core's documented contract; enumerating them
  would break on a Core or BouncyCastle bump for no benefit, since every one of them means the same
  thing at this seam. Failing closed is the safe direction — the worst case is a good license reported
  invalid, never the reverse.

## Files / modules touched

### Created — library

| Path | What it is |
|---|---|
| `src/Enigma.Licensing/ILicenseGenerator.cs` | Sign a request into an envelope; three overloads, error contract |
| `src/Enigma.Licensing/LicenseGenerator.cs` | Its implementation — defaults, request validation, `keyId` stamping |
| `src/Enigma.Licensing/ILicenseValidator.cs` | The verdict interface + the documented check order |
| `src/Enigma.Licensing/LicenseValidator.cs` | Its implementation |
| `src/Enigma.Licensing/ILicenseStore.cs` | The re-validating registry interface |
| `src/Enigma.Licensing/LicenseStore.cs` | Its implementation — lock over a list, snapshot-then-verify |
| `src/Enigma.Licensing/LicenseFailure.cs` | The seven failure reasons, in check order |
| `src/Enigma.Licensing/LicenseValidationResult.cs` | The verdict record + internal `Valid`/`Invalid` factories |
| `src/Enigma.Licensing/DeviceIdentifier.cs` | `Generate()` over the DeviceId package |
| `src/Enigma.Licensing/Internal/LicenseSignatures.cs` | Sign/verify dispatch across the two Core families |
| `src/Enigma.Licensing/Internal/ProductIdMatch.cs` | The wildcard rule (escape-then-substitute, `\A`/`\z`) |
| `src/Enigma.Licensing/Internal/DocumentFile.cs` | Shared path guard + open-for-read / create-with-parents |

### Modified — library

- `src/Enigma.Licensing/Internal/CoreAlgorithms.cs` — added `ToRsaSignatureAlgorithm`, the third leg of
  the enum → Core map. Still the only place naming an Enigma.Core algorithm type.
- `src/Enigma.Licensing/Keys/KeyService.cs` — folded onto `DocumentFile`; its private
  `RequireUsablePath`, the duplicated `FileStream` construction and the parent-directory creation are
  gone. Behaviour is unchanged and the PHASE02 tests (which assert `ParamName` and directory creation)
  pin that.
- `src/Enigma.Licensing/Enigma.Licensing.csproj` — unconditional `Ulid` and `DeviceId` references.
- `Directory.Packages.props` — pinned `Ulid` 1.4.1 and `DeviceId` 6.11.0; removed both from the
  "still to land" comment.

### Modified — documentation (freshness sweep)

- `CLAUDE.md` — full refresh. *Current state* now says the library signs and checks end to end and that
  DI registration is the only outstanding piece; a *Shipped* section for PHASE03 records the six
  load-bearing decisions; *Planned architecture* is renamed **Architecture & conventions** with the
  genuinely-planned PHASE04 items called out inline; the project-layout tree gains the twelve new
  library files and the two new test fixtures; the dependency section records Ulid, DeviceId and the
  netstandard2.0-only `Microsoft.DotNet.PlatformAbstractions` transitive; the fixture note now covers
  both collection fixtures. `README.md` and `RELEASENOTES.md` are deliberately empty until
  FEATURE-57BC-PHASE03 and were left alone.

### Created — tests

| Path | What it covers |
|---|---|
| `tests/…/Api/SignedLicenseFixture.cs` | Generate-once pair per algorithm (all six) + the theory data |
| `tests/…/Api/SignedLicenseTestData.cs` | Requests with real windows, save/load/edit an envelope |
| `tests/…/Api/LicenseGeneratorTests.cs` | Envelope shape, defaulting, every refused request, both save overloads |
| `tests/…/Api/LicenseValidatorTests.cs` | The full matrix (see below) |
| `tests/…/Api/LicenseStoreTests.cs` | Add/remove/query, replace-on-reissue, insertion order, concurrency |
| `tests/…/Api/DeviceIdentifierTests.cs` | Stability, non-disclosure of the machine name |
| `tests/…/Internal/ProductIdMatchTests.cs` | Wildcard + eight regex-metacharacter rows + anchoring |

## Deviations & follow-ups

**Deviations from the plan — one, and it is a naming/structure choice rather than a behaviour change:**

- The plan sketched `Validate(…, string? productId = null, …)` as "productId null → license's own". The
  implementation instead **skips** the product check when `productId` is null. The two are
  indistinguishable in behaviour — matching a license's product identifier against itself always
  succeeds, wildcards included — and skipping says what is meant without the indirection.

**Additions the plan did not spell out**, each because the alternative was worse:

- `Internal/DocumentFile.cs` was not in the plan. Without it the path guard and parent-directory
  creation would have existed in three copies (key service, generator, validator). `KeyService` was
  folded onto it in the same change so there is one implementation rather than two.
- `LicenseStore` has a second constructor taking `ILicenseValidator`. The plan's "public parameterless
  constructor" still holds and is what a caller uses; the store is simply the one service here that
  genuinely composes with another, and PHASE04's `TryAddSingleton` will resolve the richer constructor.
- All six algorithms are covered by the round-trip theory, not the "both RSA and ML-DSA at minimum" the
  phase asked for. Three extra RSA-2048 key generations cost roughly a second, and it discharges the
  item-level criterion now rather than at PHASE04.

**Follow-ups:**

- **PHASE04 must add `DeviceIdentifier` and the three new service types to the public-surface guard
  test.** No `Enigma.Core.*`, `Org.BouncyCastle.*`, `Ulid` or `DeviceId.*` type appears on any exported
  member today — `DeviceIdentifier.Generate()` returns a plain `string` for exactly that reason — but
  nothing enforces it until the reflection test lands.
- `DeviceId` 6.11.0 pulls `Microsoft.DotNet.PlatformAbstractions` 3.1.6 into the **netstandard2.0**
  dependency closure only. Restore and the NuGet audit are clean today; worth a look at release time
  (FEATURE-57BC-PHASE01's third-party licence audit) since that package is legacy.
- Serialization is still reflection-based, so the trim/AOT question recorded in `CLAUDE.md` is
  unchanged — the model is now complete enough for that decision to be taken deliberately at PHASE04 or
  release.
- No line-ending (CRLF) inconsistency was observed in any touched file; nothing to recommend.

## Build / test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded. 0 Warning(s), 0 Error(s)
    (from a cleaned obj/bin, across netstandard2.0 / net8.0 / net10.0)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → Passed!  total: 790  failed: 0  succeeded: 790  skipped: 0   (11s)
    net8.0 passed (10.7s) · net10.0 passed (11.1s)
```

**790 test cases, up from 468 at PHASE02 — 322 added** (161 per TFM). Suite runtime is essentially
unchanged (~11 s) because the new fixture generates its three RSA pairs at 2048 bits.

### Acceptance criteria

| Plan criterion | Evidence |
|---|---|
| Build + tests green, zero warnings | Above; clean rebuild across all three TFMs |
| Validation returns the correct `LicenseFailure` for every matrix row | `LicenseValidatorTests` — see the row list below |
| Both an RSA and an ML-DSA key, from a collection fixture | `SignedLicenseFixture`; all six algorithms, not just two |

Every matrix row the plan named is covered: valid license · expired · not-yet-valid · window with both
bounds · half-open window · wrong product · wildcard matches incl. eight regex-metacharacter
non-matches · device match / mismatch / absent / skipped · tampered payload (both in memory and edited
in the file) · tampered signature · swapped `algorithm` field (five rows, plus cross-family) · unknown
`algorithm` · unknown `format` · wrong public key · cross-algorithm key confusion · wrong ML-DSA
parameter set · malformed JSON and base64 returning a result rather than throwing (eight rows) · a
correctly-signed payload that is not a license · store add / remove / query / replace-on-reissue /
insertion order / concurrent reads and writes · generator defaulting and every argument rejection.
