# Enigma.Licensing — Guides & Samples

Per-category guides for **Enigma.Licensing**, a .NET library that generates signed software licenses
and checks them, signing with RSA or post-quantum ML-DSA keys. The shape is the same throughout: an
issuer generates a key pair with `IKeyService`, signs a `LicenseRequest` into a `LicenseEnvelope`
with `ILicenseGenerator`, and the licensed application verifies that envelope against the **public**
half with `ILicenseValidator` — or lets `ILicenseStore` hold several and re-check them on demand.

All four services are sealed classes behind interfaces with a public parameterless constructor, so
`new LicenseValidator()` works anywhere. They are equally DI-friendly: one call to
`AddEnigmaLicensing()` registers all four as singletons.

Each guide follows the same shape — **supported algorithms/operations → key types →
copy-pasteable usage samples → notes** — and every snippet targets the real public API, with its
full `using` block.

## The formats

- [The license format](license-format.md) — the `enigma-license/1` envelope, what the payload
  contains, the algorithm identifiers, why the signature covers the payload bytes with no
  canonicalization step, and how to read a document without trusting it.
- [Keys](keys.md) — the `enigma-key/1` format, generating pairs in all six schemes, RSA modulus
  sizes, Argon2id + AES-256-GCM password encryption, key fingerprints, and every `IKeyService`
  member.

## Issuing and checking

- [Generating licenses](generating-licenses.md) — `LicenseRequest`, features and claims, the
  half-open validity window, product wildcards, reissuing under an existing identifier, and the three
  `ILicenseGenerator` overloads.
- [Validating licenses](validating-licenses.md) — the fixed check order, all seven `LicenseFailure`
  reasons and what to do about each, `ILicenseStore`, and `AddEnigmaLicensing()`.

## Locking a license to a machine

- [Device identity](device-identity.md) — `DeviceIdentifier.Generate()`, its documented weaknesses,
  the skipping-means-passing trap, and plugging in a scheme of your own.

## Where to start

Issuing licenses for the first time? Read [Keys](keys.md), then
[Generating licenses](generating-licenses.md). Adding a license check to an application? Read
[Validating licenses](validating-licenses.md) — it is the only one an application that merely
*consumes* licenses needs.
