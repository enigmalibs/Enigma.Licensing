# The license format

Enigma.Licensing writes licenses as `enigma-license/1` documents — a small JSON object wrapping the
signed license bytes and the signature over them. `LicenseEnvelope` is that document as a type: you
get one back from `ILicenseGenerator`, you read one with `LicenseEnvelope.LoadAsync`, and you hand
one to `ILicenseValidator` to find out whether it is authentic.

Everything else in the library is built on one property of this format: **the signature covers the
payload bytes exactly as stored, and nothing ever recomputes them.** `Payload` is the UTF-8 JSON of
a `License`; `Signature` is the signature over precisely those bytes. Verification decodes the
base64 and checks the signature against the result — it never re-serializes a license to work out
what "should" have been signed. There is deliberately no canonicalization step, and therefore no
canonicalization step to get wrong.

## The document

```json
{
  "format": "enigma-license/1",
  "algorithm": "RSA-SHA256",
  "keyId": "3F9A1C40D2B87E56",
  "payload": "eyJpZCI6IjAxSllRN1pLM00…",
  "signature": "cHJldGVuZC1zaWduYXR1cmU…"
}
```

Five fields, in that order. `keyId` is omitted when there is none; `payload` and `signature` are
base64. The document is indented, camelCase, and read back case-sensitively — no comments, no
trailing commas. Fields the format does not define are **ignored** rather than rejected: they fall
outside the signature either way, so refusing them would protect nothing while ruling out a future
optional field within this same format version.

| Field | Type | Meaning |
|---|---|---|
| `format` | string | Always `enigma-license/1`. Checked before anything else happens. |
| `algorithm` | string | The wire identifier of the signature scheme — this is what *selects the verifier*. |
| `keyId` | string, optional | Fingerprint of the signing pair, so an application shipping several public keys can pick one. A hint, never a security control. |
| `payload` | base64 | The signed bytes: the compact UTF-8 JSON of a `License`. |
| `signature` | base64 | The signature over `payload`, under `algorithm`. |

### The payload

Decoded, `payload` is a `License` — the only part of the document a signature protects:

```json
{
  "id": "01JYQ7ZK3M8XVB4T2N6P0RWDCF",
  "createdAt": "2026-08-07T09:14:22.1043117+00:00",
  "productId": "MyApp 2.*",
  "deviceId": "9F2C4A…",
  "validFrom": "2026-08-07T00:00:00+02:00",
  "validUntil": "2027-08-07T00:00:00+02:00",
  "owner": "Acme GmbH"
}
```

`id`, `createdAt` and `productId` are always present; everything else is omitted when unset, so an
absent field and an explicit `null` mean the same thing. Dates are ISO-8601 with an explicit offset,
so a license issued in one time zone means the same instant everywhere. See
[Generating licenses](generating-licenses.md) for what each field does.

**A payload member this library does not know is ignored, not rejected.** That is what keeps a
license issued by 1.0 — whose payload carries `features` and `claims`, two fields 2.0 removed —
valid under 2.0: nothing re-serializes a license in order to check one, so the signature still covers
exactly the bytes it was made over, and the unknown members simply do not surface on `License`.

## Algorithm identifiers

`algorithm` is a `string` on the envelope rather than a `LicenseSignatureAlgorithm`, because a
document read from disk may name a scheme this library does not implement — and that has to be
representable in order to be *reported* as a validation failure rather than crashing the parse.

| `LicenseSignatureAlgorithm` | Wire identifier | Notes |
|---|---|---|
| `RsaSha256` | `RSA-SHA256` | PKCS#1 v1.5. The general-purpose choice; widest compatibility. |
| `RsaSha384` | `RSA-SHA384` | As above, stronger digest. |
| `RsaSha512` | `RSA-SHA512` | As above, stronger digest again. |
| `MLDsa44` | `ML-DSA-44` | FIPS 204 post-quantum. Smallest parameter set. |
| `MLDsa65` | `ML-DSA-65` | Recommended post-quantum choice — the balance point. |
| `MLDsa87` | `ML-DSA-87` | Largest parameter set, largest signatures. |

Matching is ordinal and case-sensitive in both directions. An identifier outside this table is not a
parse error — `LicenseEnvelope.LoadAsync` accepts it and the validator reports
`LicenseFailure.UnknownAlgorithm`.

### Why rewriting `algorithm` does not help an attacker

Neither `algorithm` nor `format` is inside the signed bytes, which invites the obvious question. The
answer is that `algorithm` is what *selects the verifier*: change it and the signature is checked
under a different scheme, which fails. Downgrading `RSA-SHA512` to `RSA-SHA256` means verifying a
SHA-512 PKCS#1 signature as a SHA-256 one, and the padded digest does not match. Swapping an RSA
identifier for an ML-DSA one means the key that would be needed is not the key the verifier was
given. For the ML-DSA members the parameter set is bound to the key material itself, so the
identifier is not even load-bearing there. `format` is checked against the one version this library
understands before any of that runs.

## Key types

| Type | Namespace | Role |
|---|---|---|
| `LicenseEnvelope` | `Enigma.Licensing` | The signed document. Immutable record; `LoadAsync`/`SaveAsync`/`ReadLicense`. |
| `LicenseEnvelope.CurrentFormat` | `Enigma.Licensing` | The one format version this library reads or writes: `"enigma-license/1"`. |
| `License` | `Enigma.Licensing` | What the payload decodes to — the license data itself. |
| `LicenseSignatureAlgorithm` | `Enigma.Licensing` | The six schemes, as an enum. |

`LicenseEnvelope` exposes:

```csharp
public const string CurrentFormat = "enigma-license/1";

public License? ReadLicense();

public Task SaveAsync(
    Stream output,
    CancellationToken cancellationToken = default);

public static Task<LicenseEnvelope> LoadAsync(
    Stream input,
    CancellationToken cancellationToken = default);
```

Both stream methods leave the stream **open** — `SaveAsync` flushes but never closes, `LoadAsync`
reads to the end but never closes — so the caller keeps ownership of it.

## Usage

### Reading a license document

`LoadAsync` is a parser: a caller who asks for an envelope and gets one back is entitled to assume
it is structurally sound, so a document it cannot parse **throws**. That is the opposite of the
validator's contract, which never throws for a bad license.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;

using FileStream input = File.OpenRead("customer.license");
LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);

Console.WriteLine(envelope.Format);     // enigma-license/1
Console.WriteLine(envelope.Algorithm);  // RSA-SHA256
Console.WriteLine(envelope.KeyId);      // 3F9A1C40D2B87E56, or null
```

Parsing proves only that the document is well-formed and of a known format version: valid JSON, the
five fields present and typed correctly, both base64 blobs decoding, and `format` matching
`CurrentFormat`. It says nothing about whether the signature is good, whether the algorithm is one
this library implements, or whether the license inside is in date. Those are
[the validator's questions](validating-licenses.md).

### Looking inside without validating

`ReadLicense()` returns the `License` the payload describes, or `null` when those bytes are not a
readable license. It never throws — inspecting an untrusted document is not an error path.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;

using FileStream input = File.OpenRead("customer.license");
LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);

License? claimed = envelope.ReadLicense();

if (claimed is null)
{
    Console.WriteLine("The payload is not a readable license.");
}
else
{
    Console.WriteLine($"{claimed.Id} — {claimed.ProductId} for {claimed.Owner ?? "(no owner)"}");
}
```

**The result is unverified: safe to display, unsafe to act on.** It says what the document *claims*,
not whether the claim is authentic — exactly like `LicenseValidationResult.License` on a failed
result, and for the same reason. Use it to name a license you just issued, to show a support
engineer what a customer's file contains, or to fill in a form. Never to decide that a licence is
valid.

### Writing a document

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

char[] password = "correct horse battery staple".ToCharArray();
LicensingKey privateKey;

try
{
    privateKey = await keyService.LoadKeyAsync("signing.private.key.json", password);
}
finally
{
    Array.Clear(password, 0, password.Length);
}

LicenseEnvelope envelope = generator.Generate(
    new LicenseRequest { ProductId = "MyApp 2.0", Owner = "Acme GmbH" },
    privateKey);

using FileStream output = File.Create("customer.license");
await envelope.SaveAsync(output);
```

In practice you rarely write an envelope by hand: `ILicenseGenerator` has overloads that sign and
write in one call, including one that creates missing parent directories. See
[Generating licenses](generating-licenses.md).

### Seeing the signed bytes

`Payload` is the exact JSON that was signed. Decoding it is how you see what a signature actually
covers:

```csharp
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Enigma.Licensing;

using FileStream input = File.OpenRead("customer.license");
LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);

Console.WriteLine(Encoding.UTF8.GetString(envelope.Payload));
Console.WriteLine($"{envelope.Signature.Length} signature bytes");
```

### Handling a document that will not parse

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;

try
{
    using FileStream input = File.OpenRead("customer.license");
    LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);
    Console.WriteLine($"Loaded a {envelope.Algorithm} license.");
}
catch (ArgumentException ex)
{
    // Not JSON, missing a required field, base64 that does not decode, or an unknown format
    // version. The underlying JsonException is preserved as ex.InnerException.
    Console.WriteLine($"Not a license document: {ex.Message}");
}
catch (FileNotFoundException)
{
    Console.WriteLine("No license installed.");
}
```

If you would rather have a verdict than an exception for a malformed file, use
`ILicenseValidator.ValidateAsync` — it runs this same parse inside its own `try`/`catch` and reports
`LicenseFailure.InvalidFormat`.

## Notes

- **There is no reader for Enigma.LicenseManager licenses.** Enigma.Licensing is a clean break from
  its predecessor: the formats are new, and no converter, fallback or compatibility reader exists or
  will be added. The `format` field is there so a *future* Enigma.Licensing format can be introduced
  without ambiguity, not to support the past.
- **Never build a string to sign.** The predecessor's hand-rolled "data for signature" string was
  the source of its worst defect. Sign the payload bytes, verify the payload bytes; if you find
  yourself re-serializing a `License` in order to check one, something has gone wrong.
- **`keyId` is an identifier, not a security control.** It says which key *claims* to have signed the
  license. Only verifying the signature says whether one actually did — use `keyId` to choose
  between several shipped public keys, then let the validator settle it.
- **Serialization is source-generated**, so the library is trim- and AOT-safe: a consumer publishing
  with `PublishAot`, or with `JsonSerializerIsReflectionEnabledByDefault=false`, needs no extra
  configuration for these documents.
