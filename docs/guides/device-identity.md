# Device identity

A license can be locked to one machine: set `LicenseRequest.DeviceId` when you issue it, pass the
same kind of string as `deviceId` when you validate, and the two are compared **ordinally**.

`DeviceIdentifier.Generate()` is a convenience for producing that string — not a requirement. **The
library never interprets a device identifier**, it only compares it, so any scheme an application
prefers plugs in with no change to the license format: an account identifier, a hardware dongle
serial, a value provisioned at install time, a hash of something you already have.

The one thing to get right is the trap in the check itself: **a `null` `deviceId` skips the device
check, and skipping means passing.** An application that locks licenses to devices must supply the
identifier on *every* validation call — omit it and a device-locked license validates on any machine.

## Operations

| Member | Returns | Notes |
|---|---|---|
| `DeviceIdentifier.Generate()` | `string` | Machine name + OS version, hashed. Static; no state, no configuration. |

That is the whole surface. It is a `static class` with one method, and it returns a plain `string` —
the type that goes straight into `LicenseRequest.DeviceId` and straight back into
`ILicenseValidator.ValidateAsync`.

## What `Generate()` actually produces

An opaque string, stable across restarts of the same machine, built from the machine name and the
operating system version and then **hashed** — so a license file does not disclose what the customer
calls their computer.

**Treat it as a token to store and compare, never as something to parse.** The exact string is an
implementation detail of the underlying components and their encoding. Two calls on the same machine
agree; that is the whole guarantee.

### Its documented weaknesses

`Generate()` is best-effort, and deliberately so. Design around these rather than being surprised by
them:

- **An OS upgrade changes it.** A customer who installs a major update needs their license reissued.
  Handle that in support policy rather than by hunting for a component that never changes — there
  isn't one.
- **Identical machines collide.** Two freshly imaged VMs with the same name and OS produce the same
  identifier. Device locking raises the effort of casual copying; it is not a defence against a
  determined one, and no client-side scheme is.
- **The MAC address is deliberately excluded** — the same call the predecessor library made. Docks,
  VPN adapters and USB network devices come and go, and an identifier that changes when somebody
  unplugs a dock generates support tickets rather than security.

## Key types

| Type | Namespace | Role |
|---|---|---|
| `DeviceIdentifier` | `Enigma.Licensing` | The static helper. `Generate()` is its only member. |
| `LicenseRequest.DeviceId` | `Enigma.Licensing` | `string?` — set it to lock the license being issued. |
| `License.DeviceId` | `Enigma.Licensing` | `string?` — what the signed license says. `null` means the license runs anywhere. |
| `LicenseFailure.DeviceMismatch` | `Enigma.Licensing` | The verdict when a locked license meets a different machine. |

```csharp
public static string Generate();
```

## Usage

### Reading the current machine's identifier

```csharp
using System;
using Enigma.Licensing;

string deviceId = DeviceIdentifier.Generate();

Console.WriteLine(deviceId);
```

In practice a customer sends you this string — an "activate this machine" screen usually shows it
with a copy button — and you issue a license against it.

### Issuing a device-locked license

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();

LicensingKey privateKey = await keyService.LoadKeyAsync("keys/signing.private.key.json");

// The identifier the customer sent from the machine being licensed.
const string CustomerDeviceId = "sQ4kkzL5wZ0y3Ht9pC1vX7…";

var request = new LicenseRequest
{
    ProductId = "MyApp 2.0",
    Owner = "Acme GmbH",
    DeviceId = CustomerDeviceId,
};

LicenseEnvelope envelope = generator.Generate(request, privateKey);

Console.WriteLine($"Locked to {envelope.ReadLicense()?.DeviceId}.");
```

Note what you do **not** do: call `Generate()` on the issuing machine. That would lock the license to
*your* computer.

### Checking it on the customer's machine

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;

var validator = new LicenseValidator();

LicenseValidationResult result = await validator.ValidateAsync(
    "acme.license",
    "signing.public.key.json",
    productId: "MyApp 2.0",
    deviceId: DeviceIdentifier.Generate());   // never omit this in a device-locked product

if (result.Failure == LicenseFailure.DeviceMismatch)
{
    Console.WriteLine("This license is registered to another machine.");
}
```

The same argument goes to `ILicenseStore.HasValidLicense` and `FindValidLicense`, which pass it
straight through to the validator:

```csharp
using System;
using Enigma.Licensing;

var store = new LicenseStore();

// … licenses added at start-up …

string deviceId = DeviceIdentifier.Generate();

Console.WriteLine(store.HasValidLicense("MyApp 2.0", deviceId));
```

Reading the identifier once at start-up and holding it is fine — it does not change while the process
runs.

### Licenses that are not locked

A license with **no** `DeviceId` runs anywhere: it passes the device check whatever the application
asks about. Only a locked license can mismatch, so an application can pass the identifier
unconditionally and let each license decide whether it cares.

```csharp
using System;
using System.Threading.Tasks;
using Enigma.Licensing;
using Enigma.Licensing.Keys;

var keyService = new KeyService();
var generator = new LicenseGenerator();
var validator = new LicenseValidator();

LicensingKeyPair pair = keyService.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa65);

// No DeviceId — a site license, valid on every machine.
LicenseEnvelope envelope = generator.Generate(
    new LicenseRequest { ProductId = "MyApp 2.0" },
    pair.PrivateKey);

LicenseValidationResult result = validator.Validate(
    envelope,
    pair.PublicKey,
    productId: "MyApp 2.0",
    deviceId: DeviceIdentifier.Generate());

Console.WriteLine(result.IsValid);   // True — the license is not locked
```

### Plugging in your own scheme

Anything that produces a stable string works. Nothing in the library needs to know about it.

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Enigma.Licensing;

// A tenant-scoped identifier: the customer's account, not their hardware. Stable across machines,
// which is exactly right for a per-seat cloud product and exactly wrong for a per-machine one.
static string TenantDeviceId(Guid tenantId, int seat)
{
    string source = FormattableString.Invariant($"tenant:{tenantId:N}/seat:{seat}");

    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));

    return Convert.ToBase64String(hash);
}

var validator = new LicenseValidator();

LicenseValidationResult result = await validator.ValidateAsync(
    "acme.license",
    "signing.public.key.json",
    productId: "MyApp 2.0",
    deviceId: TenantDeviceId(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), seat: 3));

Console.WriteLine(result.IsValid);
```

Issue the license with `DeviceId` set to the *same* function's output and the two will compare equal.
Whatever you choose, be consistent: the comparison is ordinal and case-sensitive, so a scheme that
sometimes upper-cases its hex will fail on the machine it was issued for.

## Notes

- **Skipping the check means passing it.** `deviceId: null` is "don't ask", not "must not match".
  Supply it on every call in a product that locks licenses, including through `ILicenseStore`.
- **Comparison is ordinal and case-sensitive**, with no normalization — no trimming, no case folding,
  no Unicode normalization. The string you issue is the string that must come back.
- **Store the identifier you issued.** When a customer's OS upgrade changes their machine's
  identifier, the support flow is "reissue against the new one" — which is much easier when you know
  what the old one was.
- **`Generate()` does no I/O and needs no permissions**, so it is safe to call on a UI thread and in a
  container. It reads the machine name and OS version, nothing more.
- **This is deterrence, not enforcement.** A device lock makes casual license sharing inconvenient.
  An attacker who controls the machine controls what `Generate()` returns; the honest threat model is
  "keep honest customers honest", and the signature — not the device lock — is what makes a license
  unforgeable.
