# FEATURE-1ECA — New application icon

## Summary

`src/Enigma.Licensing.Desktop/Assets/appicon.ico` is now the family's new Enigma Licensing artwork: a
certificate with a green seal on a dark rounded square (LIC-A2, *Soft* palette). It replaces
FEATURE-7310's white key, and is byte for byte the Branding repository's
`Apps/final/licensing/enigma-licensing.ico`. The csproj (`<ApplicationIcon>`) and `MainWindow.axaml`
(`Icon="/Assets/appicon.ico"`) are untouched: only the bytes moved.

## Files/modules touched

- Modified:
  - `src/Enigma.Licensing.Desktop/Assets/appicon.ico` (binary: 372,526 → 25,761 bytes)
  - `CLAUDE.md` (the FEATURE-7310 section gains a *Superseded* bullet; the *Project layout* line)
  - `docs/roadmap.md`, `docs/plan/FEATURE-1ECA.md`
- Created: `docs/done/FEATURE-1ECA.md`

## Deviations & follow-ups

- None from the plan.
- The `.ico` holds ten 32-bpp **PNG** frames: 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256. Each is
  byte-identical to the matching `png/enigma-licensing-<N>.png`. FEATURE-CF21 (About 256 px PNG) and
  FEATURE-F319 (Linux hicolor PNGs) can therefore take the Branding PNGs directly.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Where the redesign lives | CLAUDE.md says the Branding repository (`export.py`), never here | The SVG headers name that tool as the generator. An edit here would be lost on the next export |

## Build/test evidence

- Frame inspection, with a throwaway script in the session scratchpad that parses the ICO directory: 10
  frames, all PNG, 32 bpp, sizes as above. Each is identical to its Branding PNG. The old file had 7 DIB
  frames.
- `cmp` of `Assets/appicon.ico` against the Branding `.ico`: identical.
- Visual check of the 256, 32 and 16 px PNGs: the certificate and seal read at every size. The 16 px
  frame is the simplified master's.
- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**. No `.axaml` was touched.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **712/712 passed**.
- Launch check: the Release `Enigma.Licensing.Desktop.exe` was started and its window captured with
  `PrintWindow`. The title bar shows the new certificate icon. The process was closed after the capture.

## Documentation sweep

- `CLAUDE.md`: the FEATURE-7310 section and the layout line described the old key icon. Fixed.
- README: does not describe the icon. Nothing to do.
