# FEATURE-179C-PHASE03 — Generate page & license profiles

**Status:** DONE
**Branch:** `feature/feature-179c-phase03-generate-page`
**Plan:** `docs/plan/FEATURE-179C.md` (PHASE03)

## Summary

The second of the three placeholder pages is now real, and it is the one the issuer actually lives in. The
generate page takes a product identifier (the only required field), an owner, a device — with a **This
machine** button behind `DeviceIdentifier.Generate()` — an optional validity window from two calendar
pickers, a feature list and a claims list, then a private key file and its password, and finally a
destination. It signs on a background thread behind the blocking overlay through
`ILicenseGenerator.GenerateAndSaveAsync`, reports the outcome on the info bar, and keeps the new licence's
identifier, the signing pair's fingerprint and the file's path on a result card with a Copy button.

With it come **licence profiles**: `LicenseProfile` plus `ILicenseProfileStore`/`LicenseProfileStore`, one
small JSON file per saved form, loaded and saved through the file dialogs. A profile holds the form and the
signing key's *path*, and deliberately holds neither the password nor the destination.

Four things are worth recording because they are not obvious from the plan text:

- **The chosen end date is included in the window.** `ValidUntil` is the first instant a licence is *no
  longer* usable, so a picker set to the 31st becomes midnight at the end of the 31st — the licence works all
  day on the 31st and stops at midnight. The alternative reading (expire at the start of the chosen day)
  compiles equally well and generates a support ticket every time. `ValidFrom` is local midnight at the start
  of its day. Both are pinned by tests on either side of the boundary, including two against the real
  validator.
- **The licence identifier is read out of the payload, because there is no other way to get it.** The
  generator mints a ULID per call and the envelope carries it only inside the signed bytes;
  `LicenseEnvelope` exposes `Payload` as `byte[]` but nothing that reads a licence out of it, and `Ulid` is
  deliberately off the library's public surface, so the page cannot mint its own. `ReadLicenseId` therefore
  parses one property with `JsonDocument` and yields `null` if it cannot — the licence is on disk by then, and
  not being able to name it is no reason to report the issue as failed. **This is a small gap in the library's
  public surface**; see *Deviations & follow-ups*.
- **Claims are add-and-remove, never edit-in-place.** `LicenseClaimEntry` is an immutable record, and the add
  command refuses a name already in the list. That makes duplicate keys structurally impossible rather than
  merely unlikely: two editable rows sharing a name would collapse into one claim on the way into the licence,
  and the user would have typed something the licence does not carry. The cost is that changing a claim means
  removing and re-adding it.
- **`ILicenseProfileStore` throws where `IAppSettingsStore` swallows.** The preference file is the
  application's own and nobody asked for it, so falling back to defaults is right there. A profile is the
  opposite — the user picked this file from a dialog and is watching for the form to fill — so a file that is
  not a profile is reported on the info bar. Both stores are right; they answer to different owners.

## Files/modules touched

**Created — application (`src/Enigma.Licensing.Desktop/`)**

- `Models/LicenseProfile.cs` — the saved form: `formatVersion`, product, owner, device, two `DateOnly?`
  bounds, features, claims and the signing key's path. Null collections coerce to empty on the way in, as
  `License` does, because the file is hand-editable.
- `Models/ILicenseProfileStore.cs` / `Models/LicenseProfileStore.cs` — one JSON file per profile, camel-cased,
  indented, nulls omitted; parent directories created on save; a malformed or too-new file reported as
  `ArgumentException` with the parse failure preserved.
- `ViewModels/LicenseClaimEntry.cs` — one key/value row, immutable for the reason above.

**Modified — application**

- `ViewModels/GenerateLicenseViewModel.cs` — the placeholder replaced by the page. `ObservableValidator`, six
  injected seams, eleven commands, the request and profile mapping, and the background signing path.
- `Views/GenerateLicenseView.axaml` — a `SettingsCard` for the profile buttons and six
  `SettingsCardExpander`s (licence, validity, features, claims, signing, output) over the library's
  `TextEditor`s and two stock `CalendarDatePicker`s, the accent generate button, and the result card.
- `Services/IFileDialogService.cs` / `Services/FileDialogService.cs` — a `ShowOpenFileDialogAsync` alongside
  the save one. PHASE02 needed only saves; this page has to *pick* a key and *open* a profile. It returns a
  single path, and reports a cancellation and a selection with no local path alike — this application reads
  its documents by path, and a handle it cannot name is a file it cannot open.
- `Hosting/ServiceCollectionExtensions.cs` — `ILicenseProfileStore` as a singleton (it holds no state and
  caches nothing, so one instance serves every path the user picks).

**Created — tests (`tests/Enigma.Licensing.Desktop.UnitTests/`)**

- `ViewModels/GenerateLicenseViewModelTests.cs` — 40 cases over the page against doubles: request mapping,
  blank-optional handling, the window rules, the field flags, features and claims, the profile capture and
  apply, the dialogs, the password's lifetime, and the busy/overlay/error paths.
- `ViewModels/GenerateLicenseRoundTripTests.cs` — 10 cases against the **real** generator, key service,
  validator and profile store, writing real files to a temporary folder: this is where the phase's acceptance
  criterion is actually shown, including a licence that expires on either side of today, a device-locked one,
  an encrypted signing key, and a profile written by one page and used by another.
- `Models/LicenseProfileStoreTests.cs` — 15 cases: the round trip, what the file looks like on disk, the
  absence of anywhere to put a password, and every way a chosen file can turn out not to be a profile.
- `TestDoubles/FakeLicenseGenerator.cs`, `TestDoubles/FakeLicenseProfileStore.cs`.

**Modified — tests**

- `TestDoubles/FakeFileDialogService.cs` — an open-dialog arm with its **own** answer queue and request list.
  Sharing one queue with the save arm would have made a test's answers depend on the order the page happens to
  raise its dialogs in, which is exactly the coupling a double should not add.
- `Hosting/ServiceCollectionExtensionsTests.cs` — the profile store's lifetime and its resolvability.

**Modified — docs**

- `docs/roadmap.md`, `docs/plan/FEATURE-179C.md` — statuses, plus the plan amendment recorded below.

## Deviations & follow-ups

- **`LicenseProfile` does not carry an algorithm**, though the plan's design section said "signing-key *path*
  + algorithm". Raised before building and settled with the maintainer: `ILicenseGenerator` takes no
  algorithm, because the scheme belongs to the key and was fixed when the pair was generated, so the field
  could only ever be written and not read — and could silently disagree with the key file it names. The plan
  line was amended in place with the reason.
- **The generate button is *live* for a missing field and *disabled* for a contradictory window**, where the
  plan said "generate command disabled until valid". This follows PHASE02's documented rule rather than the
  plan's wording: a disabled button explains nothing, so a field the user simply has not filled in is flagged
  when they press the button (`ValidateAllProperties` plus the two path flags), while a window that ends
  before it starts disables it and says why under the dates — the same shape as the keys page's password
  confirmation, and for the same reason, that it is a contradiction rather than an omission. The plan's
  `ObservableValidator` annotations are used as asked; they are what drives the product field's error text.
- **The library has no public way to read a licence out of an envelope.** `LicenseEnvelope.Payload` is public
  `byte[]`, and `ILicenseValidator` is the only thing that turns it into a `License` — which needs the
  *public* key, and this page holds the private one. The page therefore reads the `id` property directly with
  `JsonDocument`. It works and is documented, but a consumer wanting the identifier of a licence they just
  issued has to do the same thing. **Worth considering at `FEATURE-57BC-PHASE01`** alongside the trim/AOT
  decision — a `LicenseEnvelope.ReadLicense()` would close it, at the cost of a method whose result is
  unverified and must be documented as such.
- **The output path is not part of a profile.** The plan says "form fields + signing-key path"; a destination
  belongs to one issue rather than to the kind of licence a profile describes, so each issue picks its own.
  Loading a profile also **clears the password field**, because the profile has just named a key — possibly a
  different one — and a password typed for the previous key is no longer the password for anything.
- **Claims cannot be edited in place** — see *Summary*. The plan says "key/value grid add/remove", which is
  what shipped; the grid is an `ItemsControl`, not a `DataGrid`, since `Avalonia.Controls.DataGrid` is not
  referenced and adding a package for two columns of read-only text would not be earned.
- **`CalendarDatePicker`, not `DatePicker`.** The dates are individually optional and must be clearable, and
  `DatePicker` offers no way to unset a value once one is chosen. `CalendarDatePicker` has an empty state with
  a placeholder, which is what an optional date needs.
- **`FileDialogService.ShowOpenFileDialogAsync` is covered by registration and resolution only**, as its save
  sibling was in PHASE02, and for the same reason: the untested part would need a stub of Avalonia's
  thirteen-member `IStorageProvider`.
- **No manual click-through of the generate flow was possible** — this machine still has no input-automation
  tooling (`xdotool`, `wtype`, `ydotool`), the limitation PHASE01 and PHASE02 both recorded. What replaces it
  is the ten-case round-trip suite against the real library. The end-to-end manual pass is **PHASE04's**
  explicit acceptance criterion, where a validate page will exist to do it with.
- No line-ending (CRLF) inconsistency was observed in any file touched by this phase.

## Build/test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded.  0 Warning(s)  0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal
  → 0 lines matching AVLN (XAML warnings bypass TreatWarningsAsErrors, so they are counted by hand)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → total: 1049   failed: 0   succeeded: 1049   skipped: 0   (12.9 s)
```

1049 cases, up from 976: **73 new**, all in the desktop suite (213, up from 140). That suite still runs in
about two seconds — the real cryptography in it is ML-DSA-44 throughout, plus the two Argon2id derivations the
encrypted-signing-key test pays for deliberately.

**Manual verification (Linux/Wayland, Release, `dotnet run`):** the application starts and shuts down cleanly
— `Application started` / `Application is shutting down` from `Microsoft.Hosting.Lifetime`, with no
`Navigation failed` line and no exception — which is the evidence that the new `ILicenseProfileStore`
registration did not break the container and the shell still resolves. The page itself is verified by
compilation (compiled bindings resolve every path, including the `$parent[ItemsControl]` casts in the two item
templates, or the build would carry `AVLN2100`) and by the test suites above.

## Acceptance criteria

| Criterion | Met |
|---|---|
| Form: ProductId (required), Owner, DeviceId + "use this machine" | Yes |
| Optional ValidFrom / ValidUntil pickers, each independently optional | Yes — `CalendarDatePicker`, clearable, tested with each end set alone |
| ValidFrom ≤ ValidUntil validated | Yes — disables the command and reports under the dates |
| Features editor (add/remove) | Yes — trimmed, ordinal, no duplicates |
| Claims editor (key/value add/remove) | Yes — immutable rows, so a duplicate name is impossible |
| Private key file picker + password field | Yes — an open dialog, and the password cleared in a `finally` |
| Generate & save via `ILicenseGenerator`, suggested `<product>.license.json` | Yes — wildcards and invalid characters stripped from the name |
| `Models/LicenseProfile` — form fields + key path, never the password | Yes — and no algorithm, per the amendment above |
| Profiles save/load as `.json` via file dialogs; loading prefills the form | Yes |
| `ObservableValidator` annotations; command blocked until valid | Yes — with the live-vs-disabled split described in *Deviations* |
| InfoBar success (with license id) / error | Yes — and the identifier also lives on the result card, with Copy |
| VM tests: request mapping, window order, profile excludes password, generator invocation, device-id fill | Yes |
| Build/tests green | Yes — 0 warnings, 0 AVLN, 1049/1049 |
| A licence generated in the app validates in the app and via the library API | Yes — `GenerateLicenseRoundTripTests`, against the real validator |
