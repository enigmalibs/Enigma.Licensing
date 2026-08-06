# FEATURE-03A9 — Licensing core library

**Status:** IN PROGRESS
**Type:** multi-phase (4 phases, one branch per phase: `feature/feature-03a9-phaseNN-<slug>`)

## Objective

Implement the `Enigma.Licensing` library: generate signed licenses and import/check/validate them
with services, signing with RSA or ML-DSA keys via Enigma.Core 1.1.0. New format, clean break from
Enigma.LicenseManager (no legacy reading, no conversion).

## Design decisions (validated 2026-08-06)

- **License format `enigma-license/1` — signed JSON envelope.** Outer JSON
  `{ "format", "algorithm", "keyId", "payload", "signature" }`; `payload` = base64 of the UTF-8
  JSON license data; the signature covers the **exact payload bytes** — no canonicalization step
  (the predecessor's hand-rolled `GetDataForSignature()` string was the source of its critical
  v1.0.0 bug; this design eliminates the class). `format` and `algorithm` are also covered:
  sign `payloadBytes` where the payload JSON itself embeds nothing — instead protect against
  algorithm substitution by *validating* that the envelope's algorithm is the one the signature
  verifies under (an attacker changing `algorithm` makes verification fail because the verifier
  is chosen from the envelope value and the signature bytes won't verify under another scheme;
  additionally the ML-DSA parameter set is bound to the key itself). Document this reasoning in
  XML docs; add an explicit tamper test flipping `algorithm`.
- **Algorithms — full Enigma.Core surface.** `LicenseSignatureAlgorithm` enum:
  `RsaSha256`, `RsaSha384`, `RsaSha512`, `MLDsa44`, `MLDsa65`, `MLDsa87`. String identifiers
  (`"RSA-SHA256"`, `"RSA-SHA384"`, `"RSA-SHA512"`, `"ML-DSA-44"`, `"ML-DSA-65"`, `"ML-DSA-87"`)
  mapped by one internal `AlgorithmIds` class (single source of truth, mirroring Enigma.Core's
  `SignatureAlgorithms.ToJcaName`). RSA = PKCS#1 v1.5 via `IPublicKeyService` (PEM-string keys);
  ML-DSA via `IMLDsaServiceFactory.CreateMLDsaService(parameterSet)` (raw byte[] keys).
- **Key format `enigma-key/1` — unified for both algorithms.** JSON envelope:
  `{ "format": "enigma-key/1", "kind": "public"|"private", "algorithm", "keyId", "encryption"?, "key" }`.
  RSA material = the Enigma.Core PEM string (SPKI public / PKCS#8 private, always generated
  **unencrypted** at the Core layer — Core's PEM-level password support is deliberately unused);
  ML-DSA material = raw FIPS 204 encodings (private = expanded form, as Core emits). Private keys
  optionally password-encrypted **at this envelope layer**, uniformly for both algorithms:
  Argon2id (Enigma.Core `Argon2Service`; parameters + salt recorded in the `encryption` object)
  deriving an AES-256-GCM key (Enigma.Core `BlockCipherService`, nonce + tag recorded/embedded).
  `keyId` = uppercase hex of the first 8 bytes of SHA-256 over `algorithmId || publicKeyMaterial`
  (deterministic, identical for both halves of a pair); stamped into license envelopes at signing.
- **Model** — immutable (`init`-only records; fixes predecessor review finding #6). Dates are
  `DateTimeOffset`, serialized ISO-8601, validated against `DateTimeOffset.UtcNow` (no clock
  abstraction — tests craft validity windows instead).
- **Serialization** — System.Text.Json (predecessor review #17; no Newtonsoft), camelCase names.
  Reference the `System.Text.Json` package on the TFMs that need it (netstandard2.0 at minimum);
  prefer conditional references to keep net8/net10 on inbox versions — log the final dependency
  set in the completion doc.
- **Error contract** (Enigma.Core discipline): `ArgumentNullException`/`ArgumentException` for
  caller mistakes; `System.Security.Cryptography.CryptographicException` for crypto-input failures
  (wrong password, malformed key material); **validation never throws** for invalid/malformed
  licenses — it returns a result object; I/O exceptions propagate.
- **Public surface exposes only `Enigma.Licensing` types.** Enigma.Core stays an implementation
  detail (no `Enigma.Core.*` or `Org.BouncyCastle.*` types on the public API); guarded by a
  reflection test (Enigma.Core's `BouncyCastleIsolationTests` pattern).
- **Dependencies** (runtime, all MIT): Enigma.Core 1.1.0, System.Text.Json (as needed per TFM),
  Ulid (license ids), DeviceId (device-identity helper),
  Microsoft.Extensions.DependencyInjection.Abstractions (deliberate low floor, DataEncryption
  precedent).

## Public API sketch

```csharp
namespace Enigma.Licensing;

public enum LicenseSignatureAlgorithm { RsaSha256, RsaSha384, RsaSha512, MLDsa44, MLDsa65, MLDsa87 }

public sealed record License
{
    public required string Id { get; init; }                       // ULID
    public required DateTimeOffset CreatedAt { get; init; }
    public required string ProductId { get; init; }                // '*' wildcard supported at match time
    public string? DeviceId { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }                // null = valid immediately
    public DateTimeOffset? ValidUntil { get; init; }               // null = never expires
    public string? Owner { get; init; }
    public IReadOnlyCollection<string> Features { get; init; }     // empty by default
    public IReadOnlyDictionary<string, string> Claims { get; init; }
    public bool HasFeature(string feature);                        // ordinal comparison
}

public sealed record LicenseEnvelope                               // "enigma-license/1"
{
    public required string Format { get; init; }
    public required string Algorithm { get; init; }
    public string? KeyId { get; init; }
    public required byte[] Payload { get; init; }                  // serialized as base64
    public required byte[] Signature { get; init; }                // serialized as base64
    public Task SaveAsync(Stream output, CancellationToken ct = default);          // leaveOpen
    public static Task<LicenseEnvelope> LoadAsync(Stream input, CancellationToken ct = default);
}

public sealed record LicenseRequest                                // generator input
{
    public required string ProductId { get; init; }
    public string? Owner { get; init; }
    public string? DeviceId { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
    public IReadOnlyCollection<string>? Features { get; init; }
    public IReadOnlyDictionary<string, string>? Claims { get; init; }
    public string? Id { get; init; }                               // default: new ULID
    public DateTimeOffset? CreatedAt { get; init; }                // default: UtcNow
}

public interface ILicenseGenerator
{
    LicenseEnvelope Generate(LicenseRequest request, LicensingKey privateKey);
    Task<LicenseEnvelope> GenerateAndSaveAsync(LicenseRequest request, LicensingKey privateKey,
        Stream output, CancellationToken ct = default);
    Task<LicenseEnvelope> GenerateAndSaveAsync(LicenseRequest request, string privateKeyPath,
        char[]? password, string outputPath, CancellationToken ct = default);      // creates parent dirs
}

public enum LicenseFailure
{ InvalidFormat, UnknownAlgorithm, InvalidSignature, NotYetValid, Expired, ProductMismatch, DeviceMismatch }

public sealed record LicenseValidationResult(bool IsValid, LicenseFailure? Failure, string? Message, License? License);

public interface ILicenseValidator                                 // stateless; never throws on bad input
{
    LicenseValidationResult Validate(LicenseEnvelope envelope, LicensingKey publicKey,
        string? productId = null, string? deviceId = null);        // productId null → license's own
    Task<LicenseValidationResult> ValidateAsync(Stream licenseInput, Stream publicKeyInput,
        string? productId = null, string? deviceId = null, CancellationToken ct = default);
    Task<LicenseValidationResult> ValidateAsync(string licensePath, string publicKeyPath,
        string? productId = null, string? deviceId = null, CancellationToken ct = default);
}

public interface ILicenseStore                                     // thread-safe registry
{
    void Add(LicenseEnvelope envelope, LicensingKey publicKey);
    bool Remove(string licenseId);
    bool HasValidLicense(string productId, string? deviceId = null);
    License? FindValidLicense(string productId, string? deviceId = null);
}

public static class DeviceIdentifier
{
    public static string Generate();   // DeviceId pkg: MachineName + OsVersion (documented best-effort);
}                                      // validation compares plain strings, so custom schemes plug in freely

namespace Enigma.Licensing.Keys;

public enum KeyKind { Public, Private }

public sealed class LicensingKey                                   // parsed key, material internal
{
    public KeyKind Kind { get; }
    public LicenseSignatureAlgorithm Algorithm { get; }
    public string KeyId { get; }
}

public sealed record LicensingKeyPair(LicensingKey PublicKey, LicensingKey PrivateKey);

public interface IKeyService
{
    LicensingKeyPair GenerateKeyPair(LicenseSignatureAlgorithm algorithm, int rsaKeySizeBits = 3072);
    Task SaveKeyAsync(LicensingKey key, Stream output, char[]? password = null, CancellationToken ct = default);
    Task<LicensingKey> LoadKeyAsync(Stream input, char[]? password = null, CancellationToken ct = default);
    Task SaveKeyPairAsync(LicensingKeyPair pair, string publicKeyPath, string privateKeyPath,
        char[]? password = null, CancellationToken ct = default);  // creates parent dirs
    Task<LicensingKey> LoadKeyAsync(string path, char[]? password = null, CancellationToken ct = default);
}

// namespace Microsoft.Extensions.DependencyInjection (predecessor convention):
public static class EnigmaLicensingServiceCollectionExtensions
{
    public static IServiceCollection AddEnigmaLicensing(this IServiceCollection services);
    // TryAddSingleton: IKeyService, ILicenseGenerator, ILicenseValidator, ILicenseStore
}
```

**Validation order** (predecessor-proven, documented in XML docs): envelope well-formed → format
version known → algorithm known → signature verifies over payload bytes → payload deserializes →
`ValidFrom`/`ValidUntil` window vs UtcNow → productId match (exact, or regex with `Regex.Escape`
when the **license's** productId contains `*`) → deviceId ordinal match. Result carries the parsed
`License` whenever parseable, even on failure.

---

## PHASE01 — License model & envelope format — **DONE**

Branch: `feature/feature-03a9-phase01-model-envelope`

1. Remove the bootstrap placeholder source file.
2. `License`, `LicenseRequest`, `LicenseSignatureAlgorithm`, internal `AlgorithmIds` mapper
   (enum ↔ `"RSA-SHA256"`… strings, unknown → null/false pattern).
3. `LicenseEnvelope` with System.Text.Json serialization (camelCase, base64 byte[], ISO-8601
   DateTimeOffset), `SaveAsync`/`LoadAsync` (leaveOpen, CancellationToken), payload
   serialize/deserialize helpers (internal `LicensePayload` codec).
4. Add CPM pins + references: System.Text.Json strategy per TFM (log choice), Ulid deferred to
   PHASE03 unless `License` defaults need it (it should not — defaulting happens in the generator).
5. Tests: envelope JSON round-trip (stream + tampered-free), payload codec round-trip incl.
   features/claims/window fields, date format stability, unknown `format` string rejected at load,
   base64 malformed → load throws (documented; the *validator* is what never throws),
   record immutability (`with` semantics), `HasFeature` ordinal behavior.

Acceptance: build + tests green, zero warnings; envelope JSON matches the validated preview shape.

## PHASE02 — Key format & key service — **TODO**

Branch: `feature/feature-03a9-phase02-keys`

1. `enigma-key/1` envelope model (internal) + `LicensingKey`, `LicensingKeyPair`, `KeyKind`.
2. Key generation: RSA via `IPublicKeyService.GenerateRsaKeyPair(keySizeBits)` (2048–8192 accepted,
   default 3072, always unencrypted PEM); ML-DSA via `IMLDsaServiceFactory` per parameter set.
3. `keyId` fingerprint (SHA-256 over algorithmId ‖ public material, first 8 bytes, uppercase hex) —
   computed identically for public and private files.
4. Password encryption of private material: Argon2id (Core Argon2Service; sensible defaults,
   parameters recorded in the file) → AES-256-GCM (Core BlockCipherService). Wrong password / tampered
   ciphertext → `CryptographicException`. Password as `char[]` (caller owns clearing — Core convention).
5. `IKeyService`/`KeyService` save/load (streams + path overloads creating parent directories —
   fixes predecessor CLI pain point), error contract per design.
6. Tests: per-algorithm round-trips (all 6 enum members; RSA at 2048/3072/4096), encrypted
   round-trip, wrong password throws `CryptographicException`, tampered file throws, public files
   never encrypted (password on a public key → `ArgumentException`), fingerprint deterministic and
   pair-consistent, parent-directory creation, malformed JSON → `ArgumentException`/`CryptographicException`
   per contract.

Acceptance: build + tests green, zero warnings; key files match the validated preview shape.

## PHASE03 — Generator, validator, store & device identity — **TODO**

Branch: `feature/feature-03a9-phase03-services`

1. `ILicenseGenerator`/`LicenseGenerator`: defaults (ULID id, UtcNow createdAt), request validation
   (ProductId required, private key required + `Kind == Private`, window sanity: ValidFrom ≤ ValidUntil),
   sign payload bytes via algorithm dispatch (RSA `Sign(payload, pem, mappedAlg)`; ML-DSA
   `CreateMLDsaService(set).Sign`), stamp `keyId`, save overloads (parent dirs).
2. `ILicenseValidator`/`LicenseValidator`: the documented validation order; catches
   JSON/format/crypto/argument exceptions from parsing and maps to `LicenseValidationResult`
   (never throws for bad input; I/O propagates). Wildcard productId matching ported with the
   predecessor's *fixed* semantics (`Regex.Escape` then `\*` → `.*`, anchored) + regex-metachar tests.
3. `ILicenseStore`/`LicenseStore`: thread-safe (lock), `Add`/`Remove(licenseId)`/`HasValidLicense`/
   `FindValidLicense` re-validating via `ILicenseValidator` at query time.
4. `DeviceIdentifier.Generate()` via DeviceId package (MachineName + OsVersion; XML-doc the
   best-effort caveats: OS updates change it, identical VMs collide — same deliberate trade-off as
   the predecessor, no MAC because of removable adapters).
5. Add CPM pins + references: Ulid, DeviceId.
6. Tests — the full matrix, for **both** an RSA and an ML-DSA key at minimum (fixtures generated
   once via a collection fixture, Enigma.Core `RsaKeyFixture` pattern): valid license; expired;
   not-yet-valid (ValidFrom in future); window with both bounds; wrong product; wildcard matches
   (`MyApp 2.*`) incl. regex-metachar non-matches; device match/mismatch/absent; tampered payload;
   tampered signature; swapped `algorithm` field; wrong public key; cross-algorithm key confusion;
   malformed JSON/base64 → result not exception; store add/remove/query + concurrent access;
   generator defaulting and argument validation.

Acceptance: build + tests green, zero warnings; validation returns correct `LicenseFailure` for
every matrix row.

## PHASE04 — DI registration, API polish & XML docs — **TODO**

Branch: `feature/feature-03a9-phase04-di-polish`

1. `AddEnigmaLicensing()` (TryAddSingleton ×4) + M.E.DI.Abstractions pin (low floor, e.g. 9.x —
   DataEncryption precedent).
2. XML documentation on **every** public member (defaults, exception contracts, ownership of
   `char[]` clearing) — `GenerateDocumentationFile` stays on from bootstrap, so missing docs fail
   the zero-warning build.
3. Public-surface guard test (reflection walk over exported types): no `Org.BouncyCastle.*` and no
   `Enigma.Core.*` types anywhere on the public API (Enigma.Core's isolation-test pattern).
4. API review pass: sealed classes, `internal` ctors where material is passed, consistent naming,
   no `!` null-forgiveness without a commented reason.
5. Tests: DI resolution of all four services, singleton lifetime checks, TryAdd idempotence
   (double registration safe), guard test.

Acceptance: build + tests green, zero warnings; every public member documented; guard test passes.

## Item-level acceptance criteria

- All four phases done; `dotnet build`/`dotnet test --solution Enigma.Licensing.slnx -c Release`
  zero-warning and green across TFMs.
- A license generated with each of the six algorithms round-trips through save → load → validate.
- The predecessor pain-point list is demonstrably addressed: no canonicalization step, format
  version + algorithm + keyId explicit, immutable model, features + claims, interfaced services,
  parent-dir creation, System.Text.Json, DateTimeOffset.
