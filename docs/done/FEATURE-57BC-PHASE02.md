# FEATURE-57BC-PHASE02 — Usage guides & index

**Branch:** `feature/feature-57bc-phase02-guides`
**Status:** DONE

## Summary

`docs/guides/` now exists: five per-category guides plus their index, written from the
`dotnet-release` guide template (intro → operations/algorithms table → key types table →
`###`-per-scenario samples → notes). The count follows the library — the five categories are the two
document formats, the two halves of the workflow, and the device-identity helper.

The phase is documentation only; it touches no code and no csproj. The one thing it *does* have to
prove is that the samples are real, and the plan's snippet-verification gate is that proof.

**The gate was run as a compiler, not as a reading.** Every `csharp` fence carrying a `using` block
was extracted, wrapped in a method, and compiled against the actual `Enigma.Licensing` project in a
throwaway scratch project — 35 snippets, **zero errors and zero warnings on the first attempt**, with
`Nullable=enable` and `ImplicitUsings=disable` so the guides' `using` blocks had to be complete on
their own. A deliberately broken snippet (`DeviceIdentifier.GenerateNope()`) was then compiled to
confirm the harness actually fails — the failure mode of a verification pass is passing vacuously.
The scratch project lives outside the repository and is not committed; the two extraction scripts are
recorded below so the gate can be re-run in PHASE03 for the README quick start.

The five remaining fences are API-signature listings (`IKeyService`'s five members and so on) rather
than runnable code, and were checked member by member against the interface declarations by hand.
The three `json` fences were checked field by field, **including field order**, against the record
declarations that produce them — `KeyEnvelope`, `KeyEncryption` and `License` all serialize in
declaration order.

## Files/modules touched

**Created**

- `docs/guides/README.md` — the index. Repo-only, so relative links to the sibling guides are correct
  here (the packed root README, in PHASE03, must point at them in prose instead). Restates the core
  idiom so the page stands on its own, groups the five guides under three themes, and closes with a
  "where to start" split between issuing and consuming.
- `docs/guides/license-format.md` — the `enigma-license/1` envelope: the five fields, the payload's
  shape, the algorithm-identifier table, why the signature covers the payload bytes with no
  canonicalization step, and why rewriting `algorithm` does not help an attacker. Samples: reading a
  document, `ReadLicense()` with its unverified-result warning, writing one, decoding `Payload`, and
  handling a document that will not parse.
- `docs/guides/keys.md` — the `enigma-key/1` format: the six algorithms and the RSA size range, the
  three `key`-field encodings and which document field chooses between them, the Argon2id →
  AES-256-GCM layer, and key fingerprints. Samples: generate + save a pair, encrypt the private half,
  RSA at 4096, load each half, the stream overloads (including an embedded resource), cancellation.
- `docs/guides/generating-licenses.md` — `LicenseRequest` field by field, the half-open window, what
  the generator defaults per call, and the wildcard rule. Samples: the one-call path overload,
  features and claims, a validity window, reissuing under an existing identifier, a version wildcard,
  writing to a stream, and issuing many licenses from one loaded key.
- `docs/guides/validating-licenses.md` — the fixed eight-step check order as a table, all seven
  `LicenseFailure` reasons with a "typical response" column, `ILicenseStore`, and
  `AddEnigmaLicensing()`. Samples: checking a file, treating "no licence installed" as a normal state,
  a `switch` over every failure, validating an envelope already held, reading a key from an embedded
  resource, gating features, loading a store at start-up, `LicenseStore(ILicenseValidator)`, and DI.
- `docs/guides/device-identity.md` — `DeviceIdentifier.Generate()`, what it is built from, its three
  documented weaknesses, and the skipping-means-passing trap. Samples: reading the identifier,
  issuing a locked licence, checking it, an unlocked licence passing the check anyway, and a custom
  tenant-scoped scheme.

**Modified**

- `docs/roadmap.md` — PHASE02 `TODO` → `IN PROGRESS` → `DONE`.
- `docs/plan/FEATURE-57BC.md` — the same, on the phase heading.
- `CLAUDE.md` — the documentation-freshness sweep, accepted: *Current state* now says PHASE02 landed
  and PHASE03 is next, the layout tree lists the six real files in place of the `planned` marker on
  `docs/guides/`, *Conventions → Documentation* says the guides **live** there and records that their
  snippets are release-gated by compilation, and the dependency-chain line at the foot reads
  `PHASE03 next`. (That last line is the same status fact as *Current state*; leaving it would have
  contradicted the edit.)

**Not touched**

No source file, no csproj, no test. The guides are written *against* the public surface; nothing
about it changed, and nothing needed to.

## Snippet-verification gate

Per-file coverage. "Compiled" fences are the ones carrying a `using` block; "signature" fences are
API listings verified by hand; symbol references count occurrences of the library's public types,
members, enum members and documented parameter names inside the `csharp` fences.

| File | Fences | Compiled | Signature | JSON | Symbol refs | Distinct symbols | Mismatches | Uncertain |
|---|---|---|---|---|---|---|---|---|
| `license-format.md` | 6 | 5 | 1 | 2 | 44 | 22 | 0 | 0 |
| `keys.md` | 9 | 8 | 1 | 1 | 96 | 20 | 0 | 0 |
| `generating-licenses.md` | 8 | 7 | 1 | 0 | 116 | 24 | 0 | 0 |
| `validating-licenses.md` | 10 | 9 | 1 | 0 | 113 | 43 | 0 | 0 |
| `device-identity.md` | 7 | 6 | 1 | 0 | 60 | 30 | 0 | 0 |
| `README.md` (index) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **Total** | **40** | **35** | **5** | **3** | **429** | — | **0** | **0** |

**Zero mismatches is a compiler's answer, not a claim.** The harness:

```bash
# 1. extract   — every ```csharp fence whose first lines are `using` directives becomes one file,
#                wrapped in `namespace Snippets.<file>_<n> { internal static class S {
#                internal static async Task RunAsync() { … } } }`
python3 <scratchpad>/extract.py

# 2. compile   — net10.0, Nullable=enable, ImplicitUsings=disable, ProjectReference to the library
dotnet build <scratchpad>/SnippetVerify/SnippetVerify.csproj -c Debug
#              → Build succeeded. 0 Warning(s) 0 Error(s)

# 3. negative  — add a snippet calling DeviceIdentifier.GenerateNope(), rebuild
#              → error CS0117: 'DeviceIdentifier' does not contain a definition for 'GenerateNope'
```

`ImplicitUsings=disable` is what makes step 2 meaningful: it forces every fence's `using` block to be
complete, which is the template's second non-negotiable and the thing a reader actually pastes.

Portability notes that came out of writing the samples, since the library targets `netstandard2.0`
as well: the custom device-identity sample uses `SHA256.Create()` and `FormattableString.Invariant`
rather than `SHA256.HashData` and `string.Create(IFormatProvider, …)`, both of which are .NET 5+ only
and would not compile for a netstandard2.0 consumer. Streams are disposed with `using`, not
`await using`, for the same reason — `FileStream` is not `IAsyncDisposable` there.

## Deviations & follow-ups

- **Guides were written in one context rather than one sub-agent each.** The plan permits the split
  ("may be delegated one-per-sub-agent"); it was declined because this session's operating
  instructions forbid spawning agents unless the user asks. The five guides cross-reference each
  other heavily — the window semantics in `generating-licenses.md` and the `Expired` verdict in
  `validating-licenses.md` are the same fact from two sides — so a single context was defensible on
  its own merits, and the owner re-verifies every snippet either way.
- **Spelling normalised to "license" throughout**, matching the API and the library's own XML docs.
  Sample strings that read as UI copy ("This license expired on …") use the same spelling rather than
  the British form the desktop app's prose uses, so a reader copying a snippet gets something
  self-consistent.
- **PHASE03 must re-run this gate** for the README quick start, per the plan and the skill. The
  extraction script keys off ```` ```csharp ```` fences and a leading `using` block, so it will pick
  up a root `README.md` quick start unchanged.
- **The index is deliberately not linked from anywhere yet.** The root `README.md` is still a
  zero-byte placeholder; wiring the prose pointer to `docs/guides/` is PHASE03's step 1.
- **Line endings:** no CRLF/LF inconsistency observed in the files created or modified by this dev.

## Build/test evidence

Nothing in this phase compiles into a shipped artefact, but the Definition of Done was verified in
full anyway — the guides are written against the current surface, so a red build would mean they
describe something that no longer exists.

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  Build succeeded.
      0 Warning(s)
      0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  Test run summary: Passed!
    Enigma.Licensing.UnitTests.dll (net8.0|x64)          passed (13s 705ms)
    Enigma.Licensing.UnitTests.dll (net10.0|x64)         passed (15s 116ms)
    Enigma.Licensing.Desktop.UnitTests.dll (net10.0|x64) passed (2s 733ms)
    total: 1164   failed: 0   succeeded: 1164   skipped: 0
```

Plus the snippet harness above: 35 snippets compiled, 0 errors, 0 warnings, and a negative control
proving the harness is live. **No test was added** — the guides carry no code that ships, and the
scratch project is a verification tool rather than a permanent doc-sample project (the known
alternative, considered and declined by Enigma.Core for the same reason: it is a second build to keep
green, and the gate at release time is what catches drift).

## Acceptance criteria

| Criterion | Status |
|---|---|
| `docs/guides/license-format.md` — envelope, payload fields, signature-over-payload-bytes, algorithm identifiers | ✅ |
| `docs/guides/keys.md` — `enigma-key/1`, generation across all algorithms + RSA sizes, password encryption, keyId, `IKeyService` | ✅ |
| `docs/guides/generating-licenses.md` — `LicenseRequest`, features/claims, validity window, `ILicenseGenerator` scenarios | ✅ |
| `docs/guides/validating-licenses.md` — `ILicenseValidator`, `LicenseFailure` semantics, wildcard rules, `ILicenseStore`/`HasValidLicense`, embedding in an app | ✅ |
| `docs/guides/device-identity.md` — `DeviceIdentifier`, documented weaknesses, custom schemes | ✅ |
| `docs/guides/README.md` index, relative links correct | ✅ |
| Every snippet cross-checked against the real public surface in `src/` | ✅ — compiled, not read |
| Per-file coverage table recorded in the completion doc | ✅ |
| Release build zero-warning, whole suite green | ✅ — 0 warnings, 1164/1164 |
