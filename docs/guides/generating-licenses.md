# Generating licenses

`ILicenseGenerator` is the issuing half of the library: you describe the license you want as a
`LicenseRequest`, hand it a private key, and get back a signed `LicenseEnvelope`. This runs where
the private key lives — on the issuer's machine, in a build step, or behind a licensing service. It
never runs in the application that checks licenses, which only ever needs the public half and
[`ILicenseValidator`](validating-licenses.md).

**What the generator signs is what it stores.** The license is serialized to UTF-8 JSON once; those
bytes are signed, and those same bytes go into the envelope. Nothing recomputes them, so there is no
second opinion about what the license "was" for the signature to disagree with.

## Operations

| Member | Writes | Use it when |
|---|---|---|
| `Generate` | nothing — in memory | You need the envelope itself: to inspect it, hold it in a store, or send it over the wire. |
| `GenerateAndSaveAsync(request, key, stream, …)` | a stream you own | The destination is not a file, or you are already holding the key. |
| `GenerateAndSaveAsync(request, keyPath, password, outputPath, …)` | a file | The whole issue-a-license flow in one call, including creating missing parent directories. |

All three return the envelope that was signed, so you can read back the identifier that was issued.

**The path overload signs before it touches the output file**, so a rejected request leaves no
truncated license behind.

## What a request says

`LicenseRequest` is what you fill in; `License` is what ends up signed. Only `ProductId` is required.

| `LicenseRequest` member | Type | Meaning |
|---|---|---|
| `ProductId` | `string` (required) | The product the license is for. A `*` anywhere makes it a wildcard — see below. |
| `Owner` | `string?` | Who it was issued to. Purely descriptive; validation never looks at it. |
| `DeviceId` | `string?` | Lock the license to one machine. See [Device identity](device-identity.md). |
| `ValidFrom` | `DateTimeOffset?` | First instant it is usable. `null` means immediately. |
| `ValidUntil` | `DateTimeOffset?` | First instant it is **no longer** usable. `null` means perpetual. |
| `Id` | `string?` | Supply one to reproduce a specific license; omit for a fresh ULID. |
| `CreatedAt` | `DateTimeOffset?` | Supply one to reproduce a specific license; omit for `DateTimeOffset.UtcNow`. |

**The window is half-open: `[ValidFrom, ValidUntil)`.** A license is valid *at* `ValidFrom` and
**not** valid at `ValidUntil` — so "valid through 31 December" means `ValidUntil` is midnight at the
*start* of 1 January. Getting this backwards compiles perfectly and generates a support ticket every
December.

**Anything the request leaves unset that the license requires is filled in at the call**: `Id`
becomes a fresh ULID (lexicographically sortable by issue time, unlike a GUID) and `CreatedAt`
becomes `DateTimeOffset.UtcNow`. Both are stamped **per call**, so signing the same request twice
issues two licenses with different identifiers — which is what reissuing means.

### Product identifiers and wildcards

A `*` anywhere in the license's `ProductId` turns validation's comparison into an anchored wildcard
match: `"MyApp 2.*"` covers `"MyApp 2.0"` and `"MyApp 2.17"`. Every other character — including
regular-expression metacharacters like `.` — is matched **literally**, and the wildcard is honoured
only on the *license's* side; the identifier an application asks about is always a literal.

## Key types

| Type | Namespace | Role |
|---|---|---|
| `LicenseGenerator` | `Enigma.Licensing` | The implementation. Sealed, stateless, public parameterless constructor. |
| `ILicenseGenerator` | `Enigma.Licensing` | The interface. Registered by `AddEnigmaLicensing()`. |
| `LicenseRequest` | `Enigma.Licensing` | What the license should say. Immutable record; vary with `with`. |
| `LicenseEnvelope` | `Enigma.Licensing` | The signed document that comes back. |
| `LicensingKey` | `Enigma.Licensing.Keys` | The private half doing the signing. |

```csharp
LicenseEnvelope Generate(
    LicenseRequest request,
    LicensingKey privateKey);

Task<LicenseEnvelope> GenerateAndSaveAsync(
    LicenseRequest request,
    LicensingKey privateKey,
    Stream output,
    CancellationToken cancellationToken = default);

Task<LicenseEnvelope> GenerateAndSaveAsync(
    LicenseRequest request,
    string privateKeyPath,
    char[]? password,
    string outputPath,
    CancellationToken cancellationToken = default);
```

The private key's **algorithm decides the signature scheme** — there is no separate choice to make
at signing time — and its `KeyId` is stamped into the envelope so a validator can tell which public
key to reach for.

## Usage

### Issuing a license in one call

The path overload loads the key, signs, and writes the license, creating any missing parent
directories on the way.

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;

var generator = new LicenseGenerator();

char[] password = "correct horse battery staple".ToCharArray();
LicenseEnvelope envelope;

try
{
    envelope = await generator.GenerateAndSaveAsync(
        new LicenseRequest
        {
            ProductId = "MyApp 2.0",
            Owner = "Acme GmbH",
            ValidUntil = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        },
        privateKeyPath: "keys/signing.private.key.json",
        password: password,
        outputPath: "out/acme.license");
}
finally
{
    Array.Clear(password, 0, password.Length);
}

Console.WriteLine($"Issued {envelope.ReadLicense()?.Id} with key {envelope.KeyId}.");
```

Pass `null` for `password` when the key file is not encrypted.

`ReadLicense()` is how you find out which identifier was issued: the ULID is minted during signing
and exists nowhere but inside the signed bytes. It returns `null` only for a payload that is not a
readable license, which cannot happen for an envelope this call just produced — but a license on
disk that you cannot name is still a license on disk, so treat a `null` as a naming problem rather
than a failed issue.

### A validity window

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

LicensingKey privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json");

// A subscription that runs for one year from today, in the customer's own time zone.
DateTimeOffset from = new DateTimeOffset(2026, 8, 7, 0, 0, 0, TimeSpan.FromHours(2));

var request = new LicenseRequest
{
    ProductId = "MyApp 2.0",
    ValidFrom = from,
    ValidUntil = from.AddYears(1),   // the first instant it stops working
};

LicenseEnvelope envelope = generator.Generate(request, privateKey);

Console.WriteLine(envelope.ReadLicense()?.ValidUntil);
```

A window that ends before it starts is an `ArgumentException` — the generator refuses to issue a
license that can never be valid.

### Reissuing under the same identifier

Supply `Id` and `CreatedAt` to reproduce a specific license — a renewal that keeps its identity, or
a re-signature after rotating keys.

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

LicensingKey privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json");

var original = new LicenseRequest
{
    ProductId = "MyApp 2.0",
    Owner = "Acme GmbH",
    ValidUntil = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
};

LicenseEnvelope first = generator.Generate(original, privateKey);
License issued = first.ReadLicense()!;

// Same identity, longer window — a renewal rather than a second license.
LicenseRequest renewal = original with
{
    Id = issued.Id,
    CreatedAt = issued.CreatedAt,
    ValidUntil = new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero),
};

LicenseEnvelope second = generator.Generate(renewal, privateKey);

Console.WriteLine(second.ReadLicense()?.Id == issued.Id);   // True
```

A store keyed on the license identifier **replaces** the earlier entry when the renewal is added,
which is exactly what reissuing under the original identifier should do.

### Covering a range of versions

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

LicensingKey privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json");

// Matches "MyApp 2.0" and "MyApp 2.17"; does not match "MyApp 3.0".
var request = new LicenseRequest { ProductId = "MyApp 2.*", Owner = "Acme GmbH" };

LicenseEnvelope envelope = generator.Generate(request, privateKey);

Console.WriteLine(envelope.ReadLicense()?.ProductId);   // MyApp 2.*
```

### Writing to a stream

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

LicensingKey privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using FileStream output = File.Create("out/acme.license");

LicenseEnvelope envelope = await generator.GenerateAndSaveAsync(
    new LicenseRequest { ProductId = "MyApp 2.0" },
    privateKey,
    output,
    cts.Token);

Console.WriteLine($"{envelope.Payload.Length} bytes signed.");
```

The stream is flushed but never closed — ownership stays with you. Use this overload when the
destination is a network stream, a `MemoryStream` on its way into a database, or an HTTP response
body.

### Issuing many licenses from one key

The generator is stateless and safe to share, and loading the key once is worth it — the Argon2id
derivation on an encrypted key file is deliberately expensive.

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
    privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json", password);
}
finally
{
    Array.Clear(password, 0, password.Length);
}

string[] customers = { "Acme GmbH", "Globex SA", "Initech Ltd" };

foreach (string customer in customers)
{
    var request = new LicenseRequest { ProductId = "MyApp 2.0", Owner = customer };

    using FileStream output = File.Create($"out/{customer}.license");
    LicenseEnvelope envelope = await generator.GenerateAndSaveAsync(request, privateKey, output);

    Console.WriteLine($"{customer}: {envelope.ReadLicense()?.Id}");
}
```

Each call stamps its own identifier and issue time, so the three licenses are genuinely distinct.

## Notes

- **Error contract.** `ArgumentNullException` and `ArgumentException` report caller mistakes — a
  blank `ProductId`, a blank supplied `Id`, a window that ends before it starts, or the **public**
  half of a pair where the private one belongs. `CryptographicException` reports key material that
  cannot sign, or a wrong password on the path overload. I/O failures propagate untouched.
- **Signing with the public half is refused**, structurally: an issuer that can sign with a shipped
  key is an issuer whose customers can too.
- **`CreatedAt` is informational.** It is not part of the validity window and never causes validation
  to fail — a license whose `CreatedAt` lies in the future is still valid unless `ValidFrom` says
  otherwise.
- **RSA signing is fast; loading an encrypted key is not.** The cost is the Argon2id derivation
  (64 MiB by default), paid once per `LoadKeyAsync`, not per license. In a UI application, do it on a
  background thread.
- **Nothing here validates the license it just issued.** If you want to prove a freshly signed
  license passes, hand it to [`ILicenseValidator`](validating-licenses.md) with the public half —
  which is also the cheapest end-to-end test of a key pair.
