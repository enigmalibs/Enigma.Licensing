# FEATURE-179C-PHASE04 — Validate page

**Status:** DONE
**Branch:** `feature/feature-179c-phase04-validate-page`
**Plan:** `docs/plan/FEATURE-179C.md` (PHASE04)

## Summary

The last placeholder is gone: the validate page picks a licence and a public key, takes an optional expected
product and device (with the same **This machine** button the generate page has), and calls
`ILicenseValidator.ValidateAsync` on the two paths. What comes back is shown in three stacked cards — the
verdict, the document, and the licence — and the application now does end to end what the library does:
generate a pair, sign a licence with it, and say whether that licence holds.

**`FEATURE-179C` is complete with this phase**, and its item row is `DONE`.

Five things are worth recording because they are decisions rather than transcription:

- **Verdicts go to the card; exceptions go to the info bar.** This is the library's own split, honoured in the
  UI. Nothing a *licence* can be makes the validator throw, so all seven failures are verdicts and belong
  somewhere a user can read them twice. A missing file, a key document that is not a key, and the private half
  where the public one belongs are the opposite — the application's own mistake — and those are error
  notifications that leave the previous result alone. A valid licence gets **no** notification either: the green
  card is the report, and a banner saying the same thing would be noise.
- **Three severities, not two, and the split follows the library's boundary.** Authenticity is settled before
  anything the licence *says* is read, so a failure is either "this document cannot be trusted" —
  `InvalidFormat`, `UnknownAlgorithm`, `InvalidSignature`, painted red — or "authentic, and it does not apply
  here" — `NotYetValid`, `Expired`, `ProductMismatch`, `DeviceMismatch`, painted amber. An expired licence and a
  forged one are completely different conversations with a customer, and a page that painted both red would hide
  that.
- **The document's own two fields are read from the file by the page, separately from the verdict.** The
  library's path overload owns the verdict — it loads the key, parses the envelope and maps a parse failure onto
  `InvalidFormat` — but hands back a verdict, not the document it read. `algorithm` and `keyId` are worth showing
  anyway: on a signature failure a fingerprint that is *not* the key being checked against says "wrong key"
  rather than "forged licence", which is the single most useful diagnosis this page can offer. So the page opens
  the file a second time for those two fields, swallows both possible failures (both are already accounted for
  by the verdict), and cannot influence the verdict by doing so.
- **Nothing here runs on a background thread or behind the overlay**, unlike the other two pages. A public key
  is never encrypted, so there is no Argon2id derivation to pay for, and verifying is microseconds either side
  of two small file reads — the library's own `ConfigureAwait(false)` keeps what work there is off the UI thread
  already. `IsBusy` exists to stop a second click landing on the first, not to cover a wait. A blocking overlay
  would flash for less time than it takes to notice.
- **The licence card shows the instants as they are.** `ValidUntil` is the first instant a licence is *no
  longer* usable, so one issued "through the 31st" reads here as midnight on the 1st, with a line on the card
  saying so. Rounding it back into a friendlier day would mean this page and the generate page disagreeing about
  the same licence.

## Files/modules touched

**Created — application (`src/Enigma.Licensing.Desktop/`)**

- `ViewModels/LicenseVerdictSeverity.cs` — Success / Warning / Error, the application's own vocabulary. See
  *Deviations* for why it is not the control library's `InfoBarSeverity`.
- `ViewModels/LicenseVerdict.cs` — the mapping table: headline, explanation, the library's own message, and a
  severity, plus the three `Is…` flags the view binds as style classes. `For(LicenseValidationResult)` covers all
  eight outcomes and falls through to a rejection for a `LicenseFailure` member a later library version might
  add, rather than throwing on a page a user is looking at.
- `ViewModels/LicenseSummary.cs` — the read-only card's contents: instants formatted (local time, offset
  included, `yyyy-MM-dd HH:mm zzz`), absent optionals given words ("Not stated", "Any machine", "Immediately",
  "Never expires"), and `License.Claims` flattened into the `LicenseClaimEntry` rows the generate page already
  uses — a compiled binding cannot template a dictionary without naming a constructed generic type in XAML.

**Modified — application**

- `ViewModels/ValidateLicenseViewModel.cs` — the placeholder replaced by the page: three injected seams
  (`ILicenseValidator`, `IFileDialogService`, `INotificationService`), four commands, the two path fields with
  their flags, the two optional expectations, and the result trio.
- `Views/ValidateLicenseView.axaml` — two `SettingsCardExpander`s (files, expectations) over the library's
  `TextEditor`s, the accent button, then the verdict card and the two result cards.
- `App.axaml` — five styles: `Border.verdict` and its three severity variants over the theme's own status
  surface pairs (`Enigma{Success,Warning,Error}{Background,Border}Brush`), and `TextBlock.verdict` for the
  headline. The severity reaches XAML as three **bound style classes**
  (`Classes.success="{Binding Verdict.IsSuccess}"`), which is what keeps both the control library's severity enum
  and every brush key out of the page's ViewModel.

**Created — tests (`tests/Enigma.Licensing.Desktop.UnitTests/`)**

- `ViewModels/ValidateLicenseViewModelTests.cs` — 31 cases over the page against doubles: the arguments handed to
  the validator (including empty-means-skip and trimming), the field flags, the verdict and summary it shows, the
  document fields read off a real envelope written by the test, a file that is not an envelope, a missing file, a
  thrown failure becoming a notification and clearing any stale result, the busy state, the snapshot-before-await,
  and the dialogs.
- `ViewModels/ValidateLicenseRoundTripTests.cs` — 15 cases against the **real** key service, generator and
  validator, with only the platform seams doubled. The headline theory drives all three pages in sequence — keys
  page → generate page → validate page — once per algorithm family, and the rest cover expiry, a not-yet-valid
  window, a flipped payload byte, a substituted algorithm, an unimplemented algorithm, the wrong key, a product
  mismatch, a wildcard hit, a device lock from both sides, the empty-device trap, the private half, a missing
  licence and a key file chosen as the licence.
- `ViewModels/LicenseVerdictTests.cs` — 24 cases over the mapping table, including that no two failures share a
  headline and that exactly one severity flag is ever true.
- `ViewModels/LicenseSummaryTests.cs` — 15 cases over the projection. The instants are asserted by **parsing them
  back** rather than against literals: they render in local time, so a literal would pin the suite to one time
  zone.
- `LicenseSamples.cs` — licences and verdicts built by hand, at the suite root, on the library suite's
  `LicenseTestData` precedent. `LicenseValidationResult` has a public constructor, so a verdict that would
  otherwise need real cryptography costs one object.
- `TestDoubles/FakeLicenseValidator.cs` — records the four arguments, answers with a primed verdict, and can be
  told to throw or to pause mid-call.

**Modified — docs**

- `docs/roadmap.md`, `docs/plan/FEATURE-179C.md` — PHASE04 and the item itself to `DONE`.
- `CLAUDE.md` — the *Shipped* section, the project layout and the state paragraphs.

## Deviations & follow-ups

- **The verdict's severity is the application's own enum, not the control library's `InfoBarSeverity`**, which is
  what the plan's wording named. The verdict is painted on the *page* rather than shown in a notification, so the
  severity has to reach XAML — and `INotificationService` exists precisely so that "the severity enum, and the
  info bar itself" stay out of every page (CLAUDE.md records that as a decision). Introducing
  `InfoBarSeverity` on a page ViewModel would contradict it. `LicenseVerdictSeverity` carries the same three
  weights the plan meant, and reaches the view as bound style classes. Nothing in the plan's intent is lost.
- **The library's message is quoted rather than replaced.** The plan says "`LicenseFailure` mapped to a human
  message"; the card shows the mapped headline and explanation *and* the library's own message underneath,
  because that message carries the specifics no mapping can know — the instant a licence expired, the product it
  is really for, the fingerprint the signature was checked with. The wording of that message is documented as not
  part of the library's API, and nothing here branches on it.
- **No blocking overlay and no `Task.Run`**, where PHASE02 and PHASE03 both used them. Reasoned in *Summary*;
  `IBusyOverlayService` is therefore not injected into this page at all.
- **A verdict is not cleared when an input is edited.** It is labelled as the last check and replaced by the next
  one, matching the result cards on the other two pages. Clearing on edit was considered and rejected: reading the
  licence's real product off the card in order to type it into the expected-product field is a real workflow, and
  the card vanishing mid-keystroke would break it.
- **The end-to-end pass is automated rather than clicked.** This machine still has no input-automation tooling
  (`xdotool`, `wtype`, `ydotool`), the limitation PHASE01–PHASE03 each recorded, so the plan's "manual pass" was
  run as `ValidateLicenseRoundTripTests` instead: it drives the three **real** page ViewModels in sequence against
  the real library for both algorithm families, and covers the expired and tampered cases the criterion names.
  What was verified by hand is that the application starts, renders the new page and shuts down cleanly (see
  *Build/test evidence*). **A click-through of the finished application — all four pages, a real key, a real
  licence — is worth doing once before `FEATURE-57BC`**, and is the one thing no test here can stand in for.
- **The verdict card's own rendering is unverified visually.** The page renders (screenshot taken), but the
  verdict, document and licence cards only appear after a check, which needs a click. Their markup is verified by
  compilation — compiled bindings resolve every path or the build carries `AVLN2100` — and every brush key they
  name was confirmed to exist in `Enigma.Avalonia.Desktop`'s theme dictionary. Fold this into the click-through
  above.
- **The library still has no public way to read a licence out of an envelope** — PHASE03's follow-up, unchanged.
  This page does not need one (the validator hands the licence back), which incidentally shows the gap is only
  felt on the *signing* side.
- No line-ending (CRLF) inconsistency was observed in any file touched by this phase.

## Build/test evidence

```
DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
  → Build succeeded.  0 Warning(s)  0 Error(s)

DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal
  → 0 lines matching AVLN (XAML warnings bypass TreatWarningsAsErrors, so they are counted by hand)

DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
  → total: 1134   failed: 0   succeeded: 1134   skipped: 0   (13.7 s)
```

1134 cases, up from 1049: **85 new**, all in the desktop suite (298, up from 213). That suite still runs in about
two seconds. The real cryptography added is ML-DSA-44 throughout plus one RSA-2048 pair, generated in the
headline theory's RSA leg — the keys page's own 4096-bit default would have been paid for on every run.

**Manual verification (Linux/Wayland, Release):** the application starts, the shell resolves the new page — a
`ValidateLicenseViewModel` with three constructor dependencies now has to come out of the container, and a
missing registration would be a `Navigation failed` line — and it shuts down cleanly on `Application is shutting
down...` with no exception. A screenshot of the running application confirms the page itself renders as intended:
the rail, the heading, both `SettingsCardExpander`s with their editors, browse buttons, the **This machine**
button and all three hint lines, and the accent **Check the licence** button, laid out correctly in the dark
variant.

## Acceptance criteria

| Criterion | Met |
|---|---|
| `ValidateLicenseViewModel` + view: licence and public-key file pickers | Yes — open dialogs, each starting where the other file is |
| Optional expected ProductId / DeviceId, with a "use this machine" helper | Yes — empty means skip, trimmed, and the page says skipping passes |
| Validate via `ILicenseValidator` | Yes — the path overload, so the library owns the key loading and the parse |
| Prominent verdict: valid, or the failure reason as a human message + severity | Yes — headline, explanation and the library's message, in three severities |
| A read-only card of the parsed licence: id, product, owner, window, device, features, claims, algorithm, keyId | Yes — the licence's fields in one card, the document's two in another |
| Shown whenever the payload parsed, even for invalid licences | Yes — asserted against both a fake and the real validator |
| VM tests: verdict mapping per `LicenseFailure` | Yes — `LicenseVerdictTests`, member by member |
| VM tests: parsed-licence display on failure | Yes — product mismatch shows the licence's real product |
| VM tests: fake validator interactions | Yes — the four arguments, including empty-means-null |
| VM tests: missing-file handling → message not crash | Yes — against both the fake and the real validator |
| Build/tests green | Yes — 0 warnings, 0 AVLN, 1134/1134 |
| End-to-end pass: keys → licence → validate, both algorithms, plus expired and tampered | Yes, automated through the three real pages — see *Deviations* for what was and was not done by hand |
