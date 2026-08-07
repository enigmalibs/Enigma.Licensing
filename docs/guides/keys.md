# Keys

Licenses are signed with a key pair, and `IKeyService` is where pairs come from. You create a
`KeyService`, ask it for a pair in the scheme you want, and write both halves out as
`enigma-key/1` documents — one file per half, both carrying the same fingerprint.

The workflow is asymmetric on purpose. An issuer generates a pair **once**, keeps the private half
(encrypted, ideally) on the machine or build server that signs licenses, and ships the **public**
half inside the application that will check them. Everything else in this library takes its keys
from here: [the generator](generating-licenses.md) signs with the private half,
[the validator](validating-licenses.md) verifies with the public one.

**Passwords are `char[]`, and the caller owns them.** Nothing in this library modifies or clears an
array you hand it; the copies made along the way — the UTF-8 encoding, the derived AES key — are
cleared internally, but the characters you passed in remain yours to overwrite when you are done.
That is the Enigma.Core convention, and it exists because only the caller knows when the password
stops being needed.

## Supported algorithms

| `LicenseSignatureAlgorithm` | Key material | Notes |
|---|---|---|
| `RsaSha256` | RSA, `rsaKeySizeBits` | PKCS#1 v1.5. The general-purpose choice — widest interoperability, and the digest matters less than the modulus. |
| `RsaSha384` | RSA, `rsaKeySizeBits` | Same keys, stronger digest. |
| `RsaSha512` | RSA, `rsaKeySizeBits` | Same keys, stronger digest again. |
| `MLDsa44` | FIPS 204, fixed | Post-quantum. Smallest parameter set; use when signature size dominates. |
| `MLDsa65` | FIPS 204, fixed | **Recommended post-quantum choice** — the balance point, and the app's default. |
| `MLDsa87` | FIPS 204, fixed | Largest parameter set; largest keys and signatures. |

`rsaKeySizeBits` accepts **2048–8192** and defaults to **3072** — roughly 128-bit security, and what
NIST expects of keys still in use after 2030. Anything outside that range throws
`ArgumentOutOfRangeException`, as does an `algorithm` that is not a defined enum member. The
parameter is **ignored** for the ML-DSA members, whose key sizes are fixed by the parameter set the
algorithm names.

RSA key generation is a search for primes and takes noticeably longer as the modulus grows — seconds
at 4096 bits and up, long enough to matter on a UI thread. The ML-DSA members are fast at every
parameter set.

## The document

One format for both algorithm families and both halves of a pair:

```json
{
  "format": "enigma-key/1",
  "kind": "private",
  "algorithm": "ML-DSA-65",
  "keyId": "378A86040C38330F",
  "encryption": {
    "kdf": "argon2id", "iterations": 3, "memoryKb": 65536, "parallelism": 4,
    "salt": "irYyS9T16CHRZtOqGcFk0A==",
    "cipher": "AES-256-GCM", "nonce": "WsgxIPgp59l8GwTe", "tagBits": 128
  },
  "key": "YFzqD7f20lqz1KnA5tT7JqwjDl8vR/pyDwjnchIV5FiH…"
}
```

Six fields, of which `encryption` is the only optional one — present exactly when the key material
is password-encrypted, which only a private key ever is.

**The `key` field has three encodings, chosen by the document's own fields rather than guessed from
the content:**

| Document says | `key` holds |
|---|---|
| `encryption` present | base64 of the AES-256-GCM ciphertext, whatever the algorithm |
| RSA, unencrypted | PEM text — SPKI for a public key, PKCS#8 for a private one |
| ML-DSA, unencrypted | base64 of the raw FIPS 204 encoding (private keys in expanded form) |

Unlike a license envelope, an **unknown `algorithm` in a key file is fatal**: there is no verdict to
report about a key, only a key that cannot be used.

### Password encryption

Private keys are optionally encrypted at this envelope layer, uniformly across both algorithm
families. Argon2id (RFC 9106's second recommended cost set — 3 iterations, 64 MiB, 4 lanes, all
recorded in `encryption`) derives an AES-256-GCM key; the 128-bit tag is appended to the ciphertext.

The RSA PEM is therefore always generated **unencrypted** at the PEM layer — PEM-level password
support is deliberately unused, because ML-DSA has no equivalent and two schemes would mean two sets
of failure modes. A PEM that *is* encrypted at the PEM layer is rejected at load.

**Public keys are never encrypted.** Supplying a password for one is an `ArgumentException`, and a
public document carrying an `encryption` object is malformed. KDF costs read from a file are bounded
(64 iterations, 1 GiB, 64 lanes) and checked *before* the password is, so a hostile file cannot
exhaust memory unauthenticated.

### Key identifiers

`keyId` is the uppercase hex of the first 8 bytes of SHA-256 over the algorithm identifier
concatenated with the public key material. It is computed from the **public** half at generation and
written into both files, so a pair shares one value, and it is read back verbatim rather than
recomputed. The generator stamps it into every license envelope it signs, which is how an
application shipping several public keys can tell which one a license claims to need.

It is an identifier, not a security control.

## Key types

| Type | Namespace | Role |
|---|---|---|
| `KeyService` | `Enigma.Licensing.Keys` | The implementation. Sealed, stateless, public parameterless constructor. |
| `IKeyService` | `Enigma.Licensing.Keys` | The interface. DI-friendly; registered by `AddEnigmaLicensing()`. |
| `LicensingKeyPair` | `Enigma.Licensing.Keys` | Both halves: `PublicKey` and `PrivateKey`. |
| `LicensingKey` | `Enigma.Licensing.Keys` | One parsed key — `Kind`, `Algorithm`, `KeyId`. The material itself stays internal. |
| `LicensingKey.CurrentFormat` | `Enigma.Licensing.Keys` | The one key format version: `"enigma-key/1"`. |
| `KeyKind` | `Enigma.Licensing.Keys` | `Public` or `Private`. |
| `LicenseSignatureAlgorithm` | `Enigma.Licensing` | The six schemes. |

`IKeyService` exposes five members:

```csharp
LicensingKeyPair GenerateKeyPair(
    LicenseSignatureAlgorithm algorithm,
    int rsaKeySizeBits = 3072);

Task SaveKeyAsync(
    LicensingKey key,
    Stream output,
    char[]? password = null,
    CancellationToken cancellationToken = default);

Task<LicensingKey> LoadKeyAsync(
    Stream input,
    char[]? password = null,
    CancellationToken cancellationToken = default);

Task SaveKeyPairAsync(
    LicensingKeyPair pair,
    string publicKeyPath,
    string privateKeyPath,
    char[]? password = null,
    CancellationToken cancellationToken = default);

Task<LicensingKey> LoadKeyAsync(
    string path,
    char[]? password = null,
    CancellationToken cancellationToken = default);
```

Note the asymmetry: writing **by path** is `SaveKeyPairAsync`, which writes both halves and creates
any missing parent directories. Writing a single key means opening the stream yourself. Reading has
both a path and a stream overload, because reading one key at a time is the normal case.

`LicensingKey` carries no key material on its public surface — only `Kind`, `Algorithm` and `KeyId`.
That is what keeps the PEM-versus-raw difference from reaching callers, and the only way to obtain
one is through `IKeyService`, so every instance has either just been generated or just come out of a
parsed document.

`KeyService` is constructed with `new`, and `IKeyService` is DI-friendly: `AddEnigmaLicensing()`
registers it as a singleton (see [Validating licenses](validating-licenses.md#dependency-injection)).

## Usage

### Generating a pair and writing both halves

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);

await keyService.SaveKeyPairAsync(
    pair,
    publicKeyPath: "keys/signing.public.key.json",
    privateKeyPath: "keys/signing.private.key.json");

Console.WriteLine($"Key {pair.PublicKey.KeyId} ({pair.PublicKey.Algorithm})");
```

Both halves carry the same `KeyId`. The `keys/` directory is created if it does not exist. Existing
files are overwritten, and the public half is written first — so a failure part-way leaves a public
key file behind, which is harmless on its own; regenerate the pair rather than repairing it.

The suffixes are a convention, not a requirement: the library reads a key file by its contents, not
its name.

### Encrypting the private half

Supply a password and it is applied to the private file only — never to the public one.

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);

char[] password = "correct horse battery staple".ToCharArray();

try
{
    await keyService.SaveKeyPairAsync(
        pair,
        publicKeyPath: "keys/signing.public.key.json",
        privateKeyPath: "keys/signing.private.key.json",
        password: password);
}
finally
{
    Array.Clear(password, 0, password.Length);
}
```

Clearing the array is the caller's job, and the `finally` is the point: it runs whether the write
succeeded or threw. An **empty** array is an `ArgumentException` rather than "no password" — pass
`null` for that.

### Choosing RSA and a modulus size

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

// 3072 is the default; an issuing key is generated once, so paying for 4096 is usually worth it.
LicensingKeyPair pair = keyService.GenerateKeyPair(
    LicenseSignatureAlgorithm.RsaSha256,
    rsaKeySizeBits: 4096);

await keyService.SaveKeyPairAsync(
    pair,
    publicKeyPath: "keys/rsa.public.key.json",
    privateKeyPath: "keys/rsa.private.key.json");

Console.WriteLine($"{pair.PrivateKey.Algorithm} / {pair.PrivateKey.Kind}");
```

This call blocks while it searches for primes. In a UI application, run it on a background thread.

### Loading a key

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

// The public half is never encrypted — no password, ever.
LicensingKey publicKey = await keyService.LoadKeyAsync("keys/signing.public.key.json");

Console.WriteLine($"{publicKey.KeyId} — {publicKey.Algorithm}, {publicKey.Kind}");
```

For an encrypted private key, pass the password and clear it afterwards:

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

char[] password = "correct horse battery staple".ToCharArray();
LicensingKey privateKey;

try
{
    privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json", password);
}
finally
{
    Array.Clear(password, 0, password.Length);
}

Console.WriteLine($"Ready to sign with {privateKey.KeyId}.");
```

A password supplied for a key that is *not* encrypted is ignored; a password supplied for a **public**
key is an `ArgumentException`.

### Writing one key to a stream

There is no single-key path overload, so open the stream yourself. The stream is flushed but never
closed — ownership stays with you.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);

using (FileStream output = File.Create("keys/extra.public.key.json"))
{
    await keyService.SaveKeyAsync(pair.PublicKey, output);
}

// Reading back through a stream works the same way.
using FileStream input = File.OpenRead("keys/extra.public.key.json");
LicensingKey reloaded = await keyService.LoadKeyAsync(input);

Console.WriteLine(reloaded.KeyId == pair.PublicKey.KeyId);  // True
```

The stream overloads are what to reach for when a key lives somewhere other than the file system — a
secret store, an embedded resource, an HTTP response:

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

Assembly assembly = Assembly.GetExecutingAssembly();

using Stream? embedded = assembly.GetManifestResourceStream("MyApp.signing.public.key.json");

if (embedded is null)
{
    throw new InvalidOperationException("The public key is missing from the application.");
}

LicensingKey publicKey = await keyService.LoadKeyAsync(embedded);

Console.WriteLine($"Verifying against {publicKey.KeyId}.");
```

### Cancellation

Every asynchronous member takes a `CancellationToken`. Generation itself is synchronous and cannot
be cancelled — it is CPU work, not I/O.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();

LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await keyService.SaveKeyPairAsync(
    pair,
    publicKeyPath: "keys/signing.public.key.json",
    privateKeyPath: "keys/signing.private.key.json",
    password: null,
    cancellationToken: cts.Token);
```

## Notes

- **Error contract.** `ArgumentNullException` and `ArgumentException` report caller mistakes,
  including a document that is not a well-formed key file of a known format, kind and algorithm.
  `CryptographicException` reports the cryptographic failures: a wrong or missing password, a
  modified file, key material that is not usable, out-of-range KDF parameters. `FileNotFoundException`
  and other I/O failures propagate untouched.
- **A key belongs to one scheme for life.** The algorithm is fixed at generation and cannot be
  changed afterwards — switching schemes means a new pair, and re-issuing the licenses signed by the
  old one.
- **Ship the public half, guard the private one.** An application that ships the private key ships
  the ability to mint its own licenses; the validator refuses a private key outright for exactly
  that reason.
- **There is no AAD.** GCM authenticates the key material; the surrounding fields are not covered.
  Editing them can only make the key fail to load or produce signatures that do not verify, and
  adding AAD would mean building a byte string out of metadata — the pattern this library avoids
  everywhere.
- **No compatibility with Enigma.LicenseManager keys.** The format is new, and no reader or converter
  for the predecessor's keys exists or will be added.
