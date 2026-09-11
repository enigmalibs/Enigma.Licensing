# Enigma.Licensing v1.0.0 Release Notes

The first public release of **Enigma.Licensing** — a .NET library that generates signed software
licenses and checks them, signing with classical RSA or post-quantum ML-DSA keys. Its whole surface
follows one rule: a license's signature covers the stored payload bytes exactly, with no
canonicalization step and no string built for signing, so a signer and a verifier can never disagree
about what was signed. All cryptography comes from
[Enigma.Core](https://www.nuget.org/packages/Enigma.Core) 2.0.0, whose types — like those of every
other dependency bar the DI abstractions — never appear on the public API.

## Feature overview

- **License format** — `enigma-license/1`, a five-field JSON envelope: `format`, `algorithm`,
  `keyId`, the base64 `payload` and its `signature`. The payload is the compact UTF-8 JSON of a
  `License`, and it is the exact byte sequence the signature covers. `LicenseEnvelope.SaveAsync` and
  `LoadAsync` read and write one over any stream; `ReadLicense()` returns the **unverified** license
  inside a document without checking it — safe to display, unsafe to act on.
- **Keys** — `enigma-key/1`, one document format for both algorithm families and both halves of a
  pair. `IKeyService` generates pairs, writes both halves (creating parent directories), and loads
  one from a path or a stream. A private half can be password-encrypted at the envelope layer with
  Argon2id (RFC 9106's second recommended costs) deriving an AES-256-GCM key; a public half never
  is. Both halves carry the same `keyId` — the first 8 bytes of SHA-256 over the algorithm
  identifier and the public key material — which the generator stamps into every envelope it signs.
- **Signature schemes** — six, all round-tripping end to end: `RsaSha256`, `RsaSha384` and
  `RsaSha512` (PKCS#1 v1.5, moduli of 2048–8192 bits, 3072 by default), and `MLDsa44`, `MLDsa65` and
  `MLDsa87` (FIPS 204). The verifier is chosen from the **envelope's** `algorithm` field rather than
  from the key, which is what defeats algorithm substitution: rewriting the field means the
  signature is checked under the scheme the document claims, and it fails.
- **Generating licenses** — `ILicenseGenerator` signs a `LicenseRequest` carrying a product
  identifier, an owner, feature flags, arbitrary string claims, an optional half-open validity
  window and an optional device binding. A missing identifier becomes a fresh ULID and a missing
  creation time `UtcNow`, stamped per call. Three overloads: sign into an envelope, sign to a
  stream, or the one-call path overload that loads the key, signs and writes the license file.
- **Validating licenses** — `ILicenseValidator` returns a `LicenseValidationResult`, never an
  exception, for anything an untrusted license can be. Checks run in a fixed, documented order —
  envelope well-formed, format known, algorithm known, signature verified, payload readable,
  validity window, product identifier, device identifier — and stop at the **first** failure, so an
  expired license and a forged one are never confused. Product identifiers support an anchored `*`
  wildcard on the license's side, so one license can cover a version range. `ILicenseStore` holds
  several licenses and re-validates on every query, caching nothing — a license that expires while
  the process runs is reported the next time it is asked about.
- **Device identity** — `DeviceIdentifier.Generate()` derives a best-effort machine identifier from
  the machine name and OS version, hashed so neither is readable in a license file. Its limits are
  documented rather than hidden: an OS upgrade changes it, identical VMs collide, and MAC addresses
  are deliberately excluded. Any other scheme plugs in — the library only compares device
  identifiers ordinally.
- **Registration, trimming and AOT** — every service is a sealed class behind an interface with a
  public parameterless constructor, and `AddEnigmaLicensing()` registers all four as singletons.
  Serialization is source-generated, and the library builds with the trim and AOT analyzers enabled
  on its modern targets, so it works in an application published with `PublishAot` or with
  reflection-based JSON disabled.

## Compatibility

- Targets **netstandard2.0**, **net8.0** and **net10.0**.
- Built on **Enigma.Core 2.0.0**. Other runtime dependencies:
  **Microsoft.Extensions.DependencyInjection.Abstractions 9.0.18** (the one dependency deliberately
  visible on the public surface, since `AddEnigmaLicensing()` takes and returns an
  `IServiceCollection`), **Ulid 1.4.1** and **DeviceId 6.11.0**; **System.Buffers 4.6.1** and
  **System.Text.Json 10.0.10** on netstandard2.0 only, where they are not framework-provided.
- **A clean break from Enigma.LicenseManager.** Enigma.Licensing is the successor to
  Enigma.LicenseManager 1.2.0, which stays on NuGet for existing consumers. The license and key
  formats are new, and there is deliberately **no compatibility with, reader for, or converter from**
  the predecessor's formats: a license or key file produced by Enigma.LicenseManager cannot be read
  here, and none is planned. Migrating means reissuing licenses from a newly generated key pair.

## Version

- Initial release: **1.0.0**.
