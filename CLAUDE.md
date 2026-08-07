# CLAUDE.md

Guidance for Claude Code (and other AI agents) working in this repository.

## Current state

`FEATURE-2FE8` stood up the repo, the root configuration, a multi-targeted library and an MTP-native
xUnit v3 test project. `FEATURE-03A9-PHASE01` deleted the bootstrap placeholder and shipped the
**license model and the `enigma-license/1` envelope format**; `PHASE02` shipped the **`enigma-key/1`
key format and `IKeyService`**, and with it the first Enigma.Core reference; `PHASE03` shipped the
**generator, validator, store and device identity**; `PHASE04` shipped **`AddEnigmaLicensing()` and the
public-surface guard test**. See *Shipped* below.

**`FEATURE-03A9` is complete — the library is feature-done.** It signs and checks licenses end to end,
all six algorithms round-trip through save → load → validate, all four services are DI-registrable, and
a reflection guard enforces that nothing but `Enigma.Licensing` types reach the public surface.
Everything in *Architecture & conventions* below now describes **code, not design**. Never write a
doc, comment or summary that describes a planned type as if it already ships.

**`FEATURE-179C` (the Avalonia desktop app) is complete too**: `PHASE01` shipped the **app shell,
hosting, theme and settings**, `PHASE02` the **keys page and the four application service seams**,
`PHASE03` the **generate page and licence profiles**, and `PHASE04` the **validate page**. There are no
placeholder pages left — the app generates real key pairs, signs real licences with them and reaches real
verdicts about them. `FEATURE-57BC` (the v1.0.0 release) is what remains, and its `PHASE01` has landed:
the library carries its **packaging metadata**, is **trim- and AOT-safe**, and its third-party licence
audit is on file. `PHASE02` (the usage guides) is next. Read the plan file in `docs/plan/` before
implementing any of it; the plan is the contract.

**The trim/AOT decision that was outstanding is answered — the fix shipped.** Serialization was
reflection-based, which meant a consumer publishing with `PublishAot`, or with
`JsonSerializerIsReflectionEnabledByDefault=false`, got an `InvalidOperationException` on the first
serialize. `FEATURE-57BC-PHASE01` replaced it with two source-generated `JsonSerializerContext` classes
and turned on `IsAotCompatible`, so the trim and AOT analyzers now police the library at build time.
Do not reintroduce a `JsonSerializer` overload that takes bare `JsonSerializerOptions` — that is exactly
what the analyzers reject. See *Shipped* below.

## What this is

**Enigma.Licensing** is a .NET library that generates signed software licenses and imports, checks
and validates them, signing with RSA or post-quantum ML-DSA keys. All cryptography comes from
[Enigma.Core](https://www.nuget.org/packages/Enigma.Core) 1.1.0. A companion Avalonia desktop app
(`src/Enigma.Licensing.Desktop`, `FEATURE-179C`) gives the issuer a UI for the same operations, and all
four of its pages — keys, generate, validate and settings — ship.

It is the successor to **Enigma.LicenseManager** (v1.2.0, which stays on NuGet for legacy
consumers). It is a **clean break**: the license and key formats are new, and there is deliberately
**no compatibility with, reader for, or converter from** the predecessor's formats. Do not add one.

## Shipped

### License format `enigma-license/1` — signed JSON envelope (FEATURE-03A9-PHASE01)

`LicenseEnvelope` is an indented JSON object with exactly five fields, in this order:

```json
{
  "format": "enigma-license/1",
  "algorithm": "RSA-SHA256",
  "keyId": "3F9A1C40D2B87E56",
  "payload": "eyJpZCI6IjAxSllRN1pLM00…",
  "signature": "cHJldGVuZC1zaWduYXR1cmU…"
}
```

`payload` is base64 of the compact UTF-8 JSON of a `License`; `signature` is base64. `keyId` is
omitted when null, as is every optional field of the payload — `JsonIgnoreCondition.WhenWritingNull`
means an absent value and an explicit null are the same document. Everything else about the
serializer is left **strict**: camelCase names matched case-sensitively, no comments, no trailing
commas. Unknown *envelope* fields are ignored, though — they fall outside the signature anyway, and
rejecting them would rule out a future optional field within format 1.

**Load-bearing invariant — the signature covers the exact payload bytes, and there is no
canonicalization step.** The predecessor's hand-rolled `GetDataForSignature()` string was the source
of its critical v1.0.0 bug; this design eliminates the entire class of defect. Never reintroduce a
"build a string to sign" helper, and never re-serialize a `License` in order to check one. Algorithm
substitution is defeated by *validating* rather than canonicalizing: the verifier is chosen from the
envelope's `algorithm` value, so flipping that field makes the signature fail to verify under the
substituted scheme (and the ML-DSA parameter set is additionally bound to the key itself). This
reasoning lives in the XML docs of `LicenseEnvelope` and `LicensePayload`; a tamper test flipping
`algorithm` is part of `FEATURE-03A9-PHASE03`'s acceptance criteria.

`LicenseEnvelope.LoadAsync` checks `format` (a parse concern — it decides whether the document shape
is interpretable) but deliberately **not** `algorithm` (a verdict the validator has to be able to
report as `UnknownAlgorithm`, which it cannot do for a document that never parsed). It throws
`ArgumentException` — with the underlying `JsonException` as `InnerException` — for anything it
cannot parse; the *validator* is the layer that never throws.

Types in place: `License` (`HasFeature` is ordinal; `Features`/`Claims` coerce an assigned null to
empty, so an untrusted payload cannot hand downstream code a null), `LicenseRequest`,
`LicenseEnvelope` + `LicenseEnvelope.CurrentFormat`, `LicenseSignatureAlgorithm`, and the internal
seams `AlgorithmIds` (the single enum ↔ wire-string table), `LicenseJson` and `LicensePayload`.
`InternalsVisibleTo("Enigma.Licensing.UnitTests")` is declared in `Properties/AssemblyInfo.cs` so the
last two can be tested directly.

### Key format `enigma-key/1` and `IKeyService` (FEATURE-03A9-PHASE02)

One format for both algorithm families and both halves of a pair — six fields, in this order, of
which `encryption` is the only optional one:

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

- **The `key` field has three encodings, chosen by the document's own fields, never guessed from the
  content.** Encrypted → base64 of the GCM ciphertext, whatever the algorithm. Unencrypted RSA → the
  Enigma.Core PEM text (SPKI public / PKCS#8 private). Unencrypted ML-DSA → base64 of the raw FIPS 204
  encoding (private key in expanded form, as Core emits).
- **Private keys are optionally password-encrypted at this envelope layer, uniformly.** Argon2id
  (Core `Argon2Service`, RFC 9106's second recommended costs, recorded in `encryption`) derives an
  AES-256-GCM key (Core `BlockCipherService`); the tag is appended to the ciphertext, and `tagBits`
  records its length. The RSA PEM is therefore always generated **unencrypted** at the Core layer —
  Core's PEM-level password support is deliberately unused, because ML-DSA has no equivalent and two
  schemes would mean two sets of failure modes. A PEM that *is* encrypted at the PEM layer is rejected
  at load. **Public keys are never encrypted**: a password supplied for one is an `ArgumentException`,
  and a public document carrying an `encryption` object is malformed.
- **KDF costs read from a file are bounded** (64 iterations, 1 GiB, 64 lanes). They are read before the
  password is checked, so an unbounded reader would let a hostile file exhaust memory unauthenticated.
- `keyId` is the uppercase hex of the first 8 bytes of SHA-256 over `algorithmId ‖ publicKeyMaterial` —
  computed from the **public** half at generation and written into both files, so a pair shares one
  value. The material is hashed exactly as stored, with **no normalization**. It is read back verbatim,
  never recomputed: it is an identifier, not a security control. PHASE03 stamps it into license
  envelopes at signing time.
- **Unknown `algorithm` in a *key* file is fatal**, unlike in a license envelope — there is no verdict
  to report about a key, only a key that cannot be used.
- Passwords are `char[]`; the caller owns clearing them (Enigma.Core convention). The service clears
  its own copies (UTF-8 buffer, derived key) and never touches the caller's array.
- **No AAD.** GCM authenticates the key material; the surrounding fields are not covered. Editing them
  can only make the key fail to load or produce signatures that do not verify, and adding AAD would
  mean building a byte string out of metadata — the very pattern this library avoids.

Types in place: `IKeyService` / `KeyService` (generate; save/load by stream; `SaveKeyPairAsync` and
`LoadKeyAsync` by path, creating parent directories), `LicensingKey` +
`LicensingKey.CurrentFormat`, `LicensingKeyPair`, `KeyKind`, and the internal seams `KeyEnvelope`,
`KeyEncryption`, `KeyFormat`, `KeyKindIds`, `KeyFingerprint`, `KeyMaterial`, `KeyMaterialCipher` and
`CoreAlgorithms` (the only place naming an Enigma.Core algorithm type). `LicensingKey` keeps its
material internal, so the PEM-versus-raw difference never reaches the public API.

### Generator, validator, store & device identity (FEATURE-03A9-PHASE03)

Three services in `Enigma.Licensing`, all sealed classes behind interfaces with a public parameterless
constructor: `ILicenseGenerator`/`LicenseGenerator` (sign a `LicenseRequest` into a `LicenseEnvelope`),
`ILicenseValidator`/`LicenseValidator` (stateless verdict over an envelope) and
`ILicenseStore`/`LicenseStore` (thread-safe registry). Plus `LicenseFailure`,
`LicenseValidationResult` and the static `DeviceIdentifier`.

- **The verifier is chosen from the *envelope's* `algorithm`, not the key's.** This is the mechanism
  behind the format's algorithm-substitution claim, and the reason `LicenseSignatures.Verify` takes the
  algorithm as an argument separate from the key: rewriting the field means the signature is checked
  under the scheme the file claims, and it fails. An RSA key can sign under any of the three digests,
  so nothing but this check would stop a downgrade. A key from the other family is refused structurally
  before Core is called at all — PEM text is not a FIPS 204 encoding.
- **Signature verification fails closed.** `LicenseSignatures.Verify` catches broadly (everything but
  `OutOfMemoryException`, `StackOverflowException` and `OperationCanceledException`) and returns
  `false`. Core wraps BouncyCastle, whose "not a key / not a signature" failures surface as several
  unrelated types that are not part of Core's documented contract; enumerating them would break on a
  Core or BouncyCastle bump for no gain. The worst case of the broad catch is a good license reported
  invalid, never the reverse.
- **The license is untrusted input; the public key is not.** Nothing a *license* can be makes the
  validator throw — malformed JSON, bad base64, unknown format, unknown algorithm, a broken signature
  and an expired window are all verdicts. The *public key* is the opposite: it ships inside the
  application, so a malformed key file, or one holding the private half, throws. Keep that split — it
  is what stops "never throws" from swallowing a deployment fault. I/O still propagates, so a **missing
  license file throws** rather than returning `InvalidFormat`.
- **Validation stops at the first failure**, in the order documented under *Validation order* below.
  Authenticity is settled before anything the license *says* is read, so an expired license and a
  forged one are never confused. `LicenseValidationResult.License` is populated whenever the payload was
  readable — including on failure, so an application can say *what* expired — which also means it is
  unverified on a failed result: safe to display, unsafe to act on.
- **The generator defaults what the request omits**: `Id` becomes a fresh ULID and `CreatedAt`
  `DateTimeOffset.UtcNow`, stamped per call, so signing one request twice issues two licenses. It
  rejects a blank `ProductId` or `Id` and a window that ends before it starts, and stamps the pair's
  `keyId` into every envelope. The path overload signs *before* touching the output file, so a rejected
  request leaves no truncated license behind.
- **The store re-validates on every query and caches nothing** — not the verdict, not the parsed
  license. That is the only correct behaviour: a license expires while the process runs and the next
  query says so. `Add` asserts nothing beyond reading the identifier out of the payload, so installing
  a license and judging one stay separate concerns. A lock guards the registry; queries snapshot under
  it and verify outside it, so slow cryptography never blocks a write. `FindValidLicense` returns the
  first match in **insertion order**, and `Add` replaces an entry with the same id in place.
- **Product matching escapes first, then substitutes.** `Regex.Escape` the license's identifier, swap
  `\*` for `.*`, anchor with `\A`/`\z` — not `^`/`$`, which in .NET also match before a trailing
  newline. The wildcard is honoured only on the **license's** side; the application's identifier is
  always a literal. Escaping first is what fixes the predecessor's bug where a dot in a version number
  matched any character.
- **`DeviceIdentifier.Generate()` is best-effort and documented as such**: machine name + OS version,
  hashed, so the machine name is not readable in a license file. An OS upgrade changes it; identical
  VMs collide; the MAC address is deliberately excluded because docks and VPN adapters come and go. Any
  other scheme plugs in freely — the library never interprets a device identifier, it only compares it
  ordinally.

Types in place: `ILicenseGenerator`/`LicenseGenerator`, `ILicenseValidator`/`LicenseValidator`,
`ILicenseStore`/`LicenseStore`, `LicenseFailure`, `LicenseValidationResult`, `DeviceIdentifier`, and
the internal seams `LicenseSignatures` (sign/verify dispatch), `ProductIdMatch` (the wildcard rule) and
`DocumentFile` (the path guard plus open-for-read / create-with-parent-directories, which `KeyService`
was folded onto in the same change). `CoreAlgorithms` gained `ToRsaSignatureAlgorithm` and remains the
only place naming an Enigma.Core algorithm type.

### DI registration & the public-surface guard (FEATURE-03A9-PHASE04)

`AddEnigmaLicensing()` — a single extension on `IServiceCollection`, `TryAddSingleton` for all four
services — plus the reflection test that makes the isolation invariant enforceable rather than merely
documented.

- **The class is `EnigmaLicensingServiceCollectionExtensions`, in the
  `Microsoft.Extensions.DependencyInjection` namespace.** The namespace is the Microsoft convention, so
  the method appears without an extra `using`. The *qualified* class name is deliberate and differs from
  Enigma.DataEncryption's bare `ServiceCollectionExtensions`: an app referencing both Enigma libraries
  would otherwise have two identically named types in one namespace — fine when calling the method,
  ambiguous for anyone naming the type.
- **The store is registered through an explicit factory**, not `TryAddSingleton<ILicenseStore, LicenseStore>()`.
  The container would pick the greediest satisfiable constructor, which happens to be the right one;
  naming `ILicenseValidator` makes it a decision, so a consumer's own validator is what the store
  validates with. The other three are plain interface→implementation registrations.
- **Nothing from Enigma.Core is registered.** Each service news up the Core primitives it needs in its
  own constructor — Core ships no registration extension, and there is no seam worth substituting. Four
  registrations is the whole of it.
- **`Microsoft.Extensions.DependencyInjection.Abstractions` is the one dependency deliberately visible
  on the public surface**, since the extension takes and returns `IServiceCollection`. It earns the
  exception by being a contract package: abstractions only, and already present in any app doing DI. The
  floor is 9.x, matching Enigma.DataEncryption — NuGet resolves to the highest floor in the graph, so a
  high floor here would drag a consumer's whole `Microsoft.Extensions.*` set forward.
- **The guard test has two arms, because a namespace rule is not enough.** `PublicSurfaceIsolationTests`
  walks every exported type's base types, interfaces, method returns and parameters, constructor
  parameters and fields (unwrapping arrays, by-refs and generic arguments). The first arm rejects the
  documented namespaces (`Enigma.Core.*`, `Org.BouncyCastle.*`). The second is an **assembly allowlist** —
  `Enigma.Licensing`, the framework, and the DI abstractions — because **`Ulid` is declared as
  `System.Ulid`** and no namespace rule could ever distinguish it from a framework type, and because an
  allowlist fails closed for a dependency added later. A third test pins the walk itself, since the
  failure mode of a reflection guard is passing vacuously. **If you add a dependency, expect the second
  arm to fail the moment one of its types reaches a public member — that is the test working.**

Types in place: `EnigmaLicensingServiceCollectionExtensions` (namespace
`Microsoft.Extensions.DependencyInjection`, in the `Enigma.Licensing` assembly). No new library types
otherwise — this phase's other half was the XML-doc and API review pass, which found nothing to change.

### Desktop app shell, hosting, theme & settings (FEATURE-179C-PHASE01)

`src/Enigma.Licensing.Desktop` — an Avalonia 12.1.1 app on **Enigma.Avalonia.Desktop 1.0.0**, net10.0
only, following the Enigma.HardCopy structure. It runs: a navigation rail (Keys / Generate / Validate,
with Settings pinned to the footer), a complete Settings page, and three placeholder pages that
`PHASE02`–`PHASE04` fill in. **The library is nothing but a dependency of it** — the app consumes the same
public surface a third-party consumer would.

- **`App` owns the `IHost`, `Program.cs` owns nothing.** `Program` stays synchronous —
  `BuildAvaloniaApp().StartWithClassicDesktopLifetime` — and the host is built in
  `OnFrameworkInitializationCompleted`, started (never *run*: Avalonia's lifetime runs the app), and
  stopped from `desktop.Exit`. The window is **resolved** from the container rather than constructed,
  because the control library's services need that very window's hosts and storage provider.
- **Five one-time handovers happen before the window is shown**: `RegisterHost` for Overlay, ContentDialog
  and InfoBar, `SetStorageProvider` for the file and folder pickers. Each of those services throws
  `InvalidOperationException` from its first call otherwise, and the first call is a user clicking a
  button. In `MainWindow.axaml` the three hosts are the **last** children of the root `Panel`, ordered
  **Overlay → ContentDialog → InfoBar** — deliberately *not* the library guide's order, so a dialog raised
  during a long operation is never painted under the overlay's scrim.
- **The stored theme is applied before the first frame**, so the app never flashes the OS variant and then
  corrects itself. `App.axaml` declares `RequestedThemeVariant="Default"`, which is also what
  `AppTheme.System` and an unreadable preference file leave it as.
- **Two registration extensions, in two files, on purpose.** `AddEnigmaAvaloniaDesktop()` registers the
  control library's six services (`TryAddSingleton`; it ships none of its own);
  `AddEnigmaLicensingDesktop()` registers everything else and calls the library's own
  **`AddEnigmaLicensing()`** rather than re-registering its four services by hand — that is what keeps the
  app on the library's composition, including the store's explicit validator factory. The split, and
  `App.axaml.cs`'s `AppTheming` alias, exist because `Enigma.Avalonia.Desktop.Services` and
  `Enigma.Licensing.Desktop.Services` will both declare an `IFileDialogService` from `PHASE02`. Keep them.
- **Page views are transient, page ViewModels singleton.** A ViewModel holds work in progress that must
  survive leaving a page; a fresh control per navigation is what stops the visual tree leaking. Swapping
  either lifetime looks like a working app and silently breaks one of the two. Asserted by
  `ServiceCollectionExtensionsTests` as descriptors, since neither can be resolved without a platform.
- **`AppSettingsStore` never throws.** Missing, unreadable, truncated, hand-edited, or written by a newer
  `formatVersion` all resolve to the defaults plus a log line; a save writes a sibling temp file and moves
  it over the target. The file is `ApplicationData/Enigma.Licensing/settings.json`, and **appearance is the
  only thing in it** — no key path, and never a password.
- **Visiting Settings must not repaint or rewrite.** `SettingsViewModel` reads the stored variant into the
  backing field, not through the setter, because `App` has already applied it. A change applies and
  persists **exactly once**; a re-selection and a null from the selector do neither.
- **Strings are inline; there is no `.resx`** (validated decision, matching Enigma.Backup, Draw and the
  predecessor desktop app — HardCopy is the outlier). `Resources/AppInfo.cs` holds the title, the project
  URL and the two versions, read from the running assemblies so an about box cannot drift from the build.
- **`GenerateDocumentationFile` is on for the app too**, so a missing XML comment on a public member is a
  build error here as well. Note that Avalonia declares `DataContext` on `StyledElement`, not
  `Control`/`Window` — a cref naming the wrong one is `CS1574`.

Types in place: `Program`, `App`, `MainWindow` + the four page views, `MainWindowViewModel`,
`SettingsViewModel`, `ThemeOption`, the three placeholder page ViewModels, `IThemeService`/`ThemeService`,
`AppSettings`/`IAppSettingsStore`/`AppSettingsStore`/`AppTheme`, `AppIcons`, `AppInfo`, and the two
hosting extensions. The app-own wrappers over the info bar, the overlay and the file picker landed with
`PHASE02`, the first phase with a caller for them.

### Keys page & the application service seams (FEATURE-179C-PHASE02)

The first of the three placeholder pages made real, plus the four seams that let a ViewModel do platform
work without naming a platform type. The page picks a scheme, optionally a modulus size and a password,
asks where both halves go, generates off the UI thread behind the overlay, and keeps the new key's
fingerprint on screen with a Copy button.

- **`IInfoBarService.ShowAsync` completes when the bar is *dismissed*, not when it appears.** The control's
  `ShowAsync` returns a `TaskCompletionSource` resolved by its `Closed` event. `NotificationService`
  therefore **does not await it**, and hands the library's task back only when it has already completed —
  which in practice means it failed, i.e. a host never registered at startup. Awaiting would hold a command
  open until a user cleared a banner, and the `finally` that wipes the password copy out of memory would not
  run until then. `NotificationServiceTests` pins this with a double whose task never resolves. **Do not
  "simplify" that method back into a one-line `=>`.**
- **No `Content` on the info bar.** Its control template has no `ContentPresenter`, so the inherited
  `Content` is never rendered and an action button cannot live there. That is why the fingerprint's copy
  action sits on the page's result card — which is better anyway: a dismissed notification cannot be copied
  from.
- **The progress card is built in code, in `BusyOverlayService`**, from the app's own `card` / `h2` / `hint`
  style classes. A templated control would have meant a class, a control theme and a dictionary merged into
  `App.axaml` to say the same thing. `BusyOverlayServiceTests` walks the resulting tree, because a control
  tree assembled wrongly compiles perfectly and shows an empty grey box.
- **`ClipboardService` reaches the clipboard through the running application's main window**, the same route
  `ThemeService` takes to the application — no startup handover, and no clipboard is a log line, not a
  failure.
- **Defaults: ML-DSA-65, and RSA at 4096** (the predecessor desktop default, not the library's 3072 — an
  issuing key is generated once and the seconds are paid once). The size picker is *absent*, not disabled,
  for the ML-DSA members, whose parameter sets fix their own key sizes.
- **A password typed and not yet repeated keeps the generate button disabled**, while the mismatch message
  appears only once something has been typed into the confirmation. Two rules on purpose: one governs the
  button, the other the error text. The predecessor left its button live with an unconfirmed password.
- **The password's `string` cannot be cleared, only the `char[]` copy** the page makes for the one call that
  needs it — a `TextBox` hands its text over as a string. A test reads the array back after the command to
  prove the page cleans up after itself; the password is never written to a file or a log.
- **Suggested file names are `<name>.public.key.json` / `<name>.private.key.json`**, offered as `signing.*`
  when nothing has been chosen and named after the other half once one has. Nothing enforces the suffixes —
  the library reads a key file by its contents.
- **`InternalsVisibleTo("Enigma.Licensing.Desktop.UnitTests")` is now declared for the app too**
  (`Properties/AssemblyInfo.cs`), for the two services above, whose behaviour *is* how they use the control
  library and so cannot be reached through a fake of their own interface.

Types in place: `IFileDialogService`/`FileDialogService`, `INotificationService`/`NotificationService`,
`IBusyOverlayService`/`BusyOverlayService`, `IClipboardService`/`ClipboardService`,
`SignatureAlgorithmOption`, and a real `GenerateKeysViewModel` + `GenerateKeysView`. Interfaces public,
implementations `internal sealed`, all four registered `AddSingleton` by `AddEnigmaLicensingDesktop()`.

### Generate page & licence profiles (FEATURE-179C-PHASE03)

The second placeholder page made real, and the one the issuer lives in. It takes a product identifier, an
owner, a device (with a **This machine** button over `DeviceIdentifier.Generate()`), an optional validity
window, a feature list and a claims list, then a private key file and its password, and signs through
`ILicenseGenerator.GenerateAndSaveAsync` on a background thread behind the overlay. Plus **licence
profiles**: one small JSON file per saved form.

- **The chosen end date is *included* in the window.** `ValidUntil` is the first instant a licence is no
  longer usable, so the picker's day becomes midnight at the **end** of it — a licence valid until the 31st
  works all day on the 31st. `ValidFrom` is local midnight at the start of its day, and the time of day a
  picker happens to carry is discarded on both ends. The opposite reading compiles just as well and generates
  a support ticket every time; tests sit on both sides of the boundary, two of them against the real validator.
- **The licence identifier is read out of the payload, because there is no other way to get it.** The
  generator mints a ULID per call and the envelope carries it only inside the signed bytes, and `Ulid` is
  deliberately off the library's public surface, so the page cannot mint its own. It calls
  `envelope.ReadLicense()?.Id` and accepts a `null` — the licence is on disk by then, and not being able to
  name it is no reason to report the issue as failed. (Until `FEATURE-57BC-PHASE01` there was nothing on
  `LicenseEnvelope` that read a licence out of `Payload`, and this page hand-parsed the one property with
  `JsonDocument`; that gap is closed and the private helper is gone.)
- **A profile holds the form and the signing key's *path*, never the password and never the output path.**
  There is no algorithm field either: the scheme belongs to the key and was fixed when the pair was generated,
  so the field could only ever be written and not read — and could silently disagree with the key it names.
  (The plan's design line said otherwise; it was amended in place with the reason.) Loading a profile also
  clears the password field, since the profile has just named a possibly different key.
- **`ILicenseProfileStore` throws where `IAppSettingsStore` swallows.** The preference file is the app's own
  and nobody asked for it; a profile is a file the user picked from a dialog and is watching for the form to
  fill, so a file that is not a profile is reported on the info bar. Both stores are right — they answer to
  different owners.
- **Claims are add-and-remove, never edit-in-place.** `LicenseClaimEntry` is an immutable record and the add
  command refuses a name already present, which makes a duplicate key structurally impossible rather than
  merely unlikely — two editable rows sharing a name would collapse into one claim on the way into the licence.
- **The generate button is *live* for a missing field and *disabled* for a contradictory window.** PHASE02's
  rule, applied: a disabled button explains nothing, so an unfilled field is flagged when the button is
  pressed, while a window ending before it starts disables the command and says why under the dates — the
  same shape as the keys page's password confirmation. `ObservableValidator` annotations drive the product
  field's error text.
- **`CalendarDatePicker`, not `DatePicker`**: the dates are individually optional and must be clearable, and
  `DatePicker` offers no way to unset a value once one is chosen.
- **`IFileDialogService` gained `ShowOpenFileDialogAsync`** — PHASE02 needed only saves. It returns one path,
  and reports a cancellation and a pick with no local path alike, since this app reads documents by path.

Types in place: `LicenseProfile` + `LicenseProfile.CurrentFormatVersion`, `ILicenseProfileStore` /
`LicenseProfileStore` (registered `AddSingleton`), `LicenseClaimEntry`, and a real `GenerateLicenseViewModel`
+ `GenerateLicenseView`.

### Validate page (FEATURE-179C-PHASE04)

The last placeholder made real, and with it the app does end to end what the library does. The page picks a
licence and a public key, takes an optional expected product and device (with the generate page's **This
machine** button), calls `ILicenseValidator.ValidateAsync` on the two paths, and shows the answer in three
stacked cards: the verdict, the document, and the licence.

- **Verdicts go to the card; exceptions go to the info bar.** The library's own split, honoured in the UI:
  nothing a *licence* can be makes the validator throw, so all seven failures are verdicts and belong somewhere
  a user can read them twice, while a missing file, a key document that is not a key, and the private half where
  the public one belongs are the application's own mistake and become error notifications that leave the previous
  result alone. **A valid licence raises no notification at all** — the green card is the report.
- **Three severities, and the split follows the library's boundary.** Authenticity is settled before anything the
  licence *says* is read, so a failure is either "this document cannot be trusted" (`InvalidFormat`,
  `UnknownAlgorithm`, `InvalidSignature` → red) or "authentic, and it does not apply here" (`NotYetValid`,
  `Expired`, `ProductMismatch`, `DeviceMismatch` → amber). An expired licence and a forged one are different
  conversations with a customer; painting both red would hide that.
- **The severity reaches XAML as bound style classes**, not as a converter or a brush on the ViewModel:
  `Classes.success="{Binding Verdict.IsSuccess}"` over four `Border.verdict*` styles in `App.axaml` built on the
  theme's own status pairs. `LicenseVerdictSeverity` is the app's own enum precisely so the control library's
  `InfoBarSeverity` stays out of a page — the same reason `INotificationService` exists. Do not swap it back.
- **The page reads the licence file a second time itself**, for the document card's `algorithm` and `keyId`. The
  library's path overload owns the verdict but hands back a verdict, not the document it read; those two fields
  are the diagnosis on a signature failure, because a fingerprint that is *not* the key being checked against
  says "wrong key" rather than "forged licence". Both failure modes of that read are swallowed — the verdict has
  already accounted for each.
- **Nothing here runs off the UI thread or behind the overlay**, unlike the other two pages: a public key is
  never encrypted, so there is no Argon2id derivation, and verifying is microseconds either side of two small
  file reads. `IsBusy` stops a second click, it does not cover a wait. `IBusyOverlayService` is not injected.
- **`LicenseSummary` formats the instants as they are** — local time with the offset, `yyyy-MM-dd HH:mm zzz` —
  so a licence issued "through the 31st" reads as midnight on the 1st, with a line on the card saying why.
  Rounding it into a friendlier day would make this page and the generate page disagree about one licence. It
  also gives the absent optionals words ("Not stated", "Any machine", "Immediately", "Never expires") and
  flattens `License.Claims` into `LicenseClaimEntry` rows, because a compiled binding cannot template a
  dictionary without naming a constructed generic type in XAML.
- **A verdict is not cleared when an input is edited** — it is the last check, replaced by the next one, as the
  other pages' result cards are. Reading the licence's real product off the card in order to type it into the
  expected-product field is a real workflow, and a card that vanished mid-keystroke would break it.

Types in place: `LicenseVerdict`, `LicenseVerdictSeverity`, `LicenseSummary`, and a real
`ValidateLicenseViewModel` + `ValidateLicenseView`. No new service and no new registration — the page is built
from seams that already existed.

### Package metadata, source-generated JSON & the licence audit (FEATURE-57BC-PHASE01)

The library became a packable NuGet project — `Enigma.Licensing` 1.0.0, MIT, all twelve packaging
properties plus the `README.md`/`LICENSE.md` packing `ItemGroup` — and, in the same phase, stopped needing
runtime reflection. The plan file covered only the metadata and the audit; the two decisions `CLAUDE.md`
had parked on this phase were surfaced at build time, confirmed, and the plan amended in place.

- **Serialization is source-generated, and the analyzers enforce it.** `EnvelopeJsonContext` covers
  `LicenseEnvelope` and `KeyEnvelope` (indented); `PayloadJsonContext` covers `License` (compact). **Two
  contexts, not one**, because a `JsonSerializerContext` is bound to the `JsonSerializerOptions` it was
  built with and refuses to resolve types for any other instance — so the two documented option sets
  cannot share one. The settings both formats specify (camelCase, omit-when-null, strict everything else)
  moved from `JsonSerializerOptions` onto `[JsonSourceGenerationOptions]` and say the same thing.
- **`LicenseJson` hands out `JsonTypeInfo<T>`, not `JsonSerializerOptions`** — `Envelope`, `KeyFile` and
  `Payload`. This is not cosmetic: handing `JsonSerializer` a bare options object is **not statically
  analyzable even when the options carry a source-generated resolver**, so those overloads are attributed
  `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` and produced 30 IL2026/IL3050 errors on the first
  attempt. Only the typed overloads are clean. `KeyFile` is new because `KeyEnvelope` and `LicenseEnvelope`
  had been sharing one options instance.
- **`IsAotCompatible` is on for net8.0 and net10.0, off for netstandard2.0** (never published AOT). It
  implies `IsTrimmable` and turns the trim/AOT/single-file analyzers on, so under `TreatWarningsAsErrors`
  a reflection-needing code path is a **build error**. The library builds clean under it, Enigma.Core,
  Ulid, DeviceId and `Regex` paths included. Know the limit: the analyzers see *this* assembly, so an
  unannotated reflection-using dependency would still surface at a consumer's publish. It adds
  `Microsoft.NET.ILLink.Tasks` as an auto-referenced build task — verified absent from the nuspec's
  dependency groups.
- **The contexts are the only resolver these documents have**, so the whole suite is the real proof: every
  save, load, sign and verify in 1164 tests runs through source generation, and a document type that lost
  its metadata would throw rather than quietly reflect. `LicenseJsonTests` pins the diagnosis, including
  that a context answers **only** for what it was given — a context resolving an unregistered type would
  mean the fallback had crept back in.
- **`LicenseEnvelope.ReadLicense()` closes the public-surface gap** `FEATURE-179C-PHASE03` flagged: it
  returns the **unverified** `License` parsed from `Payload`, or `null` when those bytes are not one, and
  **never throws** — inspecting an untrusted document is not an error path. It is documented with exactly
  the warning `LicenseValidationResult.License` carries: safe to display, unsafe to act on. It is backed
  by `LicensePayload.TryDeserialize`; everything else still goes through `Deserialize`, because the
  validator needs the `JsonException` to pick the right failure reason.
- **Third-party licence audit — 16 shipped packages, all clear.** Fifteen are MIT. The one exception,
  `Microsoft.NETCore.Platforms` 1.1.0 (DeviceId → Microsoft.DotNet.PlatformAbstractions → here, on
  netstandard2.0 only), carries the legacy MICROSOFT .NET LIBRARY licence — and ships **no assembly at
  all**, just a `runtime.json` and a zero-byte `lib/.../_._` placeholder, so nothing under it is
  redistributed. Compile-only (PolySharp), build-task (ILLink.Tasks) and test-only packages are out of
  scope. The full table is in `docs/done/FEATURE-57BC-PHASE01.md`.
- **The package cannot be packed until `PHASE03`.** `dotnet pack` fails **NU5040 — the readme file is
  empty**, because `README.md` is a zero-byte placeholder by design. The metadata was verified instead by
  generating the nuspec directly (`dotnet msbuild -t:GenerateNuspec`). `PHASE04`'s pack-verify is the
  first point a real pack can succeed, which is the order the phases already run in.
- **The desktop app carries an explicit `<Version>1.0.0</Version>`**, as its own csproj comment promised
  this phase would set. It matches the SDK default it had been inheriting, so nothing observable moved —
  but `PHASE04`'s publish scripts read it back with `dotnet msbuild -getProperty:Version`.

Types in place: `EnvelopeJsonContext`, `PayloadJsonContext` (both internal), `LicensePayload.TryDeserialize`,
and `LicenseEnvelope.ReadLicense()` — the phase's only public-surface addition. `<PackageReleaseNotes>` is
deliberately still absent; `PHASE03` writes it beside `RELEASENOTES.md` so the two cannot disagree.

## Architecture & conventions

These hold across the whole library, and everything here **ships** — `FEATURE-03A9` is complete, so no
heading below describes design rather than code.

### Algorithms

`LicenseSignatureAlgorithm` covers the full Enigma.Core signing surface, with one internal
`AlgorithmIds` class as the **single source of truth** for the enum ↔ string mapping (mirroring
Enigma.Core's `SignatureAlgorithms.ToJcaName`). All six ship end to end as of PHASE03 — enum, wire
strings, key generation, signing and verifying — and every one is exercised by the round-trip theory:

| Enum member | Identifier | Backed by |
|---|---|---|
| `RsaSha256` / `RsaSha384` / `RsaSha512` | `RSA-SHA256` / `RSA-SHA384` / `RSA-SHA512` | `IPublicKeyService`, PKCS#1 v1.5, PEM keys |
| `MLDsa44` / `MLDsa65` / `MLDsa87` | `ML-DSA-44` / `ML-DSA-65` / `ML-DSA-87` | `IMLDsaServiceFactory.CreateMLDsaService(parameterSet)`, raw `byte[]` keys |

`CoreAlgorithms` is the seam that maps the enum onto those Core primitives (RSA-versus-ML-DSA, and the
FIPS 204 parameter set); unlike `AlgorithmIds` it throws on a value it cannot map, because anything
reaching it has already been validated. RSA sizes accepted are 2048–8192 bits, default 3072.

Matching is ordinal and case-sensitive both ways, and both directions use the unknown → null/false
pattern rather than throwing — `AlgorithmIds` maps, it does not decide what an unmappable value
means. Nothing outside that class may spell one of these strings.

### Services & DI

Four interfaces: `IKeyService` in `Enigma.Licensing.Keys` (generate, save, load keys) and
`ILicenseGenerator`, `ILicenseValidator`, `ILicenseStore` in `Enigma.Licensing`. They are sealed
classes with a public parameterless constructor that news up the Enigma.Core services they need — Core
ships no DI extension of its own, so there is nothing to inject. All are stateless and safe to share
except `LicenseStore`, which is a registry and is guarded by a lock; it alone takes a second
constructor, `LicenseStore(ILicenseValidator)`, because it is the one service that genuinely composes
with another rather than wrapping Core directly.

`AddEnigmaLicensing()` registers all four with `TryAddSingleton`, in the
`Microsoft.Extensions.DependencyInjection` namespace (predecessor convention). See *DI registration &
the public-surface guard* above for the store's explicit factory and the naming of the extension class.

The model — `License`, `LicenseRequest`, `LicenseEnvelope`, `LicenseValidationResult` — is
**immutable**: sealed `init`-only records, varied with `with`. Dates are `DateTimeOffset`, serialized
ISO-8601 with an explicit offset and validated against `DateTimeOffset.UtcNow`; there is **no clock
abstraction** (tests craft real validity windows instead). Serialization is System.Text.Json with
camelCase names — never Newtonsoft.

### Validation order

Fixed, documented in `ILicenseValidator`'s XML docs, and asserted row by row by `LicenseValidatorTests`:
envelope well-formed → format version known → algorithm known → signature verifies over the payload
bytes → payload deserializes → `ValidFrom`/`ValidUntil` window vs `UtcNow` → productId match (exact,
or the anchored wildcard when the **license's** productId contains `*`) → deviceId ordinal match.
Validation stops at the first failure, so an expired license reports `Expired` even if it is also for
the wrong product. The result carries the parsed `License` whenever the payload was parseable, even on
failure — unverified, so it is safe to display and unsafe to act on.

A `null` `productId` or `deviceId` argument **skips** that check. For `deviceId` that means passing: an
application that locks licenses to devices must supply it on every call.

### Error contract (Enigma.Core discipline)

- `ArgumentNullException` / `ArgumentException` for caller mistakes — including a document that is not
  a well-formed key file of a known format, kind and algorithm, and base64 that does not decode.
- `System.Security.Cryptography.CryptographicException` for crypto-input failures — wrong or missing
  password, malformed or tampered key material, out-of-range KDF parameters in a key file.
- **Validation never throws** for an invalid or malformed license: `ILicenseValidator` returns a
  `LicenseValidationResult` with a `LicenseFailure` reason. It catches the parse failures and maps
  them, and signature verification fails closed rather than propagating. (`LicenseEnvelope.LoadAsync`
  *does* throw on malformed input — the validator is the layer that never throws, and it runs that
  parse inside its own `try`/`catch`.) The **key** a validator is handed is not covered by this: a bad
  key file throws, because it is the application's own file rather than untrusted input.
- I/O exceptions propagate untouched — including a missing license file.

**Load-bearing invariant — only `Enigma.Licensing` types on the public surface.** Enigma.Core, Ulid and
DeviceId are all implementation details: no `Enigma.Core.*`, `Org.BouncyCastle.*`, `Ulid` or
`DeviceId.*` type may appear on any exported type or member. (That is why `DeviceIdentifier.Generate()`
returns a plain `string`.) The sole deliberate exception is
`Microsoft.Extensions.DependencyInjection.Abstractions`, because `AddEnigmaLicensing()` takes and returns
an `IServiceCollection`. `PublicSurfaceIsolationTests` enforces all of this by reflection — modelled on
Enigma.Core's `BouncyCastleIsolationTests`, with a second assembly-allowlist arm that catches what a
namespace rule cannot. **Keep it green**, and add to the allowlist only with the reason written down.

## Project layout

Directories marked *planned* do not exist yet.

```
Enigma.Licensing.slnx                Solution (SLNX format; /src/ and /tests/ solution folders)
Directory.Build.props                Shared build defaults (Authors, Copyright, LangVersion 14, Nullable, TreatWarningsAsErrors)
Directory.Packages.props             Central Package Management (all package versions pinned here)
.editorconfig                        Code style + analyzer severities
.gitattributes                       LF everywhere; CRLF for .bat/.cmd
global.json                          SDK 10.0.100 (latestFeature); test runner = Microsoft.Testing.Platform
LICENSE.md                           MIT
README.md, RELEASENOTES.md           Empty — written at release time (FEATURE-57BC-PHASE03)
src/Enigma.Licensing/                The library
  License.cs                         The license data — the signed payload's model
  LicenseRequest.cs                  Generator input
  LicenseEnvelope.cs                 The enigma-license/1 document + SaveAsync/LoadAsync/ReadLicense
  LicenseSignatureAlgorithm.cs       The six signing schemes
  ILicenseGenerator.cs               Sign a request into an envelope — interface + error contract
  LicenseGenerator.cs                Its implementation: defaulting, request validation, signing
  ILicenseValidator.cs               The verdict interface + the documented check order
  LicenseValidator.cs                Its implementation
  ILicenseStore.cs                   The registry that re-validates at query time
  LicenseStore.cs                    Its implementation: lock over a list, snapshot-then-verify
  LicenseFailure.cs                  The seven failure reasons, in check order
  LicenseValidationResult.cs         The verdict record (+ internal Valid/Invalid factories)
  DeviceIdentifier.cs                Generate() — machine name + OS version, hashed
  EnigmaLicensingServiceCollectionExtensions.cs   AddEnigmaLicensing() — namespace M.E.DependencyInjection
  Internal/AlgorithmIds.cs           Single source of truth for enum ↔ wire strings
  Internal/CoreAlgorithms.cs         The enum → Enigma.Core primitive map (the only place naming one)
  Internal/DocumentFile.cs           Path guard + open-for-read / create-with-parent-directories
  Internal/KeyEnvelope.cs            The enigma-key/1 document (internal — see Shipped)
  Internal/KeyEncryption.cs          Its `encryption` object: kdf, costs, salt, cipher, nonce, tag size
  Internal/KeyFingerprint.cs         The keyId construction (SHA-256 over algorithmId ‖ public material)
  Internal/KeyFormat.cs              Format invariants, default KDF costs, and the maxima accepted back
  Internal/KeyKindIds.cs             KeyKind ↔ "public"/"private", on the AlgorithmIds pattern
  Internal/KeyMaterial.cs            `key`-field encoding per algorithm + the structural material check
  Internal/KeyMaterialCipher.cs      Argon2id → AES-256-GCM over the private material
  Internal/EnvelopeJsonContext.cs    Source-gen metadata for the two documents a human opens (indented)
  Internal/PayloadJsonContext.cs     The same for the signed payload (compact) — two, because options bind
  Internal/LicenseJson.cs            The three JsonTypeInfo instances; never bare JsonSerializerOptions
  Internal/LicensePayload.cs         License ↔ signed UTF-8 JSON bytes (+ TryDeserialize, behind ReadLicense)
  Internal/LicenseSignatures.cs      Sign/verify dispatch across the two Core families; fails closed
  Internal/ProductIdMatch.cs         The wildcard rule: escape, then substitute, anchored \A…\z
  Properties/AssemblyInfo.cs         InternalsVisibleTo for the test assembly
  Keys/IKeyService.cs                Generate / save / load keys — the interface and its error contract
  Keys/KeyService.cs                 Its implementation, on Enigma.Core
  Keys/KeyKind.cs                    Public or private half
  Keys/LicensingKey.cs               A parsed key + LicensingKey.CurrentFormat; material stays internal
  Keys/LicensingKeyPair.cs           Both halves, as generation returns them
src/Enigma.Licensing.Desktop/        The Avalonia issuer app (net10.0)
  Program.cs                         Synchronous entry point; the host is built in App, not here
  App.axaml(.cs)                     Theme merge + style classes; owns the IHost and the startup wiring
  app.manifest                       Per-monitor-v2 DPI awareness (version bumped at release time)
  Assets/appicon.ico                 Placeholder branding, reused from the predecessor app
  Hosting/EnigmaAvaloniaServiceCollectionExtensions.cs   AddEnigmaAvaloniaDesktop() — the library's six
  Hosting/ServiceCollectionExtensions.cs                 AddEnigmaLicensingDesktop() — + AddEnigmaLicensing()
  Properties/AssemblyInfo.cs         InternalsVisibleTo for the app's test assembly (PHASE02 — see Shipped)
  Resources/AppIcons.cs              The four Phosphor rail outlines, resolved once each
  Resources/AppInfo.cs               Title, project URL, app & library versions read from the assemblies
  Services/IThemeService.cs          Repaint the app — the seam that keeps Avalonia out of the ViewModels
  Services/ThemeService.cs           The only place that assigns RequestedThemeVariant
  Services/IFileDialogService.cs     Ask where to write a document, or which one to open — paths, not handles
  Services/FileDialogService.cs      Over the library's picker; every document this app writes is JSON
  Models/LicenseProfile.cs           A saved generate-page form + its formatVersion; no password, no algorithm
  Models/ILicenseProfileStore.cs     Load/Save — and unlike the settings store, these report what went wrong
  Models/LicenseProfileStore.cs      One JSON file per profile, camel-cased, indented, nulls omitted
  Services/INotificationService.cs   Report an outcome: success or error
  Services/NotificationService.cs    Over the info bar — and deliberately does NOT await its dismissal
  Services/IBusyOverlayService.cs    Block the app while something long runs; always hide in a finally
  Services/BusyOverlayService.cs     Builds the progress card in code, from the app's own style classes
  Services/IClipboardService.cs      Copy a short value — the fingerprint, above all
  Services/ClipboardService.cs       Via the running app's main window; no clipboard is a log line
  Settings/AppTheme.cs               System / Light / Dark
  Settings/AppSettings.cs            The preference file's shape + its formatVersion
  Settings/IAppSettingsStore.cs      Load/Save — neither may throw into the application
  Settings/AppSettingsStore.cs       ApplicationData/Enigma.Licensing/settings.json, temp-then-move
  ViewModels/MainWindowViewModel.cs  The rail: three workflows + settings in the footer; navigates nowhere
  ViewModels/ThemeOption.cs          One appearance choice: an AppTheme and the words offering it
  ViewModels/SettingsViewModel.cs    Apply-and-persist exactly once, plus the about section
  ViewModels/SignatureAlgorithmOption.cs   One picker entry: scheme, wire label, description, is-RSA
  ViewModels/LicenseClaimEntry.cs          One key/value claim row — immutable, so no two can share a name
  ViewModels/LicenseVerdictSeverity.cs     Success / Warning / Error — the app's own, not InfoBarSeverity
  ViewModels/LicenseVerdict.cs             The failure → headline/explanation/severity table + the class flags
  ViewModels/LicenseSummary.cs             What a licence says, as the strings the read-only card shows
  ViewModels/GenerateKeysViewModel.cs      The keys page: pickers, password, paths, generate, fingerprint
  ViewModels/GenerateLicenseViewModel.cs   The generate page: the form, the lists, profiles, sign, result
  ViewModels/ValidateLicenseViewModel.cs   The validate page: two files, two expectations, verdict + cards
  Views/MainWindow.axaml(.cs)        NavigationView + the three service hosts, last in the root Panel
  Views/SettingsView.axaml(.cs)      Appearance SettingsCard + the About expander
  Views/GenerateKeysView.axaml(.cs)        Three SettingsCardExpanders, the accent button, the result card
  Views/GenerateLicenseView.axaml(.cs)     A profile SettingsCard + six expanders, the button, the result card
  Views/ValidateLicenseView.axaml(.cs)     Two expanders, the button, then the verdict / document / licence cards
tests/Enigma.Licensing.UnitTests/    xUnit v3 test suite
  SmokeTest.cs                       Toolchain smoke test
  LicenseTestData.cs                 Deterministic shared fixtures (no UtcNow — output is pinned)
  Api/                               Tests over the public surface
    SignedLicenseFixture.cs          One generate-once key pair per algorithm + the theory data
    SignedLicenseTestData.cs         Requests with real windows; save/load/edit an envelope
    PublicSurfaceIsolationTests.cs   The reflection guard: forbidden namespaces + assembly allowlist
  DependencyInjection/               AddEnigmaLicensing() — resolution, lifetimes, TryAdd, round-trips
  Internal/                          Tests over the internal seams (needs InternalsVisibleTo)
    LicenseJsonTests.cs              The source-gen guarantee: the resolver, the settings, and nothing else
  Keys/                              Key generation, storage & encryption + their shared fixtures
  Model/                             Tests over the records
tests/Enigma.Licensing.Desktop.UnitTests/   xUnit v3 test suite for the app (net10.0)
  Hosting/                           The container wiring: what is registered, and with which lifetime
  Settings/                          The preference file and every way it can be useless
  Models/                            The profile file: its shape on disk, and every way one can be unreadable
  Services/                          NotificationService's timing, BusyOverlayService's card (needs friend access)
  LicenseSamples.cs                  Licences and verdicts built by hand — no cryptography, nothing that moves
  ViewModels/                        The rail, the appearance selector, and the three real pages + their round-trips
  Resources/                         AppInfo's shape (a version, no build metadata, an https project URL)
  TestDoubles/                       The app's seams and the library ones beneath them, plus RecordingLogger
docs/roadmap.md                      Work-item registry
docs/plan/                           Per-item plans
docs/done/                           Per-dev completion records
docs/guides/                         planned — per-category usage guides (FEATURE-57BC-PHASE02)
scripts/                             planned — app zip publish scripts (FEATURE-57BC-PHASE04)
```

There is **no CI workflow**, deliberately — consistent with every sibling Enigma repo.

## Target frameworks & dependencies

- The library multi-targets **`netstandard2.0;net8.0;net10.0`** — it ships as a NuGet package, and
  the broad floor mirrors Enigma.Core so no Enigma.Core consumer is excluded (Enigma.DataEncryption
  precedent). The reason is recorded in a csproj comment; keep it there.
- The library's test project targets **`net8.0;net10.0`**. `netstandard2.0` is never a test TFM; its
  polyfill surface is exercised through the `net8.0` paths. The desktop app and its test project are
  **net10.0** only — an application, not a package, so nothing is gained by reaching back.
- **Currently pinned** in `Directory.Packages.props` — only what the code actually references:
  `Enigma.Core` 1.1.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.18, `Ulid` 1.4.1 and
  `DeviceId` 6.11.0 (all three TFMs), `PolySharp` 1.16.0,
  `System.Buffers` 4.6.1 and `System.Text.Json` 10.0.10 (those three **netstandard2.0 only** in the
  library), `xunit.v3` 3.2.2, `coverlet.collector` 10.0.1,
  `Microsoft.Extensions.DependencyInjection` 9.0.18 (tests only — the concrete container, so the
  registration tests can build a real `ServiceProvider`). Referencing `System.Buffers` or
  `System.Text.Json` on net8.0+ raises
  NU1510 and fails the zero-warning build — both are framework-provided there, and the library's JSON
  usage stays inside what System.Text.Json 8.0 supports. PolySharp (compile-only, `PrivateAssets=all`)
  is what makes `init`/`required` members and the nullable-analysis attributes work on netstandard2.0.
- **Enigma.Core is referenced unconditionally**: it multi-targets exactly the same three TFMs, and its
  netstandard2.0 dependency group asks for `System.Buffers` 4.6.1 — the version already pinned — so the
  graph closes with no duplicate. It brings `BouncyCastle.Cryptography` 2.7.0 transitively; neither may
  reach this library's public surface.
- **Ulid and DeviceId are referenced unconditionally too** (both MIT, both multi-target the same three
  TFMs). Ulid mints the identifier the generator stamps when a request carries none — sortable by issue
  time, unlike a GUID; on netstandard2.0 it asks for `System.Memory` and
  `System.Runtime.CompilerServices.Unsafe`, which the graph already closes over. DeviceId backs
  `DeviceIdentifier.Generate()`; only the **base** package is referenced (the `DeviceId.Windows` /
  `.Linux` / `.Mac` packages exist for hardware-bound components this library deliberately does not
  use), and on netstandard2.0 alone it pulls `Microsoft.DotNet.PlatformAbstractions` 3.1.6 — legacy, but
  MIT and .NET Foundation-authored, and cleared by `FEATURE-57BC-PHASE01`'s licence audit along with the
  `Microsoft.NETCore.Platforms` 1.1.0 beneath it (non-MIT, and ships no assembly at all).
- **Microsoft.Extensions.DependencyInjection.Abstractions is referenced unconditionally too** (MIT), and
  is the **only** dependency whose types are allowed on the public surface — `AddEnigmaLicensing()` takes
  and returns `IServiceCollection`. Abstractions only, never the concrete container: a library registers
  services, it does not build the provider. It is not part of any shared framework, so unlike
  System.Text.Json it raises no NU1510 on net8.0+. The **9.x floor is deliberate** (Enigma.DataEncryption
  precedent): NuGet resolves a package to the highest floor in the graph, so a high floor here would drag
  a consumer's whole `Microsoft.Extensions.*` set forward. On netstandard2.0 alone it pulls
  `Microsoft.Bcl.AsyncInterfaces` and `System.Threading.Tasks.Extensions` — both inbox on net8.0+, both
  MIT, and both cleared by the same audit. (Both resolve above their floors, to 10.0.10 and 4.6.3, since
  System.Text.Json 10.0.10 asks for more on that leg.)
- **Runtime dependencies, final for the library: Enigma.Core, Microsoft.Extensions.DependencyInjection.Abstractions,
  Ulid and DeviceId everywhere; System.Buffers and System.Text.Json on netstandard2.0 only.** Nothing
  further is expected — `FEATURE-03A9` is complete, and `FEATURE-57BC-PHASE01` added no package (its
  source generator ships inside System.Text.Json / the SDK, and `IsAotCompatible`'s
  `Microsoft.NET.ILLink.Tasks` is an auto-referenced build task that never reaches the nuspec).
- **The desktop app's pins landed with `FEATURE-179C-PHASE01`** and are expected to be its whole set:
  `Microsoft.Extensions.Hosting` / `.Logging.Console` / `.Logging.Debug` 10.0.10, and the
  **version-coupled** group — `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`,
  `Avalonia.Fonts.Inter` 12.1.1, `AvaloniaUI.DiagnosticsSupport` 2.2.3, `CommunityToolkit.Mvvm` 8.4.2,
  `Enigma.Avalonia.Desktop` 1.0.0 and `Enigma.Icons` / `.Phosphor` / `.Avalonia` 1.0.0. **Bump that group
  as a unit, never one package at a time**: the control library pins Avalonia 12.1.1 and
  CommunityToolkit.Mvvm 8.4.2 itself, and Enigma.Icons.Avalonia needs Avalonia ≥ 12.1.0. Five of them
  (Avalonia, Avalonia.Themes.Fluent, CommunityToolkit.Mvvm, Enigma.Icons, Enigma.Icons.Phosphor) would
  arrive transitively and are pinned explicitly anyway, house convention, so the version is visible rather
  than inherited. `AvaloniaUI.DiagnosticsSupport` replaces `Avalonia.Diagnostics`, removed in Avalonia 12,
  and the app csproj strips it from every non-Debug build. `Enigma.Avalonia.Desktop` also brings
  `Enigma.Core` (≥ 1.0.0) transitively, behind its binary editors; the graph closes on the 1.1.0 the
  library pins, so there is one Enigma.Core and one BouncyCastle in the app.
- **Add a pin when the reference actually lands**, not in advance — and never put `Version=` on a
  `<PackageReference>`; all versions live in `Directory.Packages.props`.

## Build & test

The SDK lives in `~/.dotnet` on this machine, so export `DOTNET_ROOT` (or prefix the command):

```bash
# Build the whole solution (Release)
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release

# Run the full test suite (Microsoft.Testing.Platform runner, per global.json)
DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
```

Note the `--solution` flag: on the .NET 10 SDK in MTP mode, `dotnet test <Solution>.slnx` is
rejected.

Zero-warning builds are enforced (`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`), so
any warning fails the build. **XAML (`AVLN…`) warnings bypass `TreatWarningsAsErrors`** and are emitted
from an MSBuild task, so the desktop app can print `Build succeeded` while carrying them. Check by hand
after touching any `.axaml`:

```bash
DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj \
  -c Release --no-incremental -v normal | grep -c AVLN     # must print 0
```

`GenerateDocumentationFile` is on for the app as well as the library, so a missing XML comment on a
public member fails the build in both.

Tests are **MTP-native**: `xunit.v3` + `coverlet.collector`, with **no** `Microsoft.NET.Test.Sdk`
and **no** `xunit.runner.visualstudio` — both break the Microsoft Testing Platform. `Xunit` is the
only implicit using in each test project. Test fixtures are copied by a `**/*.json` + `**/*.txt`
`PreserveNewest` glob (library suite only — the desktop suite loads no fixture files).

**The desktop suite stands up no Avalonia platform.** Anything needing one — `MainWindow`, the page
views, and `MainWindowViewModel`'s icon factory (a Phosphor glyph becomes a `StreamGeometry`, which needs
the platform render interface) — is asserted as a *service descriptor* rather than resolved, and the
shell ViewModel takes its rail icons as `Geometry` arguments precisely so a test can pass a
`PathGeometry` sentinel. Keep that seam; the alternative is a headless-Avalonia dependency for four
assertions.

*Constructing* a control needs no platform, though, and `Services/` relies on that: `BusyOverlayServiceTests`
walks the `Border`/`StackPanel`/`ProgressBar` tree the overlay card is built from, and `FakeInfoBarService`
hands a real `InfoBar` to the configure callback. Measuring, templating or rendering one is what would need
a platform, and nothing in the suite does any of the three.

The xunit.v3 analyzers run at warning level, so under `TreatWarningsAsErrors` **they are build
errors**. The one that bites constantly is `xUnit1051`: any call taking a `CancellationToken` must be
passed `TestContext.Current.CancellationToken`. `LicenseEnvelopeTests`, the `Keys/` and `Api/` suites,
`DependencyInjection/` and the desktop suite's two round-trip suites plus `Models/` each keep a
`private static CancellationToken Token => TestContext.Current.CancellationToken;`
for this — copy that pattern rather than suppressing the rule.

**Key generation is the expensive part of the suite** (most of the ~13 s run over 1164 cases — 866 in the
library suite, 298 in the desktop one, which costs about two seconds): ten RSA
pairs, one at 4096 bits, plus Argon2id at 64 MiB per encrypted round-trip. Two collection fixtures exist
so nothing generates a key it does not need to:

- `GeneratedKeyFixture` (`Keys/`) — one RSA-2048 and one ML-DSA-65 pair, for the key-format suites.
- `SignedLicenseFixture` (`Api/`) — one pair per `LicenseSignatureAlgorithm` member, RSA at 2048, for
  the signing suites. It also exposes `AllAlgorithms` as theory data; `Rsa` and `MLDsa` are the
  defaults for a test that just needs *a* key of each shape. The `DependencyInjection/` suite joins
  this collection too, rather than generating its own — a registration test needs *a* key, not a new one.

Generate outside a fixture only when generation is what the test is about. The **desktop** suite has no
fixture and needs none: it generates ML-DSA-44 pairs, the cheapest the library makes, and only where a real
key is unavoidable — a real `LicensingKeyPair` (the type's constructor is internal to the library, so a fake
key service has to be handed one rather than invent it) or a real key *file* for the generate page to sign with
and the validate page to check against. The one exception is `ValidateLicenseRoundTripTests`'s headline theory,
which generates **one RSA-2048 pair** because "both algorithm families reach a verdict" is what that test is
about; `LicenseSamples` covers everything else that needs a licence or a verdict without any cryptography at
all, since `LicenseValidationResult` has a public constructor.

### Two ways this build goes red without anyone touching the repo

Both follow from `TreatWarningsAsErrors=true`. Recognise them before hunting for a regression that
isn't there:

- **`NETSDK1138` — net8.0 leaves support on 2026-11-10.** The SDK's end-of-life target-framework
  check is skipped for `Library` projects but live for `Exe`, so it applies to
  **`Enigma.Licensing.UnitTests` alone**: it is the only project in the solution that is both an
  executable and targets net8.0 (the library is a `Library`; the app and its test project are net10.0
  only). The first SDK release that adds `8.0` to `_EolNetCoreTargetFrameworkVersions` turns that
  project's net8.0 leg into an error. Fix then: drop `net8.0` there, or set `CheckEolTargetFramework`
  to false. A comment at the property records this.
- **`NU1901`–`NU1904` — NuGet auditing.** Restore warnings are errors here too, auditing defaults to
  on at `low` severity, and on the net10.0 leg it audits the **transitive** closure. A newly
  published advisory against any pinned package — or anything it drags in — fails the build with no
  repo change. Respond with a targeted `NuGetAuditSuppress` for the specific advisory URL, or
  `WarningsNotAsErrors`; never a blanket `NoWarn`.

One measurement caveat that is *not* a warning: `coverlet.collector`'s only MSBuild hook registers a
**VSTest** data collector, and MTP-native `dotnet test` does not run the VSTest pipeline. The
reference is kept for consistency with `Enigma.Core`, but do not assume coverage is being collected —
MTP coverage needs `Microsoft.Testing.Extensions.CodeCoverage`. Measure before trusting a number.

## Conventions

- **Language/style.** C# 14, `Nullable=enable`, `ImplicitUsings=disable` (declare every `using`
  explicitly). File-scoped namespaces; `using`s outside the namespace with `System` first. Follow
  `.editorconfig`; do not introduce warnings.
- **Immutability.** Model types are sealed records with `init`-only members — a predecessor review
  finding. Services are sealed classes behind interfaces.
- **Error contract.** As specified above; it is part of the public API and is asserted by tests.
  Changing it is a breaking change.
- **Public surface.** Only `Enigma.Licensing` types — never an `Enigma.Core.*` or
  `Org.BouncyCastle.*` type — nor a `Ulid` or `DeviceId.*` one. The DI abstractions are the sole
  exception. Keep `PublicSurfaceIsolationTests` green.
- **No legacy compatibility.** Never add a reader, converter or fallback for Enigma.LicenseManager
  licenses or keys.
- **Secrets are `char[]`, and the caller owns them.** Nothing in this library modifies or clears a
  password array it is handed (the Enigma.Core convention); it clears only the copies it makes — the
  UTF-8 buffer and the derived key — in a `finally`. A test pins this.
- **Serialization is source-generated, so the library is trim- and AOT-safe** (`FEATURE-57BC-PHASE01`).
  Every document goes through `EnvelopeJsonContext` or `PayloadJsonContext` by way of a
  `JsonTypeInfo<T>` from `LicenseJson`. **Never call a `JsonSerializer` overload that takes bare
  `JsonSerializerOptions`** — those are attributed `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`,
  and `IsAotCompatible` on the net8.0+ legs turns them into build errors. A new document type needs a
  `[JsonSerializable]` entry on the right context and a `JsonTypeInfo` member on `LicenseJson`;
  registering it nowhere means it throws rather than falling back to reflection, which is the point.
- **Documentation.** Every public member carries XML docs; `GenerateDocumentationFile=true` is on
  from bootstrap, so a missing comment fails the zero-warning build. Per-category usage guides will
  live under `docs/guides/` (`FEATURE-57BC-PHASE02`).
- **Packaging metadata** (`PackageId`, `Version`, `Description`, …) landed at release time with
  `FEATURE-57BC-PHASE01`, per the `dotnet-release` skill — all twelve properties plus the packing
  `ItemGroup`. `<PackageReleaseNotes>` is the one still to come (`PHASE03`, beside `RELEASENOTES.md`).
  There are deliberately **no symbol properties** and no `GeneratePackageOnBuild`: a release ships one
  `.nupkg`, packed explicitly. The library and the app version independently, in their own csproj —
  there is no solution-wide `<Version>`; both read 1.0.0.
- **Docs stay honest.** When a phase lands, update this file, `docs/roadmap.md` and the plan status
  in the same unit of work; do not leave "planned" markers on shipped code.

## Dev workflow (tracked work)

This repo plans and tracks work through a house workflow:

- `docs/roadmap.md` — the single registry of every work item (`FEATURE-HHHH`, `BUG-HHHH`,
  `CODE-REVIEW-HHHH`; large items are split into `-PHASENN` phases).
- `docs/plan/<ID>.md` — the full plan for each item (the contract a build implements).
- `docs/done/<ID>.md` — a completion record per finished item/phase.

Each unit of work gets its own branch (`feature/…`, `bugfix/…`, `review/…`), cut from the current
`HEAD`. A unit is **done** only when the build is warning-free, the whole test suite passes, the
plan's acceptance criteria are met, the roadmap/plan statuses are updated, and the completion doc is
written. Commits are left to the maintainer.

The sequence is a hard dependency chain: `FEATURE-2FE8` (repo skeleton — **done**) →
`FEATURE-03A9` (licensing core library, 4 phases — **all done**) → `FEATURE-179C` (desktop app,
4 phases — **all done**) → `FEATURE-57BC` (v1.0.0 release, 4 phases — `PHASE01` **done**,
**PHASE02 next**).
