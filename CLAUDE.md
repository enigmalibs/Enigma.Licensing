# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## Current state

`FEATURE-2FE8` stood up the repo, the root configuration, a multi-targeted library and an MTP-native
xUnit v3 test project. `FEATURE-03A9-PHASE01` deleted the bootstrap placeholder and shipped the
**license model and the `enigma-license/1` envelope format** — see *Shipped* below.

Nothing signs or verifies yet. There are **no keys, no services and no DI registration**: the next
three phases of `FEATURE-03A9` add them, and until then everything under *Planned architecture* is
the agreed design recorded in `docs/plan/`, not code that exists. Read the plan file before
implementing any of it; the plan is the contract. Never write a doc, comment or summary that
describes a planned type as if it already ships.

## What this is

**Enigma.Licensing** is a .NET library that generates signed software licenses and imports, checks
and validates them, signing with RSA or post-quantum ML-DSA keys. All cryptography comes from
[Enigma.Core](https://www.nuget.org/packages/Enigma.Core) 1.1.0. A companion Avalonia desktop app
(`FEATURE-179C`) will give the issuer a UI for the same operations.

It is the successor to **Enigma.LicenseManager** (v1.2.0, which stays on NuGet for legacy
consumers). It is a **clean break**: the license and key formats are new, and there is deliberately
**no compatibility with, reader for, or converter from** the predecessor's formats. Do not add one.

## Shipped

### License format `enigma-license/1` — signed JSON envelope (FEATURE-03A9-PHASE01)

`LicenseEnvelope` is an indented JSON object with exactly five fields, in this order:

```json
{
  "format": "enigma-license/1",
  "algorithm": "RSA-SHA256",
  "keyId": "3F9A1C40D2B87E56",
  "payload": "eyJpZCI6IjAxSllRN1pLM00…",
  "signature": "cHJldGVuZC1zaWduYXR1cmU…"
}
```

`payload` is base64 of the compact UTF-8 JSON of a `License`; `signature` is base64. `keyId` is
omitted when null, as is every optional field of the payload — `JsonIgnoreCondition.WhenWritingNull`
means an absent value and an explicit null are the same document. Everything else about the
serializer is left **strict**: camelCase names matched case-sensitively, no comments, no trailing
commas. Unknown *envelope* fields are ignored, though — they fall outside the signature anyway, and
rejecting them would rule out a future optional field within format 1.

**Load-bearing invariant — the signature covers the exact payload bytes, and there is no
canonicalization step.** The predecessor's hand-rolled `GetDataForSignature()` string was the source
of its critical v1.0.0 bug; this design eliminates the entire class of defect. Never reintroduce a
"build a string to sign" helper, and never re-serialize a `License` in order to check one. Algorithm
substitution is defeated by *validating* rather than canonicalizing: the verifier is chosen from the
envelope's `algorithm` value, so flipping that field makes the signature fail to verify under the
substituted scheme (and the ML-DSA parameter set is additionally bound to the key itself). This
reasoning lives in the XML docs of `LicenseEnvelope` and `LicensePayload`; a tamper test flipping
`algorithm` is part of `FEATURE-03A9-PHASE03`'s acceptance criteria.

`LicenseEnvelope.LoadAsync` checks `format` (a parse concern — it decides whether the document shape
is interpretable) but deliberately **not** `algorithm` (a verdict the validator has to be able to
report as `UnknownAlgorithm`, which it cannot do for a document that never parsed). It throws
`ArgumentException` — with the underlying `JsonException` as `InnerException` — for anything it
cannot parse; the *validator* is the layer that never throws.

Types in place: `License` (`HasFeature` is ordinal; `Features`/`Claims` coerce an assigned null to
empty, so an untrusted payload cannot hand downstream code a null), `LicenseRequest`,
`LicenseEnvelope` + `LicenseEnvelope.CurrentFormat`, `LicenseSignatureAlgorithm`, and the internal
seams `AlgorithmIds` (the single enum ↔ wire-string table), `LicenseJson` and `LicensePayload`.
`InternalsVisibleTo("Enigma.Licensing.UnitTests")` is declared in `Properties/AssemblyInfo.cs` so the
last two can be tested directly.

## Planned architecture

### Key format `enigma-key/1` — unified for both algorithms (FEATURE-03A9-PHASE02)

JSON envelope: `{ "format", "kind": "public"|"private", "algorithm", "keyId", "encryption"?, "key" }`.

- RSA material is the Enigma.Core PEM string (SPKI public / PKCS#8 private), always generated
  **unencrypted** at the Core layer — Core's PEM-level password support is deliberately unused.
- ML-DSA material is the raw FIPS 204 encoding (private key in expanded form, as Core emits).
- Private keys are optionally password-encrypted **at this envelope layer**, uniformly for both
  algorithms: Argon2id (Core `Argon2Service`; parameters + salt recorded in `encryption`) deriving an
  AES-256-GCM key (Core `BlockCipherService`; nonce + tag recorded). Public keys are never encrypted.
- `keyId` is the uppercase hex of the first 8 bytes of SHA-256 over `algorithmId ‖ publicKeyMaterial`
  — deterministic and identical for both halves of a pair. It is stamped into license envelopes at
  signing time.
- Passwords are `char[]`; the caller owns clearing them (Enigma.Core convention).

### Algorithms

`LicenseSignatureAlgorithm` covers the full Enigma.Core signing surface, with one internal
`AlgorithmIds` class as the **single source of truth** for the enum ↔ string mapping (mirroring
Enigma.Core's `SignatureAlgorithms.ToJcaName`). The enum and the mapping **ship** (PHASE01); only the
*Backed by* column is still planned:

| Enum member | Identifier | Backed by |
|---|---|---|
| `RsaSha256` / `RsaSha384` / `RsaSha512` | `RSA-SHA256` / `RSA-SHA384` / `RSA-SHA512` | `IPublicKeyService`, PKCS#1 v1.5, PEM keys |
| `MLDsa44` / `MLDsa65` / `MLDsa87` | `ML-DSA-44` / `ML-DSA-65` / `ML-DSA-87` | `IMLDsaServiceFactory.CreateMLDsaService(parameterSet)`, raw `byte[]` keys |

Matching is ordinal and case-sensitive both ways, and both directions use the unknown → null/false
pattern rather than throwing — `AlgorithmIds` maps, it does not decide what an unmappable value
means. Nothing outside that class may spell one of these strings.

### Services & DI (FEATURE-03A9-PHASE03 / PHASE04)

Four interfaces in `Enigma.Licensing` / `Enigma.Licensing.Keys`: `IKeyService` (generate, save, load
keys), `ILicenseGenerator` (sign a `LicenseRequest` into a `LicenseEnvelope`), `ILicenseValidator`
(stateless verdict over an envelope), `ILicenseStore` (thread-safe registry that re-validates at
query time). Registration is a single `AddEnigmaLicensing()` extension in the
`Microsoft.Extensions.DependencyInjection` namespace (predecessor convention), doing
`TryAddSingleton` for all four.

The model (`License`, `LicenseRequest`, `LicenseEnvelope` — all shipped — plus the planned
`LicenseValidationResult`) is **immutable**: sealed `init`-only records, varied with `with`. Dates are
`DateTimeOffset`, serialized ISO-8601 with an explicit offset and validated against
`DateTimeOffset.UtcNow`; there is **no clock abstraction** (tests craft validity windows instead).
Serialization is System.Text.Json with camelCase names — never Newtonsoft.

### Validation order

Documented in XML docs and fixed by the plan: envelope well-formed → format version known →
algorithm known → signature verifies over the payload bytes → payload deserializes →
`ValidFrom`/`ValidUntil` window vs `UtcNow` → productId match (exact, or regex with `Regex.Escape`
then `\*` → `.*`, anchored, when the **license's** productId contains `*`) → deviceId ordinal match.
The result carries the parsed `License` whenever the payload was parseable, even on failure.

### Error contract (Enigma.Core discipline)

- `ArgumentNullException` / `ArgumentException` for caller mistakes.
- `System.Security.Cryptography.CryptographicException` for crypto-input failures — wrong password,
  malformed or tampered key material.
- **Validation never throws** for an invalid or malformed license: `ILicenseValidator` returns a
  `LicenseValidationResult` with a `LicenseFailure` reason. It catches JSON/format/crypto/argument
  failures from parsing and maps them. (`LicenseEnvelope.LoadAsync` *does* throw on malformed
  input — the validator is the layer that never throws.)
- I/O exceptions propagate untouched.

**Load-bearing invariant — only `Enigma.Licensing` types on the public surface.** Enigma.Core is an
implementation detail: no `Enigma.Core.*` and no `Org.BouncyCastle.*` type may appear on any
exported type or member. `FEATURE-03A9-PHASE04` adds a reflection guard test modelled on
Enigma.Core's `BouncyCastleIsolationTests`. Keep it green.

## Project layout

Directories marked *planned* do not exist yet.

```
Enigma.Licensing.slnx                Solution (SLNX format; /src/ and /tests/ solution folders)
Directory.Build.props                Shared build defaults (Authors, Copyright, LangVersion 14, Nullable, TreatWarningsAsErrors)
Directory.Packages.props             Central Package Management (all package versions pinned here)
.editorconfig                        Code style + analyzer severities
.gitattributes                       LF everywhere; CRLF for .bat/.cmd
global.json                          SDK 10.0.100 (latestFeature); test runner = Microsoft.Testing.Platform
LICENSE.md                           MIT
README.md, RELEASENOTES.md           Empty — written at release time (FEATURE-57BC-PHASE03)
src/Enigma.Licensing/                The library
  License.cs                         The license data — the signed payload's model
  LicenseRequest.cs                  Generator input (PHASE03 consumes it)
  LicenseEnvelope.cs                 The enigma-license/1 document + SaveAsync/LoadAsync
  LicenseSignatureAlgorithm.cs       The six signing schemes
  Internal/AlgorithmIds.cs           Single source of truth for enum ↔ wire strings
  Internal/LicenseJson.cs            The two JsonSerializerOptions instances
  Internal/LicensePayload.cs         License ↔ signed UTF-8 JSON bytes
  Properties/AssemblyInfo.cs         InternalsVisibleTo for the test assembly
  Keys/                              planned — key format & IKeyService (FEATURE-03A9-PHASE02)
src/Enigma.Licensing.Desktop/        planned — Avalonia issuer app (FEATURE-179C-PHASE01)
tests/Enigma.Licensing.UnitTests/    xUnit v3 test suite
  SmokeTest.cs                       Toolchain smoke test
  LicenseTestData.cs                 Deterministic shared fixtures (no UtcNow — output is pinned)
  Api/                               Tests over the public surface
  Internal/                          Tests over the internal seams (needs InternalsVisibleTo)
  Model/                             Tests over the records
tests/Enigma.Licensing.Desktop.UnitTests/   planned (FEATURE-179C-PHASE01)
docs/roadmap.md                      Work-item registry
docs/plan/                           Per-item plans
docs/done/                           Per-dev completion records
docs/guides/                         planned — per-category usage guides (FEATURE-57BC-PHASE02)
scripts/                             planned — app zip publish scripts (FEATURE-57BC-PHASE04)
```

There is **no CI workflow**, deliberately — consistent with every sibling Enigma repo.

## Target frameworks & dependencies

- The library multi-targets **`netstandard2.0;net8.0;net10.0`** — it ships as a NuGet package, and
  the broad floor mirrors Enigma.Core so no Enigma.Core consumer is excluded (Enigma.DataEncryption
  precedent). The reason is recorded in a csproj comment; keep it there.
- Test projects target **`net8.0;net10.0`**. `netstandard2.0` is never a test TFM; its polyfill
  surface is exercised through the `net8.0` paths. The planned desktop app and its test project are
  **net10.0** only.
- **Currently pinned** in `Directory.Packages.props` — only what the code actually references:
  `PolySharp` 1.16.0, `System.Buffers` 4.6.1 and `System.Text.Json` 10.0.10 (all three
  **netstandard2.0 only** in the library), `xunit.v3` 3.2.2, `coverlet.collector` 10.0.1.
  Referencing `System.Buffers` or `System.Text.Json` on net8.0+ raises NU1510 and fails the
  zero-warning build — both are framework-provided there, and the library's JSON usage stays inside
  what System.Text.Json 8.0 supports. PolySharp (compile-only, `PrivateAssets=all`) is what makes
  `init`/`required` members and the nullable-analysis attributes work on netstandard2.0.
- **The only runtime dependency so far is System.Text.Json, and only on netstandard2.0.** Still to
  land (all MIT): Enigma.Core 1.1.0 (`FEATURE-03A9-PHASE02`), Ulid (license ids) and DeviceId
  (device identity) (`PHASE03`), Microsoft.Extensions.DependencyInjection.Abstractions at a low
  floor (`PHASE04`).
  Planned app dependencies: Avalonia 12.1.1 (the coupled Avalonia set is bumped only as a unit),
  Enigma.Avalonia.Desktop 1.0.0, Enigma.Icons / Enigma.Icons.Phosphor / Enigma.Icons.Avalonia 1.0.0,
  CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.10.
- **Add a pin when the reference actually lands**, not in advance — and never put `Version=` on a
  `<PackageReference>`; all versions live in `Directory.Packages.props`.

## Build & test

The SDK lives in `~/.dotnet` on this machine, so export `DOTNET_ROOT` (or prefix the command):

```bash
# Build the whole solution (Release)
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release

# Run the full test suite (Microsoft.Testing.Platform runner, per global.json)
DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
```

Note the `--solution` flag: on the .NET 10 SDK in MTP mode, `dotnet test <Solution>.slnx` is
rejected.

Zero-warning builds are enforced (`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`), so
any warning fails the build. XAML (`AVLN…`) warnings bypass `TreatWarningsAsErrors`, so once the
desktop app exists its warning count has to be checked by reading the build output.

Tests are **MTP-native**: `xunit.v3` + `coverlet.collector`, with **no** `Microsoft.NET.Test.Sdk`
and **no** `xunit.runner.visualstudio` — both break the Microsoft Testing Platform. `Xunit` is the
only implicit using in the test project. Test fixtures are copied by a `**/*.json` + `**/*.txt`
`PreserveNewest` glob.

The xunit.v3 analyzers run at warning level, so under `TreatWarningsAsErrors` **they are build
errors**. The one that bites constantly is `xUnit1051`: any call taking a `CancellationToken` must be
passed `TestContext.Current.CancellationToken`. `LicenseEnvelopeTests` keeps a
`private static CancellationToken Token => TestContext.Current.CancellationToken;` for this — copy
that pattern rather than suppressing the rule.

### Two ways this build goes red without anyone touching the repo

Both follow from `TreatWarningsAsErrors=true`. Recognise them before hunting for a regression that
isn't there:

- **`NETSDK1138` — net8.0 leaves support on 2026-11-10.** The SDK's end-of-life target-framework
  check is skipped for `Library` projects but live for `Exe`, so it applies to the **test project
  only**, never to the library. The first SDK release that adds `8.0` to
  `_EolNetCoreTargetFrameworkVersions` turns the test project's net8.0 leg into an error. Fix then:
  drop `net8.0` from the test project, or set `CheckEolTargetFramework` to false there. A comment at
  the property records this.
- **`NU1901`–`NU1904` — NuGet auditing.** Restore warnings are errors here too, auditing defaults to
  on at `low` severity, and on the net10.0 leg it audits the **transitive** closure. A newly
  published advisory against any pinned package — or anything it drags in — fails the build with no
  repo change. Respond with a targeted `NuGetAuditSuppress` for the specific advisory URL, or
  `WarningsNotAsErrors`; never a blanket `NoWarn`.

One measurement caveat that is *not* a warning: `coverlet.collector`'s only MSBuild hook registers a
**VSTest** data collector, and MTP-native `dotnet test` does not run the VSTest pipeline. The
reference is kept for consistency with `Enigma.Core`, but do not assume coverage is being collected —
MTP coverage needs `Microsoft.Testing.Extensions.CodeCoverage`. Measure before trusting a number.

## Conventions

- **Language/style.** C# 14, `Nullable=enable`, `ImplicitUsings=disable` (declare every `using`
  explicitly). File-scoped namespaces; `using`s outside the namespace with `System` first. Follow
  `.editorconfig`; do not introduce warnings.
- **Immutability.** Model types are sealed records with `init`-only members — a predecessor review
  finding. Services are sealed classes behind interfaces.
- **Error contract.** As specified above; it is part of the public API and is asserted by tests.
  Changing it is a breaking change.
- **Public surface.** Only `Enigma.Licensing` types — never an `Enigma.Core.*` or
  `Org.BouncyCastle.*` type. Keep the isolation guard test green once it exists.
- **No legacy compatibility.** Never add a reader, converter or fallback for Enigma.LicenseManager
  licenses or keys.
- **Serialization is reflection-based, so the library is not trim/AOT-safe.** A consumer publishing
  with `PublishAot`, or with `JsonSerializerIsReflectionEnabledByDefault=false`, gets
  `InvalidOperationException` on the first serialize. Fixing it means a source-generated
  `JsonSerializerContext` wired into `LicenseJson` — worth deciding deliberately once the model is
  complete (PHASE04 or the release phase), not discovering at v1.0.0.
- **Documentation.** Every public member carries XML docs; `GenerateDocumentationFile=true` is on
  from bootstrap, so a missing comment fails the zero-warning build. Per-category usage guides will
  live under `docs/guides/` (`FEATURE-57BC-PHASE02`).
- **Packaging metadata** (`PackageId`, `Version`, `Description`, …) is deliberately absent from the
  library csproj until release time (`FEATURE-57BC-PHASE01`), per the `dotnet-release` skill. The
  library and the app version independently, in their own csproj — there is no solution-wide
  `<Version>`.
- **Docs stay honest.** When a phase lands, update this file, `docs/roadmap.md` and the plan status
  in the same unit of work; do not leave "planned" markers on shipped code.

## Dev workflow (tracked work)

This repo plans and tracks work through a house workflow:

- `docs/roadmap.md` — the single registry of every work item (`FEATURE-HHHH`, `BUG-HHHH`,
  `CODE-REVIEW-HHHH`; large items are split into `-PHASENN` phases).
- `docs/plan/<ID>.md` — the full plan for each item (the contract a build implements).
- `docs/done/<ID>.md` — a completion record per finished item/phase.

Each unit of work gets its own branch (`feature/…`, `bugfix/…`, `review/…`), cut from the current
`HEAD`. A unit is **done** only when the build is warning-free, the whole test suite passes, the
plan's acceptance criteria are met, the roadmap/plan statuses are updated, and the completion doc is
written. Commits are left to the maintainer.

The sequence is a hard dependency chain: `FEATURE-2FE8` (repo skeleton — **done**) →
`FEATURE-03A9` (licensing core library, 4 phases — **PHASE01 done**) → `FEATURE-179C` (desktop app,
4 phases) → `FEATURE-57BC` (v1.0.0 release, 4 phases).
