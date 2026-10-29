# Enigma.Licensing v2.1.0 Release Notes

A maintenance release of the library, with **no API change**, and a feature release of the desktop
application. The library drops its **net8.0** target ahead of .NET 8's end of support and raises its
dependency floors. The application joins the uniform Enigma baseline: a new icon, the family's About
dialog, a safety net for unhandled errors, and packaging scripts for Windows and Linux.

**Nothing about licences or keys moved.** Both document formats are byte-identical, all six signature
schemes behave as before, the check order and its seven failure reasons are unchanged, and the error
contract is unchanged. Every licence and key file ever written still reads.

## New Features

These are all in the desktop application, **Enigma.Licensing.Desktop**. The library gains nothing new.

- **A new application icon**: a certificate with a green seal on a dark rounded square, the family's
  artwork for this app. It replaces the white key and ships as ten PNG frames, from 16 to 256 px.
- **The uniform About dialog.** An **About Enigma Licensing** button at the end of the settings page's
  About section opens the dialog every Enigma application shows: the icon, the name, the version, the
  build revision and the copyright.
- **A safety net for unhandled errors.** An exception nothing else handled used to end the application.
  Now it is logged with where it was caught and shown in a *Something went wrong* dialog, and the
  application keeps running. The dialog has a **Details** section and **Copy details**, for a bug
  report. Two errors in a row make one dialog ("1 more error occurred."). The same error again within a
  minute is only logged.
- **The icon beside the executable.** `app.ico` is written next to `Enigma.Licensing.Desktop(.exe)` in
  every build and publish folder, for shortcuts, launchers and installers.
- **Linux install and uninstall scripts.** `packaging/linux/install.sh` installs the application for the
  current user: the application, a launcher symlink, a desktop entry and eight icons, with no root
  needed. `uninstall.sh` removes exactly that.
- **Publish scripts.**
  - Windows: `packaging/windows/publish.ps1` and `publish.bat` run
    `dotnet publish -c Release -r win-x64 -p:PublishReadyToRun=true`.
  - Linux: `packaging/linux/publish.sh` runs the same command for `linux-x64`.

  The release zips are still built by `scripts/publish.*`.

## Fixes

- Nothing. No defect was reported against 2.0.0.

## Dependencies

- **Microsoft.Extensions.DependencyInjection.Abstractions 9.0.18 → 10.0.14** (the library's only
  dependency visible on its public surface). 2.0.0 deliberately held the floor at 9.x, so as not to drag
  a consumer's `Microsoft.Extensions.*` set forward. The 8.x and 9.x lines leave support with .NET 8
  and .NET 9 on 2026-11-10, which makes 10.x the only supported line, on .NET Framework as much as on
  .NET. A consumer still pinning a 9.x `Microsoft.Extensions.*` package should move it to 10.x too.
- **System.Text.Json 10.0.12 → 10.0.14** (netstandard2.0 only), a servicing patch.
- Unchanged: **Enigma.Core 2.0.0**, **Ulid 1.4.1**, **DeviceId 6.11.0**, **System.Buffers 4.6.1**
  (netstandard2.0 only).
- The licences of the shipped dependency graph were re-checked against the new versions: every
  package is MIT. The one exception is unchanged: `Microsoft.NETCore.Platforms` 1.1.0 (legacy .NET
  library licence), which ships no assembly.
- The application moves to Avalonia 12.1.4 and Enigma.Avalonia.Desktop 1.3.0 (as one set), and to
  `Microsoft.Extensions.Hosting` / `Logging.*` 10.0.14. The repository's tests move to xunit.v3 4.0.2
  and coverlet.collector 10.1.0.

## Compatibility

- Targets **netstandard2.0** and **net10.0** — previously netstandard2.0, net8.0 and net10.0.
  **net8.0 is dropped**: .NET 8 leaves support on 2026-11-10. A .NET 8 or .NET 9 application keeps
  installing the package and resolves the **netstandard2.0** build. That build has the **same public
  surface**, since no API is guarded by a target framework, so nothing fails to compile. Two things
  differ:
  - **Trimming and native AOT.** The netstandard2.0 build carries no trim/AOT annotations (those are
    on the net10.0 build), so an application published trimmed or AOT on .NET 8 or 9 can see trim
    warnings it did not before. Moving it to .NET 10 removes them.
  - **More package dependencies.** On that build the package also brings `System.Text.Json` 10.0.14,
    `System.Buffers` and `Microsoft.Bcl.AsyncInterfaces` as packages, where net8.0 had them inbox.
- Under strict SemVer, dropping a target framework counts as breaking. This release is a minor
  because no consumer loses an API or a build: the netstandard2.0 build covers every runtime net8.0
  did.
- The **`enigma-license/1` and `enigma-key/1` formats are unchanged**.
- The desktop application targets net10.0, as before. Its preference file and licence profiles are
  unchanged.

## Version

- **2.1.0**, superseding 2.0.0, for both the library and the application.

# Enigma.Licensing v2.0.0 Release Notes

A **breaking** release with one theme: **feature flags and claims are gone.** Nothing in the library
ever decided anything from them — the validator settles authenticity, then the validity window, the
product identifier and the device identifier, and stops — so both were signed, displayed and
documented while no check ever read them. They are removed rather than deprecated.

**Nothing else about the library moved.** Both document formats are byte-identical, all six signature
schemes behave as before, the check order and the seven failure reasons are unchanged, the error
contract is unchanged, and the target frameworks are unchanged. **Every licence issued by 1.0.0 still
validates.**

## Breaking Changes & Migration

Five public members were deleted outright — no `[Obsolete]`, no shim, no compatibility reader:

| Removed | Was |
|---|---|
| `License.Features` | `IReadOnlyCollection<string>` |
| `License.Claims` | `IReadOnlyDictionary<string, string>` |
| `License.HasFeature(string)` | `bool` |
| `LicenseRequest.Features` | `IReadOnlyCollection<string>?` |
| `LicenseRequest.Claims` | `IReadOnlyDictionary<string, string>?` |

**The break is a compile error and has no runtime component.** Removed members were deleted rather
than hidden, so a 1.x call site fails to build instead of behaving differently — the build error list
is the whole to-do list. Delete the initializers on your `LicenseRequest`s, and replace any
`HasFeature`/`Claims` read with whatever your application already uses to decide entitlements.

**Licences issued by 1.0.0 remain valid, and no file needs reissuing or converting.** A 1.0.0 payload
carries `"features"` and `"claims"`; a payload member this library does not know is **ignored**, not
rejected. Nothing ever re-serializes a licence in order to check one — the founding invariant of the
format — so the signature still covers exactly the bytes it was made over. The two fields simply stop
surfacing on `License`.

The full migration instructions, including what deliberately does *not* change, are in
`docs/migrations/1.0.0-to-2.0.0.md` in the repository.

## Fixes

- Nothing. No defect was reported against 1.0.0.

## Dependencies

- **System.Text.Json 10.0.10 → 10.0.12** (netstandard2.0 only, where it is not framework-provided) —
  a servicing patch that moves nothing else in the graph.
- **Held back deliberately:** `Microsoft.Extensions.DependencyInjection.Abstractions` stays on its
  **9.x** floor (10.0.12 is available). NuGet resolves a package to the highest floor in the graph, so
  raising it here would drag a consumer's whole `Microsoft.Extensions.*` set forward for no gain —
  this library uses only `IServiceCollection` and `TryAddSingleton`.
- Unchanged: **Enigma.Core 2.0.0**, **Ulid 1.4.1**, **DeviceId 6.11.0**, **System.Buffers 4.6.1**
  (netstandard2.0 only). No dependency was added or removed, so `FEATURE-57BC-PHASE01`'s third-party
  licence audit stands as written.

## Compatibility

- Targets **netstandard2.0**, **net8.0** and **net10.0** — **unchanged** from 1.0.0. No target
  framework was added or dropped.
- The **`enigma-license/1` and `enigma-key/1` formats are unchanged**. A licence or key file written
  by 1.0.0 is read by 2.0.0 exactly as before; a licence written by 2.0.0 differs only in that its
  payload no longer carries two optional members.
- Still trim- and AOT-safe: serialization stays source-generated, with the analyzers enabled on the
  modern targets.
- The companion **Enigma.Licensing.Desktop** application also moves to 2.0.0. It loses the generate
  page's *Features* and *Claims* sections and the validate card's two matching rows, and it gains
  proper branding — a white key on a blue rounded square, replacing the placeholder icon inherited
  from the predecessor application. A profile saved by 1.0.0 still loads, with its two now-unknown
  fields ignored; `formatVersion` stays `1`.

## Version

- **2.0.0**, superseding 1.0.0. Major, because public members were removed from a published package.

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
