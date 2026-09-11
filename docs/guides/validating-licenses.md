# Validating licenses

`ILicenseValidator` is the checking half of the library, and the part that runs inside the
application being licensed. You hand it a license and the **public** half of the pair it should have
been signed with, plus optionally the product and device to check against, and it returns a
`LicenseValidationResult` — a verdict, never an exception.

`ILicenseStore` sits one level up: it holds the licenses an application currently has, re-validates
on every query, and answers "am I licensed for this?" without the caller keeping track of envelopes
and keys.

**The license is untrusted input; the public key is not.** Nothing a *license* can be makes the
validator throw — malformed JSON, bad base64, an unknown format, an unknown algorithm, a broken
signature and an expired window are all verdicts. The *public key* is the opposite: it ships inside
your application, so a malformed key file, or one holding the private half, **throws**. That split is
what stops "never throws" from swallowing a deployment fault.

## The check order

Fixed, and **validation stops at the first failure** — so an expired license reports `Expired` even
if it is also for the wrong product.

| # | Check | Failure |
|---|---|---|
| 1 | The envelope is well-formed | `InvalidFormat` |
| 2 | `format` is a version this library knows | `InvalidFormat` |
| 3 | `algorithm` is a scheme this library implements | `UnknownAlgorithm` |
| 4 | The signature verifies over the payload bytes, under that algorithm | `InvalidSignature` |
| 5 | The payload deserializes into a license | `InvalidFormat` |
| 6 | `ValidFrom` / `ValidUntil` contain `DateTimeOffset.UtcNow` | `NotYetValid` / `Expired` |
| 7 | The product identifier matches, wildcards applied | `ProductMismatch` |
| 8 | The device identifier matches | `DeviceMismatch` |

**Authenticity is settled before anything the license *says* is read.** Nothing past step 4 runs on
unverified bytes, which is why an expired license and a forged one are never confused — a distinction
worth keeping in your UI too, because they are different conversations with a customer.

`LicenseValidationResult.License` is populated whenever the payload was readable — **including on
failure**, so an application can say *what* expired. On a failed result it is therefore unverified:
safe to display, unsafe to act on.

| `LicenseFailure` | What it means | Typical response |
|---|---|---|
| `InvalidFormat` | Not a readable `enigma-license/1` document, or a payload that is not a license. | "This file is not a license." Do not trust anything in it. |
| `UnknownAlgorithm` | Signed with a scheme this build does not implement. | Usually a newer issuer than the application — prompt for an update. |
| `InvalidSignature` | The bytes were modified after signing, or a different key signed them. | Compare `LicenseEnvelope.KeyId` against the key you checked with: a mismatch means *wrong key*, not forgery. |
| `NotYetValid` | `ValidFrom` is in the future. | Show the date. Often a clock problem, not a licensing one. |
| `Expired` | `ValidUntil` has passed. | Offer a renewal; the license is authentic. |
| `ProductMismatch` | The license is for a different product. | Show both identifiers — the license's is in `result.License`. |
| `DeviceMismatch` | The license is locked to another machine. | Offer a transfer; see [Device identity](device-identity.md). |

## Key types

| Type | Namespace | Role |
|---|---|---|
| `LicenseValidator` | `Enigma.Licensing` | The implementation. Sealed, stateless, public parameterless constructor. |
| `ILicenseValidator` | `Enigma.Licensing` | The interface. Registered by `AddEnigmaLicensing()`. |
| `LicenseValidationResult` | `Enigma.Licensing` | The verdict: `IsValid`, `Failure`, `Message`, `License`. |
| `LicenseFailure` | `Enigma.Licensing` | The seven reasons, in check order. |
| `LicenseStore` / `ILicenseStore` | `Enigma.Licensing` | The registry that re-validates at query time. |
| `LicensingKey` | `Enigma.Licensing.Keys` | The public half being verified against. |

```csharp
LicenseValidationResult Validate(
    LicenseEnvelope envelope,
    LicensingKey publicKey,
    string? productId = null,
    string? deviceId = null);

Task<LicenseValidationResult> ValidateAsync(
    Stream licenseInput,
    Stream publicKeyInput,
    string? productId = null,
    string? deviceId = null,
    CancellationToken cancellationToken = default);

Task<LicenseValidationResult> ValidateAsync(
    string licensePath,
    string publicKeyPath,
    string? productId = null,
    string? deviceId = null,
    CancellationToken cancellationToken = default);
```

A `null` `productId` or `deviceId` **skips** that check. For `deviceId` that means **passing**, so an
application that locks licenses to devices must supply it on every call — omitting it makes a
device-locked license validate on any machine.

`LicenseValidator` is stateless and safe to share; a single instance per application is the intended
use, which is why `AddEnigmaLicensing()` registers it as a singleton.

## Usage

### Checking a license file

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

LicenseValidationResult result = await validator.ValidateAsync(
    licensePath: "acme.license",
    publicKeyPath: "signing.public.key.json",
    productId: "MyApp 2.0");

if (result.IsValid)
{
    Console.WriteLine($"Licensed to {result.License?.Owner ?? "(unnamed)"}.");
}
else
{
    Console.WriteLine($"{result.Failure}: {result.Message}");
}
```

`Message` is a human-readable explanation of the same verdict `Failure` names — suitable for a log
line or a support tool, though a customer-facing UI usually wants its own wording per `Failure`.

### "No license installed" is not a verdict

A **missing** license file is an I/O failure and throws: the license was never read, so there is
nothing to return a verdict about. Only a file that exists and cannot be understood becomes
`InvalidFormat`.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

const string LicensePath = "acme.license";

if (!File.Exists(LicensePath))
{
    Console.WriteLine("No license installed — running in trial mode.");
    return;
}

LicenseValidationResult result = await validator.ValidateAsync(
    LicensePath,
    "signing.public.key.json",
    productId: "MyApp 2.0");

Console.WriteLine(result.IsValid ? "Licensed." : $"Not licensed: {result.Failure}.");
```

Applications that treat "no license installed" as a normal state should check for the file first,
exactly like this.

### Reacting to each failure

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

LicenseValidationResult result = await validator.ValidateAsync(
    "acme.license",
    "signing.public.key.json",
    productId: "MyApp 2.0",
    deviceId: DeviceIdentifier.Generate());

string message = result.Failure switch
{
    null => "Thank you for your support.",
    LicenseFailure.InvalidFormat => "That file is not a license.",
    LicenseFailure.UnknownAlgorithm => "This license needs a newer version of the application.",
    LicenseFailure.InvalidSignature => "This license could not be verified.",
    LicenseFailure.NotYetValid => $"This license starts on {result.License?.ValidFrom:d}.",
    LicenseFailure.Expired => $"This license expired on {result.License?.ValidUntil:d}.",
    LicenseFailure.ProductMismatch => $"This license is for {result.License?.ProductId}.",
    LicenseFailure.DeviceMismatch => "This license is registered to another machine.",
    _ => "This license could not be used.",
};

Console.WriteLine(message);
```

Reading `result.License` here is exactly what it is for — it is populated even on failure, so the
message can name the date or the product. It is unverified for `InvalidFormat`, `UnknownAlgorithm`
and `InvalidSignature` (and `null` for most of them); for the four verdicts below the signature
check, it came out of bytes whose signature has already verified.

### Validating an envelope you already hold

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var validator = new LicenseValidator();

LicensingKey publicKey = await keyService.LoadKeyAsync("signing.public.key.json");

using FileStream input = File.OpenRead("acme.license");
LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);

// The keyId is a hint for picking between several shipped keys — only Validate settles anything.
if (envelope.KeyId is not null && envelope.KeyId != publicKey.KeyId)
{
    Console.WriteLine($"This license names key {envelope.KeyId}, not {publicKey.KeyId}.");
}

LicenseValidationResult result = validator.Validate(envelope, publicKey, productId: "MyApp 2.0");

Console.WriteLine(result.IsValid);
```

Note that `LicenseEnvelope.LoadAsync` **throws** for a document it cannot parse, whereas the
validator's own overloads turn that into `InvalidFormat`. Reach for this overload when you already
have a parsed envelope; use `ValidateAsync` when you have a file or a stream and want the verdict to
cover the parse too.

### Reading a key from somewhere other than a file

The stream overload is what to use when the public key is an embedded resource, which is the usual
way to ship one inside an application.

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

Assembly assembly = Assembly.GetExecutingAssembly();

using Stream? publicKeyInput = assembly.GetManifestResourceStream("MyApp.signing.public.key.json");

if (publicKeyInput is null)
{
    throw new InvalidOperationException("The public key is missing from the application.");
}

using FileStream licenseInput = File.OpenRead("acme.license");

LicenseValidationResult result = await validator.ValidateAsync(
    licenseInput,
    publicKeyInput,
    productId: "MyApp 2.0");

Console.WriteLine(result.IsValid);
```

### Reading what a valid license says

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

LicenseValidationResult result = await validator.ValidateAsync(
    "acme.license",
    "signing.public.key.json",
    productId: "MyApp 2.0");

if (result.IsValid && result.License is { } license)
{
    Console.WriteLine($"Licensed to {license.Owner ?? "(not stated)"}");
    Console.WriteLine($"Expires: {license.ValidUntil?.ToString("u") ?? "never"}");
}
```

Guard on `IsValid` **first**. `License` alone says what a document claims, not whether the claim is
authentic.

## The store

`ILicenseStore` is the shape most applications want: load whatever licenses were installed at
start-up, `Add` each with the public key it should be checked against, then ask `HasValidLicense`
wherever the application needs to know whether it is licensed.

**Every query re-validates, and nothing is cached** — not the verdict, not the parsed license. That
costs a signature verification per query and buys the only behaviour that is actually correct: a
license expires while the application is running, and the very next query says so.

**Adding a license asserts nothing about it.** `Add` only reads the license identifier out of the
payload, so a license that is expired, unsigned or signed by the wrong key is accepted and then fails
every query. That keeps *installing* a license and *judging* one as separate concerns — to find out
whether an incoming license is any good, and why not, ask `ILicenseValidator` directly.

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var store = new LicenseStore();

LicensingKey publicKey = await keyService.LoadKeyAsync("signing.public.key.json");

foreach (string path in Directory.GetFiles("licenses", "*.license"))
{
    using FileStream input = File.OpenRead(path);
    LicenseEnvelope envelope = await LicenseEnvelope.LoadAsync(input);

    store.Add(envelope, publicKey);
}

if (store.HasValidLicense("MyApp 2.0"))
{
    Console.WriteLine("Licensed.");
}

License? active = store.FindValidLicense("MyApp 2.0");
Console.WriteLine(active?.Owner ?? "(no valid license)");
```

`FindValidLicense` returns the **first** match in insertion order, so the result is stable when more
than one license applies; `Add` replaces an entry with the same license identifier in place, which is
what reissuing under the original identifier should do. `Remove(licenseId)` takes it back out and
returns whether anything matched.

An application that has to pick between overlapping licenses on some other basis should hold them
itself and use `ILicenseValidator` directly.

### The store and a custom validator

`LicenseStore` has a second constructor taking the `ILicenseValidator` it should validate with — the
one service in this library that genuinely composes with another rather than wrapping Enigma.Core.

```csharp
using System;
using Enigma.Licensing;

ILicenseValidator validator = new LicenseValidator();
var store = new LicenseStore(validator);

Console.WriteLine(store.HasValidLicense("MyApp 2.0"));   // False — nothing added yet
```

The store is thread-safe: a lock guards the registry, and queries snapshot under it and verify
outside it, so slow cryptography never blocks a write.

## Dependency injection

`AddEnigmaLicensing()` registers all four services with `TryAddSingleton`. It lives in the
`Microsoft.Extensions.DependencyInjection` namespace, so it appears on `IServiceCollection` without
an extra `using`.

```csharp
using System;
using Enigma.Licensing;
using Enigma.Licensing.Keys;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddEnigmaLicensing();

using ServiceProvider provider = services.BuildServiceProvider();

var keyService = provider.GetRequiredService<IKeyService>();
var generator = provider.GetRequiredService<ILicenseGenerator>();
var validator = provider.GetRequiredService<ILicenseValidator>();
var store = provider.GetRequiredService<ILicenseStore>();

Console.WriteLine(store.HasValidLicense("MyApp 2.0"));   // False — nothing added yet
```

The library depends on `Microsoft.Extensions.DependencyInjection.Abstractions` only; building a
`ServiceProvider` needs the concrete `Microsoft.Extensions.DependencyInjection` package, which an
application using `Microsoft.Extensions.Hosting` already has.

Two details worth knowing:

- **`TryAddSingleton` throughout**, so registering your own implementation *before* calling
  `AddEnigmaLicensing()` keeps yours.
- **`ILicenseStore` is registered through an explicit factory** naming `ILicenseValidator`, so a store
  always validates through whatever validator the container ended up with — including your own.

Nothing from Enigma.Core is registered: each service constructs the primitives it needs itself.

## Notes

- **Error contract.** Verdicts cover everything a *license* can be. `ArgumentNullException` covers
  null arguments; `ArgumentException` covers a key document that is not well-formed, **or one holding
  the private half** — verifying with a private key would mean an application that verifies correctly
  while shipping the key that mints licenses. `CryptographicException` covers unusable key material,
  and `FileNotFoundException` and other I/O failures propagate untouched.
- **Signature verification fails closed.** An unexpected failure inside the cryptographic layer is
  reported as `InvalidSignature`, never as a pass. The worst case is a good license reported invalid,
  never the reverse.
- **Validation reads the clock at every call** (`DateTimeOffset.UtcNow`), and there is deliberately no
  clock abstraction to substitute. Tests craft real validity windows instead.
- **A wildcard is only ever honoured on the license's side.** The `productId` your application passes
  in is always a literal, so a customer cannot install a license for `*` by asking about one.
- **Keep the public key alongside the application, not beside the license.** A license file and the
  key that verifies it, shipped together, verify each other perfectly and prove nothing.
