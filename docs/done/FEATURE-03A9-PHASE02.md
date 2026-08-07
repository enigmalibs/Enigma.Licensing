# FEATURE-03A9-PHASE02 — Key format & key service — **DONE**

## Summary

The library now has keys. `enigma-key/1` exists end to end — one JSON format covering both algorithm
families and both halves of a pair — and `IKeyService`/`KeyService` generates, saves and loads them.
Enigma.Core 1.1.0 is referenced for the first time: RSA and ML-DSA key generation, Argon2id, AES-256-GCM,
SHA-256 and hex encoding all come from it, and none of its types touch the public surface.

Six public API elements landed (`IKeyService`, `KeyService`, `LicensingKey` + `LicensingKey.CurrentFormat`,
`LicensingKeyPair`, `KeyKind`), backed by seven internal seams (`KeyEnvelope`, `KeyEncryption`,
`KeyFormat`, `KeyKindIds`, `KeyFingerprint`, `KeyMaterial`, `KeyMaterialCipher`, `CoreAlgorithms`).

Two decisions carry the phase:

**Password protection lives at the envelope layer, uniformly.** Enigma.Core will happily write an
AES-256-CBC-encrypted private-key PEM, and ML-DSA has no equivalent — so using it would mean one
protection scheme for RSA keys and a second for ML-DSA keys, with different KDFs, different parameters
and two sets of failure modes. Instead the RSA PEM is always generated unencrypted at the Core layer
and both families' material bytes go through the same Argon2id → AES-256-GCM path, with the parameters
recorded in the file's `encryption` object. `KeyMaterial.Validate` rejects a PEM that is itself
encrypted, so the unused path fails clearly rather than confusingly.

**The `keyId` is computed once, from the public half.** SHA-256 over `algorithmId ‖ publicKeyMaterial`,
first 8 bytes, uppercase hex — so both files of a pair carry the same value, and PHASE03's generator can
stamp it into every license it signs. The material is hashed exactly as stored, with no normalization:
the same refusal to canonicalize that the license payload is built on.

## Files/modules touched

**Created**

| Path | Notes |
|---|---|
| `src/Enigma.Licensing/Keys/KeyKind.cs` | `Public`/`Private` — which half a file holds |
| `src/Enigma.Licensing/Keys/LicensingKey.cs` | Sealed class, internal ctor, material internal. Carries `CurrentFormat` and the `keyId` docs |
| `src/Enigma.Licensing/Keys/LicensingKeyPair.cs` | Positional record — the one moment both halves are together |
| `src/Enigma.Licensing/Keys/IKeyService.cs` | The five members of the plan's sketch, with the full error contract in XML docs |
| `src/Enigma.Licensing/Keys/KeyService.cs` | The implementation. Holds the two Enigma.Core services; stateless and singleton-safe |
| `src/Enigma.Licensing/Internal/KeyEnvelope.cs` | The six-field on-disk record. Internal, unlike `LicenseEnvelope` — a key file is only ever a way to get a key in and out of storage |
| `src/Enigma.Licensing/Internal/KeyEncryption.cs` | The `encryption` object: kdf, costs, salt, cipher, nonce, tag size |
| `src/Enigma.Licensing/Internal/KeyFormat.cs` | Format invariants, default costs, and the maxima accepted back |
| `src/Enigma.Licensing/Internal/KeyKindIds.cs` | The `KeyKind` ↔ wire-value table, on the `AlgorithmIds` pattern |
| `src/Enigma.Licensing/Internal/KeyFingerprint.cs` | The `keyId` construction |
| `src/Enigma.Licensing/Internal/KeyMaterial.cs` | `key`-field encoding/decoding per algorithm + the structural material check |
| `src/Enigma.Licensing/Internal/KeyMaterialCipher.cs` | Argon2id → AES-256-GCM, and the bounds on parameters read from a file |
| `src/Enigma.Licensing/Internal/CoreAlgorithms.cs` | The only place naming an Enigma.Core algorithm type: RSA-versus-ML-DSA and the FIPS 204 parameter set |
| `tests/Enigma.Licensing.UnitTests/Keys/GeneratedKeyFixture.cs` | Generate-once RSA-2048 + ML-DSA-65 pairs, collection fixture (Enigma.Core `RsaKeyFixture` pattern) |
| `tests/Enigma.Licensing.UnitTests/Keys/KeyTestData.cs` | Save/load/edit helpers over key documents + a self-deleting scratch directory |
| `tests/Enigma.Licensing.UnitTests/Keys/KeyGenerationTests.cs` | 27 tests: all six algorithms, RSA at 2048/3072/4096, material shapes, argument guards |
| `tests/Enigma.Licensing.UnitTests/Keys/KeyStorageTests.cs` | 53 tests: document shape, round-trips, 21 rejected documents, the path overloads |
| `tests/Enigma.Licensing.UnitTests/Keys/KeyEncryptionTests.cs` | 37 tests: encrypted round-trips, wrong/missing password, tampering, parameter bounds, password ownership |
| `tests/Enigma.Licensing.UnitTests/Internal/KeyFingerprintTests.cs` | 8 tests, including two known answers computed outside the library |
| `tests/Enigma.Licensing.UnitTests/Internal/KeyKindIdsTests.cs` | 14 tests, mirroring `AlgorithmIdsTests` |
| `docs/done/FEATURE-03A9-PHASE02.md` | This record |

**Modified**

| Path | Notes |
|---|---|
| `Directory.Packages.props` | Added `Enigma.Core` 1.1.0; comment now records that it is referenced on all three TFMs and brings BouncyCastle 2.7.0 transitively |
| `src/Enigma.Licensing/Enigma.Licensing.csproj` | Unconditional `Enigma.Core` reference replacing the placeholder comment |
| `src/Enigma.Licensing/Internal/LicenseJson.cs` | Doc comment only — the options now serve both envelope documents, licence and key |
| `docs/roadmap.md` | `PHASE02` → `IN PROGRESS` → `DONE` |
| `docs/plan/FEATURE-03A9.md` | `PHASE02` heading → `IN PROGRESS` → `DONE` |

## Dependency decision

**`Enigma.Core` 1.1.0, referenced unconditionally on all three TFMs.** It multi-targets exactly
`netstandard2.0;net8.0;net10.0`, so no conditional reference is needed and no TFM is left out. Its own
netstandard2.0 dependency group asks for `System.Buffers` 4.6.1 — already the pinned version — so the
graph closes with no second version of anything, and `BouncyCastle.Cryptography` 2.7.0 comes in
transitively. Restore is clean: no `NU1510`, and NuGet auditing (which reaches the transitive closure on
the net10.0 leg) raises nothing against either package today.

No other pin landed. `Ulid` and `DeviceId` belong to PHASE03, which is where identifiers and device
identity are decided.

## Format decisions made here (not pre-specified by the plan)

- **Field order is `format, kind, algorithm, keyId, encryption?, key`**, with `encryption` omitted
  entirely when absent (the existing `WhenWritingNull` rule). Its *presence* is what tells a reader that
  `key` holds ciphertext — the encoding is never guessed from the content.
- **The `key` field has three encodings, chosen by the document's own fields.** Encrypted: base64 of the
  GCM ciphertext, whatever the algorithm. Unencrypted RSA: the PEM text. Unencrypted ML-DSA: base64 of
  the raw FIPS 204 bytes. The plan fixed the material shapes; this is how they reach the file.
- **The GCM tag is appended to the ciphertext, not a field.** That is what Enigma.Core's
  `BlockCipherService` produces, and splitting it back out would be work in service of nothing.
  `tagBits` records its length, which is all a reader needs.
- **KDF costs read from a file are bounded** (64 iterations, 1 GiB, 64 lanes — the
  `Enigma.DataEncryption` numbers). The costs are read *before* the password is checked, so an
  unbounded reader would let a hostile key file allocate a terabyte and take the process down without
  ever being authenticated. Out-of-range → `CryptographicException`.
- **No AAD.** GCM authenticates the key material; the surrounding fields are not covered. Editing
  `algorithm` or `keyId` in a key file is therefore possible — and pointless, since it can only make the
  key fail to load or produce signatures that do not verify. Adding AAD would mean building a byte string
  out of metadata to authenticate, which is precisely the "build a string to sign" pattern this library
  is designed around avoiding.
- **`keyId` is read back verbatim, not recomputed.** It is an identifier, not a security control (the
  same reasoning already in `LicenseEnvelope.KeyId`'s docs), and a public key file whose `keyId` was
  edited can only cause a lookup miss.
- **Unknown `algorithm` in a *key* file is fatal**, unlike in a license envelope. A license naming an
  unimplemented scheme still parses so the validator can report `UnknownAlgorithm` as a verdict; there is
  no verdict to report about a key, only a key that cannot be used. A test pins each half of this.
- **A password for an unencrypted *private* key is ignored; for a *public* key it is rejected.** A caller
  holding a password cannot know in advance whether a given private key file needs it (Enigma.Core's PEM
  reader behaves the same way), whereas nobody has a legitimate reason to pass one for a public key — the
  format never encrypts those. The plan specified the second half; the first follows from it.
- **`SaveKeyPairAsync` refuses to write both halves to one path.** Not in the plan, three lines, and
  without it the second write silently destroys the first — possibly leaving an unencrypted private key
  where the public one was meant to be.

## Deviations & follow-ups

- **No deviation from the plan's scope.** All six scope items are implemented as written, and the public
  API matches the plan's sketch member for member.
- **`KeyFingerprint` blocks on one already-completed async call.** Enigma.Core hashes streams, and
  `GenerateKeyPair` is synchronous by the plan's sketch. The hash runs over a `MemoryStream` whose
  `ReadAsync` always completes synchronously, so the state machine never yields and `GetResult()` returns
  a finished task's value rather than blocking a thread. The alternatives were a second SHA-256
  implementation outside Enigma.Core or an async `GenerateKeyPair`; neither is an improvement. The
  reasoning is in a comment at the call site.
- **RSA material validation is structural, not a parse.** It catches empty material, RSA material that is
  not PEM at all, and a PEM encrypted at the PEM layer. It does not confirm that a well-formed PEM really
  holds an RSA key, nor that ML-DSA bytes are a valid FIPS 204 encoding — Enigma.Core exposes no
  parse-only entry point for either, and hardcoding the FIPS 204 key sizes would buy a little tampering
  detection at the cost of a spec constant that can drift. Those failures surface when the key is used,
  which is PHASE03's error-mapping problem.
- **`ArgumentException.ParamName` is `"input"` for a malformed document even via the path overload**,
  because that overload opens a `FileStream` and delegates. The exception *type* is the contract; the
  parameter name is a hint, and the message names the real problem.
- **System.Text.Json escapes `+` as `\u002B` inside the stored PEM.** The default encoder's behaviour,
  inherited from PHASE01's `LicenseJson`. It costs a few percent of file size and makes the PEM
  slightly less pleasant to read. Switching to `UnsafeRelaxedJsonEscaping` would fix it but changes the
  text of *both* document types, including a shipped one, for a purely cosmetic gain — a PEM inside a
  JSON string is not directly copy-pasteable anyway because of the `\n` escapes. Left alone deliberately;
  worth revisiting in PHASE04's API-polish pass if it is revisited at all.
- **Follow-up — the trim/AOT caveat from PHASE01 now covers two more types.** `KeyEnvelope` and
  `KeyEncryption` are serialized reflectively like the rest. The eventual source-generated
  `JsonSerializerContext` has to include them; nothing changes about the decision, only its size.
- **Follow-up — the netstandard2.0 leg still has build-time coverage only.** These are the first types in
  the library to do real cryptography, and none of it is executed under netstandard2.0 (test TFMs are
  net8.0/net10.0 by the bootstrap decision). Enigma.Core is tested on that TFM in its own repo.
- **Test-suite cost.** This phase adds ~10 s to the run, almost all of it RSA key generation (seven pairs,
  one at 4096 bits) and Argon2id at 64 MiB. The collection fixture keeps it to one RSA-2048 and one
  ML-DSA-65 pair for everything that only needs *a* key.
- **Documentation freshness sweep:** the maintainer chose a full `CLAUDE.md` refresh, folded into this
  dev's commit. *Current state* now says keys ship and names what is left; the `enigma-key/1` section
  moved out of *Planned architecture* into **Shipped** with the real file shape and every decision
  listed above; *Algorithms* records that generation ships for all six and adds `CoreAlgorithms`;
  *Services & DI* marks `IKeyService` shipped and notes the parameterless-constructor pattern (Core
  ships no DI extension); the *Error contract* gains the key-file cases; *Project layout* lists the 13
  new source files and the `Keys/` test folder; *Target frameworks & dependencies* records the
  Enigma.Core pin, why it is unconditional, and the BouncyCastle transitive; *Build & test* warns that
  key generation is the expensive part of the suite and points at the collection fixture;
  *Conventions* gains the `char[]` ownership rule and extends the trim/AOT note to the key documents;
  the dependency chain shows PHASE01–02 done. `README.md`/`RELEASENOTES.md` stay empty by design until
  `FEATURE-57BC-PHASE03`, and there is still no `CHANGELOG.md`, `CONTRIBUTING.md` or `SECURITY.md` —
  nothing else was stale.
- **Line endings:** nothing to report. Every file created here is LF and `.gitattributes` enforces
  `* text=auto eol=lf`; no CRLF churn appears in the diff.

## Build/test evidence

Verified from a clean tree on `feature/feature-03a9-phase02-keys`, with `DOTNET_ROOT=~/.dotnet`:

```
$ dotnet build Enigma.Licensing.slnx -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test --solution Enigma.Licensing.slnx -c Release
  ...net8.0|x64) passed (11s 180ms)
  ...net10.0|x64) passed (10s 959ms)
Test run summary: Passed!  total: 468  failed: 0  succeeded: 468  skipped: 0
```

468 = 234 test cases on each of `net8.0` and `net10.0` — 139 new, plus PHASE01's 95.

**Acceptance criteria**

| Criterion | Result |
|---|---|
| Build + tests green, zero warnings | ✅ 0 warnings / 0 errors across 3 library TFMs + 2 test TFMs; 468/468 passing |
| Key files match the validated preview shape | ✅ verified structurally by `SaveKeyAsync_WritesTheDocumentedShape` and `SaveKeyAsync_RecordsTheKeyDerivationParameters`, and by eye against real generated files, reproduced below |

A real RSA public key file:

```json
{
  "format": "enigma-key/1",
  "kind": "public",
  "algorithm": "RSA-SHA256",
  "keyId": "FDFC282A1BEC333A",
  "key": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAsJ5h8eWCvI/PpA4oCQ89\n…\nOQIDAQAB\n-----END PUBLIC KEY-----\n"
}
```

…and the matching private half of an ML-DSA-65 pair, password-protected:

```json
{
  "format": "enigma-key/1",
  "kind": "private",
  "algorithm": "ML-DSA-65",
  "keyId": "378A86040C38330F",
  "encryption": {
    "kdf": "argon2id",
    "iterations": 3,
    "memoryKb": 65536,
    "parallelism": 4,
    "salt": "irYyS9T16CHRZtOqGcFk0A==",
    "cipher": "AES-256-GCM",
    "nonce": "WsgxIPgp59l8GwTe",
    "tagBits": 128
  },
  "key": "YFzqD7f20lqz1KnA5tT7JqwjDl8vR/pyDwjnchIV5FiHk2RsealFA4cPkcuuuJQS…"
}
```

**Plan test-list coverage** (PHASE02 scope item 6)

| Asked for | Where |
|---|---|
| Per-algorithm round-trips, all six members | `GenerateKeyPair_ProducesAPairThatRoundTrips` (6 cases) |
| RSA at 2048/3072/4096 | `GenerateKeyPair_AcceptsTheSupportedRsaKeySizes` (3 cases) |
| Encrypted round-trip | `RoundTrip_RecoversTheMaterialWithTheRightPassword` (RSA + ML-DSA) |
| Wrong password → `CryptographicException` | `LoadKeyAsync_RejectsTheWrongPassword`, `LoadKeyAsync_RejectsAMissingPassword` |
| Tampered file throws | `LoadKeyAsync_RejectsATamperedFile` (key/salt/nonce), `LoadKeyAsync_RejectsTruncatedCiphertext` |
| Public files never encrypted; password on a public key → `ArgumentException` | `SaveKeyAsync_RejectsAPasswordForAPublicKey`, `LoadKeyAsync_RejectsAPasswordForAPublicKey`, `LoadKeyAsync_RejectsAPublicKeyCarryingEncryption`, `SaveKeyPairAsync_EncryptsOnlyThePrivateHalf` |
| Fingerprint deterministic and pair-consistent | `KeyFingerprintTests` (8), `GenerateKeyPair_GivesBothHalvesTheSameKeyId`, `GenerateKeyPair_GivesDifferentPairsDifferentKeyIds` |
| Parent-directory creation | `SaveKeyPairAsync_CreatesMissingParentDirectories` |
| Malformed JSON → `ArgumentException`/`CryptographicException` per contract | `LoadKeyAsync_ThrowsForADocumentItCannotParse` (9), the three unknown-field theories (11), `LoadKeyAsync_RejectsMalformedBase64Material`, `…RejectsRsaMaterialThatIsNotPem`, `…RejectsEmptyMaterial` |

Guards beyond the list: two **known-answer** fingerprint tests computed outside the library (so the
construction cannot drift silently), the KDF-cost bounds theory (8 cases, including `int.MaxValue`
memory), and `SaveAndLoad_LeaveTheCallersPasswordArrayIntact`, which pins the `char[]` ownership rule
the XML docs promise.
