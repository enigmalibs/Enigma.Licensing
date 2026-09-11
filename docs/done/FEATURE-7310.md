# FEATURE-7310 — Branded desktop application icon

**Status:** DONE
**Branch:** `feature/feature-7310-app-icon`
**Run:** feature/2026-09-11-icon-trim-release

## Summary

`src/Enigma.Licensing.Desktop/Assets/appicon.ico` was placeholder branding inherited from the
predecessor application. It is now a **white key on a `#3574F0` rounded square**, in the idiom
Enigma.MarkdownEditor established, with seven frames from 16 px to 256 px.

The icon is the whole of the change. Both places that reference it — `<ApplicationIcon>` in
`Enigma.Licensing.Desktop.csproj` and `Icon="/Assets/appicon.ico"` on `MainWindow` — already pointed at
this path and were not touched, so nothing but the bytes moved. No C# file, no `.axaml` file and no test
changed.

## The artwork, recorded

The generator was a throwaway (decision 5 of the plan), so the geometry is written down here — this is
what a later redesign needs, rather than a script nobody re-runs. Every number is a fraction of the
canvas, which is why all seven frames are the same picture.

**Container** — measured out of `Enigma.MarkdownEditor`'s `app-256.png`, not guessed:

| | Value (at 256) | As a fraction |
|---|---|---|
| Square inset | 7 px on each side | 0.0273 |
| Corner radius | 44 px | 0.1719 |
| Fill | `#3574F0` | — |
| Outside the square | alpha 0 | — |

**Glyph** — white, `#FFFFFF`, drawn as one winding-filled path (so the overlapping parts union with no
internal seam), with the bow's hole filled back in blue on top:

| Part | Geometry (at 256) |
|---|---|
| Bow, outer | circle, centre (90, 124), radius 54 |
| Bow, hole | circle, same centre, radius 26 |
| Shaft | rectangle x 90 → 220, y 110 → 138 |
| First tooth | rectangle x 144 → 168, y 138 → 186 |
| Second tooth | rectangle x 196 → 220, y 138 → 172, flush with the shaft's tip |

The glyph's bounding box is x 36 → 220, y 70 → 186 — centred on (128, 128) to the pixel.

## Files/modules touched

**Modified**

- `src/Enigma.Licensing.Desktop/Assets/appicon.ico` — replaced. 372,526 bytes, 7 frames, all 32-bpp.
  Already covered by `.gitattributes`' `*.ico binary`, so git never converted it.
- `CLAUDE.md` — the project-layout line no longer calls the icon placeholder branding, and a *Shipped*
  section records the container/glyph split, the frame set and the encoding choice.
- `docs/roadmap.md`, `docs/plan/FEATURE-7310.md` — statuses.

**Created**

- `docs/done/FEATURE-7310.md` — this file.

**Not committed** (scratchpad only, per decision 5): the PowerShell generator, the 256 px preview, and
the magnified 16/24/32 frame sheet used to judge small-size legibility.

## Decisions taken at build time

1. **The plan's glyph geometry was drawn as specified and kept after looking at it.** Two renderings
   were inspected before the icon was installed — the 256 px preview, and a nearest-neighbour
   magnification of the decoded 16, 24 and 32 px frames. At 16 px the ring's hole, the shaft and the gap
   between the two teeth all survive, which was the criterion the glyph was chosen on; nothing needed
   adjusting, so nothing was.
2. **The second tooth is flush with the shaft's tip** rather than inset. An inset tooth would leave an
   8 px stub of bare shaft past it at 256 — half a pixel at 16 px, which renders as a smudge rather than
   as a detail. Flush gives the key a solid tip at every size.
3. **The bow's hole is filled blue over the white glyph, not subtracted from it.** An even-odd
   subtraction would leave the hole's edge antialiased against transparency and then composited over the
   square; filling blue on top antialiases white against blue directly, which is the two colours that
   actually meet there.
4. **`biSizeImage` is written rather than left 0.** Zero is legal for `BI_RGB`, but some decoders use the
   field to find the AND mask; writing it costs nothing and removes a decoder question from an asset that
   has to render in Explorer, the taskbar and Avalonia alike.

## Deviations & follow-ups

- **No deviation from the plan.** All seven acceptance criteria were met as written.
- **Follow-up, not done and not needed here:** the Linux artifact carries no icon, because
  `scripts/publish.sh` ships the executable, `README.md` and `LICENSE.md` and nothing else. A `.desktop`
  entry and a PNG would be a packaging work item, out of scope by the plan.
- **Line endings:** this dev's diff is one binary file plus markdown; no CRLF/LF churn appeared, and per
  the workflow's recommendation-only rule nothing was done about line endings either way.

## Build/test evidence

- `DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release` — **succeeded, 0 warnings,
  0 errors**. `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are solution-wide, so a warning would
  have failed it.
- `dotnet build src/Enigma.Licensing.Desktop/... -c Release --no-incremental -v normal | grep -c AVLN`
  → **0**. Checked by hand because XAML warnings bypass `TreatWarningsAsErrors`.
- `DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release` — **736 passed, 0
  failed** on the net10.0 legs (438 library + 298 desktop). The run reports `error: 1` and a non-zero
  exit code because this machine has no .NET 8 runtime and the library suite's net8.0 leg cannot launch —
  the condition `CLAUDE.md` documents, not a red suite.
- The net8.0 leg, run the documented way
  (`DOTNET_ROLL_FORWARD=LatestMajor ./Enigma.Licensing.UnitTests.exe` from `bin/Release/net8.0`) —
  **Total: 438, Errors: 0, Failed: 0, Skipped: 0**.
- Total across both legs and both suites: **1174 passing**, unchanged from before this dev, as expected
  for a change no test can reach.
- **Icon verified by decoding the committed file**, not by trusting the writer: the directory carries 7
  entries — 16, 24, 32, 48, 64, 128 and 256 (written as `0`, per the format) — each `bpp=32`, with byte
  counts matching `40 + w·h·4 + AND-mask` exactly (1,128 at 16 px; 270,376 at 256 px). The decoded
  frames were re-rendered and inspected.
