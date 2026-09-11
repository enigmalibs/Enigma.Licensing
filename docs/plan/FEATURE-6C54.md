# FEATURE-6C54 — Enigma.Core 2.0 migration

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-6c54-enigma-core-2`
**Run:** vibe/2026-09-11-core-2-centering
**Depends on:** FEATURE-57BC complete (nothing published yet — see *Version* below)

## Objective

Move this repository's `Enigma.Core` reference from **1.1.0 to 2.0.0** and adopt the new API shape,
which is the reusable `RsaKey` handle in place of PEM strings. The library's own behaviour — the two
document formats, the six algorithms, the validation order, the error contract — must come out of this
**unchanged**, because nothing in 2.0.0 changes what `enigma-key/1` or `enigma-license/1` look like on
disk.

## Context & constraints

`Enigma.Core` 2.0.0 is published on nuget.org. Its dependency graph is *identical* to 1.1.0's —
`BouncyCastle.Cryptography` 2.7.0 on all three TFMs plus `System.Buffers` 4.6.1 on netstandard2.0 —
it is MIT, and it multi-targets `netstandard2.0;net8.0;net10.0` exactly as before. Verified against
the published nuspec, 2026-09-11.

The sibling repository carries the authoritative migration instructions at
`../Enigma.Core/docs/migrations/1.1.0-to-2.0.0.md`. In summary:

- **One breaking theme: RSA key material no longer crosses the public surface as PEM text.** A PEM is
  parsed once into `Enigma.Core.Asymmetric.PublicKey.RsaKey`, and that handle goes to every RSA call.
  The per-call `char[]? password` parameters are gone with it.
- Removed members were **deleted outright** — no `[Obsolete]`, no shims — so 1.x call sites fail to
  compile. The build error list is the to-do list.
- `GenerateRsaKeyPair(int, char[]?)` → `GenerateRsaKey(int)` returning one handle carrying both halves;
  `RsaKey.ExportPublicKeyPem()` / `ExportPrivateKeyPem(char[]?)` serialize it.
  `Sign(byte[], RsaKey, RsaSignatureAlgorithm)` and `Verify(byte[], byte[], RsaKey, RsaSignatureAlgorithm)`
  replace their PEM-string predecessors.
- **File compatibility is total.** `ExportPrivateKeyPem(null)` still writes unencrypted PKCS#8
  (`PRIVATE KEY`), `ExportPublicKeyPem()` still writes SPKI (`PUBLIC KEY`), and `ImportPrivateKeyPem`
  reads everything 1.x wrote. The PBES2 change concerns PEM-level passwords, which this library
  deliberately never uses.
- Everything else is untouched: Argon2id (`KeyMaterialCipher`), AES-256-GCM, SHA-256 and hex
  (`KeyFingerprint`), and the whole ML-DSA operation surface (`IMLDsaServiceFactory`/`IMLDsaService`).

Three files in this repository name a broken member. Nothing else in `src/` does:

| File | What breaks |
|---|---|
| `src/Enigma.Licensing/Keys/KeyService.cs` | `_rsaService.GenerateRsaKeyPair(rsaKeySizeBits)` returning `(string, string)` |
| `src/Enigma.Licensing/Internal/LicenseSignatures.cs` | `Sign(payload, pem, algorithm, password: null)` and `Verify(payload, signature, pem, algorithm)` |
| `src/Enigma.Licensing/Keys/LicensingKey.cs` | nothing yet — this is where the handle comes to live |

The **desktop app never calls Enigma.Core directly**. It picks Core up transitively through
`Enigma.Avalonia.Desktop` 1.0.0 (floor `>= 1.0.0`), whose assembly references `Enigma.Core.Encoding`
and *nothing* from the RSA or certificate surface — verified by scanning
`Enigma.Avalonia.Desktop.dll` for `IPublicKeyService`, `PublicKeyServiceFactory`, `RsaKey`,
`GenerateRsaKeyPair` and `X509Certificate` (zero hits each). NuGet unifies the graph on the 2.0.0 the
library pins, so the app keeps one Enigma.Core and one BouncyCastle.

**Version.** The library stays at **1.0.0**. `FEATURE-57BC` wrote the release documentation but
*nothing outward-facing has happened*: no tag, nothing pushed, nothing on nuget.org. So this is not a
change to a released package — it is a change to what 1.0.0 will be, and the dependency line in
`RELEASENOTES.md` / `<PackageReleaseNotes>` / `README.md` is updated in place rather than a 1.0.1
section being opened.

## Decisions

1. **`LicensingKey` owns a lazily imported, cached `RsaKey`.** The whole point of 2.0.0 is to stop
   re-parsing: roughly 84 % of a 1.x `Sign(data, pem, password)` call was key reconstruction, because
   BouncyCastle validates the CRT components on construction. A migration that imports a handle
   immediately before each call compiles and throws the release away. `LicensingKey` is the natural
   owner — one instance *is* one key, created either by generation or by a parsed `enigma-key/1`
   document — so "import once per key, per lifetime" becomes structural rather than a rule to remember.
   - The import must be **race-safe**: `LicenseStore` snapshots under its lock and verifies *outside*
     it, so one public `LicensingKey` can be verified with from several threads at once. A null-checked
     `Interlocked.CompareExchange` on a backing field is enough — `RsaKey` is immutable and
     thread-safe, so losing the race merely discards a redundant handle. (`Lazy<T>` would allocate and
     capture; the field pattern works identically on netstandard2.0.)
   - The handle is **not** seeded from generation. `GenerateRsaKey` hands back a private handle, and
     putting it on the *public* half — where it would carry private material that must not be able to
     sign — is exactly the confusion this library avoids elsewhere. Generation is dominated by key
     generation itself, so one lazy path for both halves is simpler and costs nothing measurable.
2. **`enigma-key/1` does not change.** 2.0.0 adds `IMLDsaPemService`, which could put ML-DSA keys in
   PEM too and collapse the format's two `key` encodings into one. It is declined: the format is
   documented in five guides and in the release notes, nothing on disk needs to move, and permission to
   break a format is not a reason to break one. Noted as a possible future item, not adopted here.
3. **`KeyMaterial.Validate` stays structural.** A real parse is now cheap to reach for, but making it
   the load-time check would move error semantics (an `ArgumentException` from the importer versus this
   library's documented `CryptographicException`) and pay for a parse the caller may never need. A
   genuinely unusable key still fails at first use, which is where the generator and validator already
   report it.
4. **No explicit `Enigma.Core` pin in the desktop csproj.** The project reference to the library
   already brings 2.0.0; adding a second declaration of the same version in a second place is how the
   two drift apart.
5. **The Checksum module and the ML-KEM PEM services are out of scope** — additive, and this repository
   hand-rolls no equivalent.
6. **Migration guide steps that do not apply here, recorded so the next reader does not hunt for them:**
   Step 3 (certificates) — no certificate call site exists. Step 5 (bad-passphrase handling moves to
   the import) — no PEM in an `enigma-key/1` file is ever encrypted at the PEM layer, so no `catch` site
   moves; the password this library takes protects the *file*, through Argon2id + AES-256-GCM, and that
   path is untouched by 2.0.0.

## Steps

1. **Bump the pin.** `Directory.Packages.props`: `Enigma.Core` `1.1.0` → `2.0.0`, and update the
   comment block above it (the netstandard2.0 `System.Buffers` note still holds — 2.0.0 asks for the
   same 4.6.1 — and the desktop group's "the graph closes on the 1.1.0 the library pins" line becomes
   2.0.0). Restore, then build to enumerate the breaks.
2. **`LicensingKey` gains the handle.** Add an internal `RsaKey` member that imports the key's own
   material once — `RsaKey.ImportPublicKeyPem` for `KeyKind.Public`, `RsaKey.ImportPrivateKeyPem` for
   `KeyKind.Private` — caches it with `Interlocked.CompareExchange`, and documents why it exists
   (import once; immutable; meaningless for the ML-DSA members, whose material is raw FIPS 204 bytes).
   Keep `Material`. Keep `Pem` only if something still reads it, and say what for.
3. **`LicenseSignatures` takes handles.** `Sign` passes the private key's handle;
   `Verify` passes the public key's, **inside the existing `try`**, so an unparseable key stays a
   `false` verdict rather than an exception — the fail-closed contract is load-bearing. Drop the
   `password: null` argument and the remark that explained it.
4. **`KeyService` generates a handle.** `GenerateRsaKey(rsaKeySizeBits)` once, then
   `ExportPublicKeyPem()` / `ExportPrivateKeyPem()` for the two material buffers the format stores.
   The comment about writing the PEM unencrypted stays true and still earns its place.
5. **Sweep for anything else naming a removed member** (`grep` for `GenerateRsaKeyPair`,
   `EncryptPkcs1`, `DecryptOaep`, `ImportPkcs12`, `privateKeyPem`, …) across `src/` and `tests/`, so the
   migration is finished rather than merely compiling.
6. **One new test** in `tests/Enigma.Licensing.UnitTests/Keys/`: the cached handle is the *same
   instance* across two reads of the same `LicensingKey` (`Assert.Same`), which is the only externally
   observable form of "imported once" and pins decision 1 against a future refactor that quietly
   re-imports per call. The rest of the migration is proved by the existing suite, every RSA case of
   which now runs through the new surface.
7. **Build and test the whole solution** (Release, zero warnings, `--solution`), and check the desktop
   app's `AVLN` count is still 0 — the app builds against the same unified graph.
8. **Documentation sweep** (auto-applying, this dev's own commit): `CLAUDE.md` (the Core version, the
   dependency section, the desktop transitive-graph note, and a *Shipped* entry for this work),
   `README.md` ("built on Enigma.Core 1.1.0"), `RELEASENOTES.md` and the csproj's
   `<PackageReleaseNotes>` (kept in step with each other, per the house rule), `docs/RELEASE.md`'s
   per-TFM dependency table, and the csproj / `Directory.Packages.props` comments. The five
   `docs/guides/` files describe behaviour that did not change and are **not** touched.

## Acceptance criteria

1. `Directory.Packages.props` pins `Enigma.Core` 2.0.0; restore is clean; no `Version=` attribute
   appears on any `<PackageReference>`.
2. `DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release` succeeds with **zero
   warnings** on every target framework, and the desktop project's `AVLN` count is 0.
3. `DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release` is green — all
   1164 existing cases plus the new one. In particular: the six-algorithm round-trip theory, the
   tamper tests, `KeyGenerationTests`' assertions that the generated public material starts
   `-----BEGIN PUBLIC KEY-----` and the private `-----BEGIN PRIVATE KEY-----` with no `ENCRYPTED`,
   and `PublicSurfaceIsolationTests` (both arms — `RsaKey` must not reach the exported surface).
4. No `RsaKey.Import*` call sits in a per-operation path: signing or verifying N times with one
   `LicensingKey` parses the PEM once, asserted by the new test.
5. No wrapper, extension or helper re-creates a removed PEM-string overload, and no file in `src/`
   still names one.
6. `enigma-key/1` and `enigma-license/1` are unchanged: a key file written before this dev loads after
   it, and a licence signed before it still validates. (Proved by the fixtures the suite writes and
   re-reads, and by the format assertions in criterion 3.)
7. The library version is still 1.0.0, and every document that named Enigma.Core 1.1.0 now names 2.0.0
   — with `RELEASENOTES.md` and `<PackageReleaseNotes>` saying the same thing.

## Out of scope

- The Checksum module, `IMLKemPemService`, and `IMLDsaPemService` (additive; nothing here needs them).
- Any change to `enigma-key/1`, `enigma-license/1`, the algorithm set, RSA key sizes or the digest
  choices — this is an API migration, not a crypto review.
- Bumping the library's own version, tagging, packing or pushing (`docs/RELEASE.md` is the maintainer's).
- `Enigma.Avalonia.Desktop` and the Avalonia version-coupled group.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where does the parsed `RsaKey` live? | On `LicensingKey`, lazily and cached | One instance is one key, so "import once per key" becomes structural; 84 % of a 1.x sign call was reconstruction | Import at each call site (compiles, discards the release's whole point); a `ConditionalWeakTable` cache in `LicenseSignatures` (same effect, more machinery, no owner) |
| How is the lazy import made thread-safe? | Null-checked `Interlocked.CompareExchange` on a field | `LicenseStore` verifies outside its lock; `RsaKey` is immutable, so a lost race just discards a handle | `Lazy<T>` (allocation + closure for no benefit); a lock (serializes verification); no synchronisation (torn double-import is harmless but undisciplined) |
| Seed the handle from `GenerateRsaKey`? | No | The generated handle is private; parking it on the public half would let a "public" key sign | Seeding both halves from one handle (blurs the kinds); seeding the private half only (two construction paths for one field) |
| Adopt 2.0.0's ML-DSA PEM services and unify the key format? | No | Nothing on disk needs to move; the format is documented in five guides and the release notes | Unify `key` to PEM for both families (a format break with no beneficiary) |
| Make `KeyMaterial.Validate` a real parse? | No | Moves the documented exception type and pays for a parse that may never be needed | Parse at load (error-contract change); parse only for private keys (two rules) |
| Bump to 1.0.1 / open a new release-notes section? | No — 1.0.0 stands, in place | Nothing was ever tagged, packed or pushed, so there is no released 1.0.0 to amend | A 1.0.1 section (claims a release that never happened) |
| Explicit `Enigma.Core` pin in the desktop csproj? | No | The project reference already brings 2.0.0; a second declaration is a second thing to keep in step | Pin it (drift); `ExcludeAssets` games on the transitive reference (nothing to fix) |
| How much test coverage for the migration? | The existing suite, plus one `Assert.Same` on the cached handle | Every RSA case already exercises the new surface; the cache is the one new invariant that a refactor could silently undo | New RSA round-trip tests (duplicates the theory); no new test at all (leaves decision 1 unguarded) |
