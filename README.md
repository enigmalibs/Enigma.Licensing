# Enigma.Licensing

[![NuGet](https://img.shields.io/nuget/v/Enigma.Licensing.svg)](https://www.nuget.org/packages/Enigma.Licensing)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

Enigma.Licensing generates signed software licenses and checks them. A license is a small JSON
envelope whose signature covers the stored payload bytes **exactly** — there is no canonicalization
step, no "build a string to sign" helper, and therefore no way for a signer and a verifier to
disagree about what was signed. That single idea shapes the whole surface: you sign a
`LicenseRequest` into a `LicenseEnvelope` with a private key, ship the public half inside your
application, and ask the validator for a verdict. Signing is classical RSA or post-quantum ML-DSA,
and all cryptography comes from [Enigma.Core](https://www.nuget.org/packages/Enigma.Core) — whose
types never reach this library's public surface.

> **What's new in 2.0** — a breaking release: feature flags and claims are removed, since validation
> never read them. Every license issued by 1.0 still validates, and no key or license file changes.
> See [RELEASENOTES.md](RELEASENOTES.md).

## Features

- **License format** — `enigma-license/1`, a five-field JSON envelope carrying the format, the
  algorithm, the signing key's fingerprint, the base64 payload and its signature. `ReadLicense()`
  inspects an untrusted document without verifying it; `SaveAsync`/`LoadAsync` move one over any
  stream.
- **Keys** — `enigma-key/1`, one format for both algorithm families and both halves of a pair.
  `IKeyService` generates, saves and loads them, optionally encrypting the private half with
  Argon2id + AES-256-GCM. Both halves share a `keyId` fingerprint, which the generator stamps into
  every envelope it signs.
- **Signature schemes** — RSA with SHA-256, SHA-384 or SHA-512 (2048–8192-bit moduli, 3072 by
  default), and ML-DSA-44/65/87 (FIPS 204) for post-quantum signatures. The verifier is chosen from
  the envelope's own `algorithm` field, so rewriting it makes the signature fail rather than
  downgrade.
- **Generating licenses** — `ILicenseGenerator` signs a `LicenseRequest` carrying a product
  identifier, an owner, an optional validity window and an optional device binding. Identifier and
  creation time are defaulted per call; the one-call path overload loads the key, signs and writes
  the file.
- **Validating licenses** — `ILicenseValidator` runs a fixed, documented check order and reports the
  **first** failing step as one of seven `LicenseFailure` reasons, so an expired license and a forged
  one are never confused. Nothing a license can be makes it throw. Product identifiers may carry an
  anchored `*` wildcard, so one license can cover a version range. `ILicenseStore` holds several
  licenses and re-validates on every query, caching nothing.
- **Device identity** — `DeviceIdentifier.Generate()` derives a best-effort machine identifier from
  the machine name and OS version, hashed so neither is readable in a license file. Any other scheme
  plugs in: the library only ever compares device identifiers ordinally.

### Registration, trimming and AOT

All four services are sealed classes behind interfaces with a public parameterless constructor, so
`new LicenseValidator()` works anywhere; one call to `AddEnigmaLicensing()` registers all four as
singletons instead. Serialization is source-generated rather than reflection-based, and the library
is built with the trim and AOT analyzers enabled on its modern targets — so it stays usable in an
application published with `PublishAot` or with reflection-based JSON turned off.

## Installation

```bash
dotnet add package Enigma.Licensing
```

Targets **.NET Standard 2.0** and **.NET 10.0** (a .NET 8 or 9 application uses the .NET Standard 2.0
build); built on **Enigma.Core 2.0.0**.

## Quick start

Generate a key pair, sign a license with the private half, and check it against the public half:

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

// The issuer generates a pair once: the private half signs, the public half ships with the app.
var keyService = new KeyService();
LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);
await keyService.SaveKeyPairAsync(pair, "signing.public.key.json", "signing.private.key.json");

// Sign a license. The path overload loads the key, signs and writes the file in one call.
LicenseEnvelope envelope = await new LicenseGenerator().GenerateAndSaveAsync(
    new LicenseRequest { ProductId = "MyApp 2.0", Owner = "Acme GmbH" },
    privateKeyPath: "signing.private.key.json",
    password: null,
    outputPath: "acme.license");

// The application checks it — a verdict, never an exception.
LicenseValidationResult result = await new LicenseValidator().ValidateAsync(
    licensePath: "acme.license",
    publicKeyPath: "signing.public.key.json",
    productId: "MyApp 2.0");

Console.WriteLine(result.IsValid
    ? $"Licensed to {result.License?.Owner}, key {envelope.KeyId}."
    : $"{result.Failure}: {result.Message}");
```

## Documentation

Per-category guides — each with the supported algorithms and operations, the key types, and
copy-pasteable C# samples verified against the public API — live under `docs/guides/` in the
repository, indexed by `docs/guides/README.md`. They cover the license format, keys, generating
licenses, validating licenses, and device identity. Every public member also carries XML
documentation, which ships inside the package.

Upgrading from 1.0? `docs/migrations/1.0.0-to-2.0.0.md` in the repository says what breaks, what
deliberately does not, and why no existing license needs reissuing.

## Desktop application

The repository also contains **Enigma.Licensing.Desktop**, an Avalonia application for issuers: it
generates key pairs, signs licenses from a form (with saveable profiles), and validates a license
against a public key, reaching the same verdicts the library does. It is built on the library's
public surface — exactly what a third-party consumer gets — and is distributed as a self-contained
zip for Windows and Linux rather than through NuGet. On Linux, `packaging/linux/install.sh` in the
repository builds it and installs it for the current user — application, launcher entry and icon,
no root needed — and `uninstall.sh` beside it removes exactly that. To publish it yourself, run
`packaging/windows/publish.ps1` (or `publish.bat`) on Windows, or `packaging/linux/publish.sh` on
Linux: each is one `dotnet publish -c Release -r <win-x64|linux-x64> -p:PublishReadyToRun=true`,
whose output lands in `src/Enigma.Licensing.Desktop/bin/Release/net10.0/<rid>/publish/`.

## License

Enigma.Licensing is released under the [MIT License](LICENSE.md).
