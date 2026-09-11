# FEATURE-6C54 — Enigma.Core 2.0 migration

**Branch:** `feature/feature-6c54-enigma-core-2`
**Status:** DONE

## Summary

The library now builds on **Enigma.Core 2.0.0**. The major release breaks exactly one thing — RSA key
material no longer crosses Core's public surface as PEM text, but as a reusable
`Enigma.Core.Asymmetric.PublicKey.RsaKey` handle, and the per-call `char[]? password` parameters are
gone with it — and because the removed overloads were deleted rather than deprecated, the three call
sites in this repository failed to compile. That was the whole to-do list: nine errors (three sites ×
three TFMs), and no other member of Core that this library touches moved.

**Nothing observable about Enigma.Licensing changed.** `enigma-key/1` and `enigma-license/1` are
byte-identical across the bump (`ExportPublicKeyPem()` is still SPKI `PUBLIC KEY`,
`ExportPrivateKeyPem(null)` still unencrypted PKCS#8 `PRIVATE KEY`), the six algorithms behave as
before, the validation order and the error contract are untouched, and the library's own version stays
1.0.0 — `FEATURE-57BC` wrote the release documentation but nothing was ever tagged, packed or pushed,
so this is a change to what 1.0.0 *will be*, not an amendment to a released package.

The migration was taken in the shape the release intends rather than the shape that merely compiles:
**`LicensingKey` parses its material once and holds the handle.** BouncyCastle validates a private
key's CRT components while constructing it, which is why roughly 84 % of a 1.x `Sign(data, pem,
password)` call was key reconstruction; a `LicensingKey` is one key for its whole lifetime, so caching
there makes "parse once per key" structural instead of a rule someone has to remember. The
initialisation is lock-free (`Interlocked.CompareExchange` over a nullable field) because
`LicenseStore` snapshots its registry under a lock and then verifies *outside* it — one public key
genuinely is read from several threads at once — and an `RsaKey` is immutable, so a lost race discards
nothing but an identical handle.

Verification: solution builds Release with **0 warnings / 0 errors** on all three TFMs, the desktop
project's `AVLN` count is **0**, and the whole suite passes — **1174 cases, 0 failures** (438 net8.0 +
438 net10.0 in the library suite, 298 in the desktop one). See *Build/test evidence* for the one
environmental wrinkle in how the net8.0 leg had to be executed.

## Files/modules touched

**Modified**

- `Directory.Packages.props` — `Enigma.Core` `1.1.0` → `2.0.0`. Both comment blocks that describe the
  reference were updated: the library block now records the handle shape and that Core's own
  dependencies did not move, and the desktop block records why a control library built against Core 1.x
  is unaffected by the major bump.
- `src/Enigma.Licensing/Keys/KeyService.cs` — RSA generation is one `GenerateRsaKey(rsaKeySizeBits)`
  call whose handle is exported twice (`ExportPublicKeyPem()`, `ExportPrivateKeyPem(password: null)`)
  into the two material buffers the format stores. The comment explaining the unencrypted PEM survived
  because it is still the reason: file-level Argon2id + AES-256-GCM is what protects a private key.
- `src/Enigma.Licensing/Keys/LicensingKey.cs` — new `internal RsaKey RsaHandle`, backed by a
  `private RsaKey? _rsaHandle` field, importing by `Kind` (`ImportPublicKeyPem` for the public half,
  `ImportPrivateKeyPem` for the private one) and caching with `Interlocked.CompareExchange`. `Material`
  and `Pem` are unchanged. XML docs carry the why: parse cost, the store's concurrency, and the two
  exceptions the import can raise.
- `src/Enigma.Licensing/Internal/LicenseSignatures.cs` — `Sign` and `Verify` take the key's handle.
  `Verify` reads it **inside** its existing `try`, with a comment saying why: importing is where
  unusable RSA material now surfaces, and to the validator that is one more way of saying this key does
  not vouch for these bytes. The remark about passing `password: null` to Core was replaced by one
  about the handle.
- `src/Enigma.Licensing/Enigma.Licensing.csproj` — the dependency comment: version, plus one sentence
  on the handle. `<PackageReleaseNotes>` names no dependency version and needed no edit.
- `CLAUDE.md` — the Core version in three places, the test counts (1164 → 1174, 866 → 876, ten RSA
  pairs → eleven), the `Keys/` suite and `LicensingKey.cs` layout lines, a new *Shipped* section
  (*Enigma.Core 2.0.0 & the RSA key handle*), a *Current state* paragraph, the build/test section's new
  note about the missing .NET 8 runtime, and the dev-workflow section's claim that the roadmap has no
  `TODO` row left — which the planning commit had just made false.
- `README.md`, `RELEASENOTES.md`, `docs/RELEASE.md` — "Enigma.Core 1.1.0" → 2.0.0 (the prose line, the
  dependency bullet, and both rows of the per-TFM dependency-floor table).
- `docs/roadmap.md`, `docs/plan/FEATURE-6C54.md` — statuses.

**Created**

- `tests/Enigma.Licensing.UnitTests/Keys/KeyHandleTests.cs` — five cases over `RsaHandle`, on the
  shared `GeneratedKeyFixture`: the same instance on a second read (the observable form of "imported
  once"), each half carrying only its own material (`HasPrivateKey` false on the public half, and the
  two handles not the same object), the generated modulus size reported back, and eight threads racing
  for a fresh key's handle all receiving one instance.

**Deleted** — nothing.

## Deviations & follow-ups

- **No deviation from the plan.** Every step landed as written, including the two "does not apply"
  findings the plan predicted: no certificate call site exists, and no bad-passphrase `catch` had to
  move, because an `enigma-key/1` file never holds a PEM that is encrypted at the PEM layer.
- **The `KeyMaterial` PEM-layer guard is now double-covered, and that is fine.** `KeyMaterial.Validate`
  still rejects an `ENCRYPTED PRIVATE KEY` / `Proc-Type: 4,ENCRYPTED` document at load, and
  `RsaKey.ImportPrivateKeyPem` would now also refuse it (with a `CryptographicException`) at first use.
  The load-time check is kept: it fails at the moment the file is read, which is the failure a user can
  act on.
- **Follow-up — the .NET 8 runtime is not installed on this machine.** `dotnet test --solution` cannot
  launch the library test project's net8.0 executable and reports `error: 1` with a non-zero exit code
  even though every test that ran passed. It is a missing runtime, not a red suite, and it predates this
  dev; the recorded workaround (`DOTNET_ROLL_FORWARD=LatestMajor` on the net8.0 binaries) is how that
  leg was verified here. Installing the .NET 8 runtime restores a clean single-command run.
- **Follow-up, not adopted here — `IMLDsaPemService`.** 2.0.0 can write ML-DSA keys as PEM, which would
  let `enigma-key/1` carry one encoding for its `key` field instead of two. Worth its own work item if
  the format is ever revised; declined now because nothing on disk needs to move.
- **Line endings:** the working tree is LF throughout and this dev introduced no CRLF churn
  (recommendation-only, per the workflow — no action taken).

## Build/test evidence

| Check | Result |
|---|---|
| `dotnet build Enigma.Licensing.slnx -c Release` | **Build succeeded — 0 Error(s), 0 Warning(s)** (netstandard2.0, net8.0, net10.0, desktop, both test projects) |
| Desktop `AVLN` count (`--no-incremental -v normal \| grep -c AVLN`) | **0** |
| `dotnet test --solution Enigma.Licensing.slnx -c Release` | **736 / 736 passed** (library net10.0 438, desktop 298); `error: 1` is the net8.0 launcher, see below |
| net8.0 leg, `DOTNET_ROLL_FORWARD=LatestMajor ./Enigma.Licensing.UnitTests.exe` | **Total: 438, Errors: 0, Failed: 0, Skipped: 0** |
| Total | **1174 cases, 0 failures** |
| Restore | clean — `Enigma.Core` 2.0.0 resolved on all three TFMs, no new transitive package, no NU1510/NU1901-1904 |

The first build after the pin bump produced exactly the nine predicted errors — `GenerateRsaKeyPair`
(CS1061), `Sign`'s `password` argument (CS1739) and `Verify`'s PEM string (CS1503), once per TFM — and
the fix cycle after them was the only one needed.

Migration-specific assertions that already existed and still pass, which is what proves the file
formats did not move: `KeyGenerationTests.GenerateKeyPair_ProducesPemMaterialForTheRsaAlgorithms`
(`-----BEGIN PUBLIC KEY-----` / `-----BEGIN PRIVATE KEY-----`),
`GenerateKeyPair_LeavesTheRsaPrivatePemUnencrypted`, `KeyStorageTests`' PEM-layer-encrypted rejection
rows, the six-algorithm save → load → validate round-trip theory, the `algorithm`-field tamper test,
and both arms of `PublicSurfaceIsolationTests` — the second of which would have failed the moment
`RsaKey` reached an exported member, which is exactly why `RsaHandle` is internal.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Property name for the handle | `RsaHandle`, not `RsaKey` | A property named after its own type compiles (C#'s "Colour Colour" rule) but reads as a puzzle at the one place it matters — `RsaKey.ImportPublicKeyPem` inside the accessor of a property called `RsaKey` |
| Keep `LicensingKey.Pem`? | Kept | It is what the handle imports from, and the generation tests assert the stored PEM's shape through it; deleting it would have traded a two-line member for a test rewrite that asserted less |
| Read the field with `Volatile.Read` before the fast path? | Yes | The fast path is a plain read on every later call; making it explicit costs nothing and states that the field is written by another thread |
| Where to assert the cache | A new `KeyHandleTests`, on the shared fixture | The key suites are organised by concern (generation / storage / encryption), and "what a key does with its material" is a fourth |
| The racing-readers test generates its own key | Yes, one RSA-2048 pair | The fixture's pair may already have been imported by an earlier test in the collection, so a first read has to be a genuinely first read. It is the eleventh RSA pair in the suite, and the counts in `CLAUDE.md` were updated with it |
| `CLAUDE.md`'s "no `TODO` row left" line | Rewritten | The planning commit made it false, and the sweep's job is exactly the statements a dev's diff invalidates |
| A `<PackageReleaseNotes>` edit | None | It describes the library's features and names no dependency version, so there was nothing to keep in step with `RELEASENOTES.md` |
| The five `docs/guides/` files | Untouched | They document behaviour, and no behaviour changed. A version bump in a guide would have been noise |
