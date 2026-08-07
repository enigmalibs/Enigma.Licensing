# FEATURE-03A9-PHASE04 — DI registration, API polish & XML docs

**Status:** DONE
**Branch:** `feature/feature-03a9-phase04-di-polish`
**Plan:** `docs/plan/FEATURE-03A9.md` (PHASE04)

## Summary

The last piece of the licensing core library: `AddEnigmaLicensing()` registers all four services, and a
reflection guard now enforces the isolation invariant the previous three phases have been asserting in
prose. With this, **FEATURE-03A9 is complete** — the library generates, saves, loads and validates
signed licenses under all six signature schemes, and its public surface is machine-checked to expose
nothing but its own types.

Three things are worth recording because they are not obvious from the plan text:

- **The store is registered through an explicit factory, not `TryAddSingleton<ILicenseStore, LicenseStore>()`.**
  The container would pick `LicenseStore`'s greediest satisfiable constructor, which *happens* to be the
  right one — `LicenseStore(ILicenseValidator)`. Naming the dependency makes it a decision instead of an
  accident, so a consumer who registers their own `ILicenseValidator` gets a store that validates
  through it. `APreRegisteredValidatorIsWhatTheStoreValidatesWith` is the test that would catch a
  regression here; asserting only the validator's own identity would not.
- **The guard test needed two arms, because the invariant has two halves that fail differently.** The
  namespace arm states the documented rule (`Enigma.Core.*`, `Org.BouncyCastle.*`) and names offenders in
  the language the docs use. The assembly arm is what actually closes the hole: **`Ulid` is declared as
  `System.Ulid`**, so no namespace rule could ever tell it apart from a framework type, and a dependency
  added *later* would walk straight past a denylist written before it existed. The assembly arm is an
  allowlist — `Enigma.Licensing`, the framework, and `Microsoft.Extensions.DependencyInjection.Abstractions`
  — so a future leak fails by default rather than needing the guard to be updated first.
- **The guard was verified by making it fail.** A throwaway `LeakProbe.cs` exposing an
  `MLDsaParameterSet` and a `Ulid` on public members was added, both arms were confirmed to report the
  exact offending member, and the probe was deleted. This mattered: the run showed the namespace arm
  reporting only the Enigma.Core leak while the assembly arm caught both — the empirical justification
  for the second test. A third test, `TheWalk_ReachesEveryKindOfMemberItClaimsTo`, pins the walk itself,
  because the failure mode of a reflection guard is passing vacuously.

**`Microsoft.Extensions.DependencyInjection.Abstractions` is now the one dependency deliberately visible
on the public surface** — `AddEnigmaLicensing()` takes and returns `IServiceCollection`, and an extension
method that hid its own extended type would be useless. It earns the exception by being a contract
package: abstractions only, no concrete container, and already present in any application doing DI. The
allowlist in the guard test records that reasoning at the point where it is enforced.

## Files / modules touched

### Created — library

| Path | What it is |
|---|---|
| `src/Enigma.Licensing/EnigmaLicensingServiceCollectionExtensions.cs` | `AddEnigmaLicensing()` — `TryAddSingleton` ×4, in the `Microsoft.Extensions.DependencyInjection` namespace |

### Modified — library

- `src/Enigma.Licensing/Enigma.Licensing.csproj` — added the unconditional
  `Microsoft.Extensions.DependencyInjection.Abstractions` reference with the reason it is the one
  surface-visible dependency; replaced the now-stale "guarded by a reflection test in
  FEATURE-03A9-PHASE04" comment with what actually ships.
- `src/Enigma.Licensing/Keys/KeyService.cs`, `LicenseGenerator.cs`, `LicenseValidator.cs` — the three
  "which is why the DI registration makes it a singleton" remarks were forward references written before
  the method existed. They now `<see cref="…AddEnigmaLicensing"/>` it, so IntelliSense links a consumer
  from the service to the way to register it.
- `src/Enigma.Licensing/LicenseStore.cs` — the `LicenseStore(ILicenseValidator)` remark now names
  `AddEnigmaLicensing()` as the caller and states the consequence: registering a custom
  `ILicenseValidator` changes what the store validates with.
- `Directory.Packages.props` — pinned `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.18
  (library) and `Microsoft.Extensions.DependencyInjection` 9.0.18 (tests); removed the abstractions from
  the "still to land" comment.

### Modified — documentation (freshness sweep)

- `CLAUDE.md` — full refresh, nine stale spots. *Current state* now says the item is complete and feature-
  done, and carries the trim/AOT decision forward as explicitly outstanding; a *Shipped* section for
  PHASE04 records the five load-bearing decisions; *Architecture & conventions* no longer warns that some
  of it describes design; the *Services (all four ship) & DI (**PHASE04 — planned**)* heading becomes
  *Services & DI*; the isolation invariant now names the DI-abstractions exception and
  `PublicSurfaceIsolationTests` instead of promising a test "once it exists" (in both places it appeared);
  the project-layout tree gains the extension, the guard test and the `DependencyInjection/` folder, and
  its "nothing marked planned except…" sentence is corrected from two directories to four; the dependency
  section pins the abstractions and the tests' concrete container, adds the netstandard2.0 transitives,
  and closes the library's runtime set instead of listing something "still to land"; the trim/AOT
  convention names FEATURE-57BC-PHASE01 rather than "PHASE04 or the release phase"; the fixture note
  records 836 cases at ~13 s and that the DI suite joins the `signed-licenses` collection; and the
  dependency chain reads **all done** with FEATURE-179C marked next. `README.md` and `RELEASENOTES.md`
  are deliberately empty until FEATURE-57BC-PHASE03 and were left alone.

### Created — tests

| Path | What it covers |
|---|---|
| `tests/…/Api/PublicSurfaceIsolationTests.cs` | The two-arm reflection guard + the walk's own self-check |
| `tests/…/DependencyInjection/EnigmaLicensingServiceCollectionExtensionsTests.cs` | Resolution, implementation types, singleton lifetime, shared store, chaining, null guard, `TryAdd` idempotence and survival, and two end-to-end round-trips through resolved instances |

### Modified — tests

- `tests/Enigma.Licensing.UnitTests/Enigma.Licensing.UnitTests.csproj` — added the concrete
  `Microsoft.Extensions.DependencyInjection` reference. The library takes only the abstractions, so the
  registration tests bring the container themselves; that is the only way to build a real
  `ServiceProvider` and assert what the extension put in it.

## Final dependency set (PHASE01 asked for this to be logged once the item closed)

**Runtime, all TFMs:** `Enigma.Core` 1.1.0 (→ `BouncyCastle.Cryptography` 2.7.0),
`Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.18, `Ulid` 1.4.1, `DeviceId` 6.11.0.
**netstandard2.0 only:** `System.Text.Json` 10.0.10, `System.Buffers` 4.6.1, and `PolySharp` 1.16.0
(compile-only). Referencing System.Text.Json or System.Buffers on net8.0+ raises NU1510 and fails the
zero-warning build — both are framework-provided there.
**netstandard2.0 transitives new to this phase:** `Microsoft.Bcl.AsyncInterfaces` 9.0.18 and
`System.Threading.Tasks.Extensions` 4.5.4, from the DI abstractions. Both are inbox on net8.0+.

The 9.x floor is deliberate and matches Enigma.DataEncryption. NuGet resolves a package to the highest
floor any project in the graph asks for, so a high floor here would drag a consumer's whole
`Microsoft.Extensions.*` set forward — the opposite of what a library should do.

## Deviations & follow-ups

**Deviations from the plan — one, and it is a naming choice:**

- The extension class is named `EnigmaLicensingServiceCollectionExtensions`, as the plan's API sketch
  specified, rather than the bare `ServiceCollectionExtensions` that Enigma.DataEncryption uses. The
  plan's name is the better one and the reason is recorded in the class's own XML docs: this class shares
  the `Microsoft.Extensions.DependencyInjection` namespace with every other library's registration
  extensions, so an application referencing both Enigma libraries would have two identically named types
  in one namespace — harmless when calling the method, ambiguous for anyone who names the type. Worth
  knowing that the two sibling repos differ here on purpose.

**Additions the plan did not spell out**, each because the alternative was worse:

- **The second guard arm (assembly allowlist).** The plan asked for one reflection walk rejecting
  `Org.BouncyCastle.*` and `Enigma.Core.*`. That is necessary but not sufficient: `Ulid` lives in the
  `System` namespace, and `CLAUDE.md`'s invariant names `Ulid` and `DeviceId.*` alongside the other two.
  A namespace denylist cannot express that; an assembly allowlist can, and it also fails closed for
  dependencies not yet added.
- **`TheWalk_ReachesEveryKindOfMemberItClaimsTo`.** Three assertions pinning one arm each of the walk
  (a method return, a method parameter, an implemented interface), so a wrong `BindingFlags` combination
  or a renamed assembly cannot turn the guard into a test that inspects nothing and passes forever.
- **The two end-to-end tests in the DI suite** (`TheResolvedServicesRoundTripALicense`,
  `TheResolvedKeyServiceGeneratesAUsablePair`). A registration checklist proves the container can
  construct four objects; it does not prove they compose. These generate, save, load, validate and store
  a license using nothing but resolved instances.

**The plan's item 4 (API review pass) found nothing to change.** Recorded here so the pass is not
mistaken for having been skipped: all five implementation classes are `sealed`
(`KeyService`, `LicenseGenerator`, `LicenseValidator`, `LicenseStore`, `LicensingKey`), `DeviceIdentifier`
and the new extension class are `static`; `LicensingKey`'s only constructor is `internal`, which is what
keeps key material off the public API; the sole two `!` null-forgiveness operators in the library
(`LicenseGenerator.BuildLicense`, assigning `Features`/`Claims`) already carry the comment explaining
that `License` coerces a null itself. Item 2's doc pass is likewise enforced rather than asserted:
`GenerateDocumentationFile` + `TreatWarningsAsErrors` make CS1591 an error and there is **no `NoWarn`,
`WarningsNotAsErrors` or `#pragma` suppression anywhere in the repo**; the generated XML holds 196
documented members, and the only 15 without a `<summary>` are the implementations' `<inheritdoc />`
members, which is correct.

**Follow-ups:**

- **The trim/AOT decision is now due, and is deliberately deferred to FEATURE-57BC (release).** The model
  is complete as of this phase, which `CLAUDE.md` named as the moment to decide. Serialization is still
  reflection-based, so a consumer publishing with `PublishAot` — or with
  `JsonSerializerIsReflectionEnabledByDefault=false` — gets `InvalidOperationException` on the first
  serialize. The fix is a source-generated `JsonSerializerContext` wired into `LicenseJson`, covering
  both the license *and* key documents. It is not in this phase's plan and was not smuggled in; it
  belongs in FEATURE-57BC-PHASE01 alongside the packaging metadata, where the decision can be taken
  against the released surface. **Do not let v1.0.0 ship without answering it.**
- `Microsoft.Bcl.AsyncInterfaces` 9.0.18 and `System.Threading.Tasks.Extensions` 4.5.4 join
  `Microsoft.DotNet.PlatformAbstractions` 3.1.6 in the netstandard2.0-only transitive closure — three
  packages for FEATURE-57BC-PHASE01's third-party licence audit to cover. Restore and the NuGet audit are
  clean today.
- The `Api/` test folder now holds both API-behaviour suites and the isolation guard. If it grows further,
  a `Surface/` folder would separate "what the library does" from "what the library exposes" — cosmetic,
  and not worth a move today.
- No line-ending (CRLF) inconsistency was observed in any touched file; nothing to recommend.

## Build / test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded. 0 Warning(s), 0 Error(s)
    (from a deleted obj/bin, across netstandard2.0 / net8.0 / net10.0)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → Passed!  total: 836  failed: 0  succeeded: 836  skipped: 0   (13s)
    net8.0 passed (13.4s) · net10.0 passed (12.2s)
```

**836 test cases, up from 790 at PHASE03 — 46 added** (23 per TFM: 20 registration, 3 isolation). Suite
runtime is unchanged (~13 s); the DI suite joins the `signed-licenses` collection rather than generating
keys of its own, so it adds no key generation beyond one cheap ML-DSA-44 pair.

**Guard-test negative control** (temporary `LeakProbe.cs`, since deleted):

```
failed …PublicSurfaceIsolationTests.NoExportedMember_ExposesAnEnigmaCoreOrBouncyCastleType
  Enigma.Licensing.LeakProbe.CoreLeak (return): Enigma.Core.Asymmetric.Pqc.MLDsaParameterSet
failed …PublicSurfaceIsolationTests.NoExportedMember_ExposesATypeFromAnUnexpectedAssembly
  Enigma.Licensing.LeakProbe.CoreLeak (return): …MLDsaParameterSet (from Enigma.Core)
  Enigma.Licensing.LeakProbe.UlidLeak  (return): System.Ulid            (from Ulid)
```

### Acceptance criteria — PHASE04

| Plan criterion | Evidence |
|---|---|
| `AddEnigmaLicensing()`, `TryAddSingleton` ×4, + M.E.DI.Abstractions pin (low floor, 9.x) | `EnigmaLicensingServiceCollectionExtensions`; abstractions pinned at 9.0.18, matching DataEncryption |
| XML docs on every public member (defaults, exception contracts, `char[]` ownership) | CS1591 is an error and unsuppressed; 196 documented members; defaults documented on `rsaKeySizeBits` (3072), the generator's ULID/`UtcNow` stamping, and the null-skips `productId`/`deviceId`; `char[]` ownership on `IKeyService` and `ILicenseGenerator`'s path overload |
| Public-surface guard test (no `Org.BouncyCastle.*`, no `Enigma.Core.*`) | `PublicSurfaceIsolationTests` — two arms, plus `Ulid`/`DeviceId` coverage the plan's namespace rule could not reach; proven to fail on a real leak |
| API review pass (sealed, internal ctors, naming, no bare `!`) | Audited; nothing to change — see *Deviations & follow-ups* |
| Tests: DI resolution ×4, singleton lifetime, TryAdd idempotence, guard | 20 registration cases + 3 isolation cases, per TFM |
| Build + tests green, zero warnings | Above |

### Acceptance criteria — FEATURE-03A9 (item level, closing with this phase)

| Item criterion | Evidence |
|---|---|
| All four phases done | PHASE01–04 all `DONE` |
| Zero-warning, green build and test run across TFMs | Above; netstandard2.0 / net8.0 / net10.0 build, net8.0 / net10.0 test |
| A license generated with each of the six algorithms round-trips save → load → validate | `LicenseValidatorTests.Validate_AcceptsALicenseRoundTrippedThroughAFile`, over `SignedLicenseFixture.AllAlgorithms` (PHASE03) |
| The predecessor pain-point list is demonstrably addressed | No canonicalization step (`LicensePayload` signs the stored bytes); `format` + `algorithm` + `keyId` explicit in both envelopes; immutable `init`-only records; `Features` + `Claims`; all four services interfaced and DI-registered; parent-directory creation via `DocumentFile`; System.Text.Json throughout; `DateTimeOffset` with explicit offsets; and the wildcard bug fixed by escaping before substituting (`ProductIdMatch`) |
