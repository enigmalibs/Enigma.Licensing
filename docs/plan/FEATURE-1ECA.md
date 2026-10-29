# FEATURE-1ECA — New application icon

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-1eca-new-app-icon`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Replace the desktop application's icon with the new Enigma Licensing artwork from the sibling Branding
repository, `C:\Dev\EnigmaLibs\Branding\Apps\final\licensing\`.

## Context & constraints

- **The new set:** `enigma-licensing.ico` (25,761 bytes), `enigma-licensing.svg` / `-small.svg` (the
  masters) and `png/enigma-licensing-<N>.png` for N = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256. It is
  the Branding repository's output, as Enigma.GitClient's FEATURE-F7B5 icon was (ten PNG frames, each
  byte for byte the matching PNG).
- **Where the icon lives today:** `src/Enigma.Licensing.Desktop/Assets/appicon.ico` only — the
  `<ApplicationIcon>` (the Windows `.exe`) and `MainWindow`'s `Icon="/Assets/appicon.ico"` (the title bar
  and taskbar on every platform). FEATURE-7310 authored it (a white key on a `#3574F0` square, seven
  uncompressed 32-bpp DIB frames, ~364 KB) with a throwaway script; CLAUDE.md records its geometry and
  its *Project layout* line describes it.
- No test pins the icon today. FEATURE-0156 adds the output copy and its test; FEATURE-CF21 adds the
  256 px PNG for the About dialog; FEATURE-F319 adds the Linux hicolor PNGs — all three from this same set.

## Steps

1. Copy `enigma-licensing.ico` → `src/Enigma.Licensing.Desktop/Assets/appicon.ico`, byte for byte.
2. Inspect it with a throwaway script in the scratchpad: frame count, sizes, bit depth and encoding; and
   that each frame is byte-identical to the matching `png/enigma-licensing-<N>.png` (recorded — later devs
   rely on it).
3. Look at the 16, 32 and 256 px frames (rendered) — the artwork is legible at title-bar size.
4. `CLAUDE.md`: the FEATURE-7310 section gains a line that the artwork was replaced by this item (frames,
   source, generator), and the *Project layout* line for `Assets/appicon.ico` describes the new icon.
5. Release build warning-free (AVLN 0); the whole suite.

## Acceptance criteria

- `Assets/appicon.ico` is byte-identical to the Branding `.ico`; the old icon is gone.
- The csproj and `MainWindow.axaml` still name `Assets/appicon.ico` and resolve it (the build embeds it;
  a launched app shows it in the title bar — checked once, by a launch).
- Release build warning-free; the whole suite green.

## Out of scope

- The SVG masters in the repository (nothing here consumes them; the Branding repo is their home).
- The output-directory copy (FEATURE-0156), the About PNG (FEATURE-CF21), the Linux PNGs (FEATURE-F319).
- The version and release notes (FEATURE-B6BB).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which `.ico` | The designer's file, all frames, unchanged | Windows picks the exact frame at 125 %/150 % (20, 24, 40); GitClient's F7B5 did the same | Re-packing to FEATURE-7310's seven DIB frames |
| PNG vs DIB frames | Accept PNG frames | Every sibling app ships PNG-frame `.ico`s and renders them in the title bar; FEATURE-7310's DIB choice was caution about an unknown, not an observed failure | Converting frames to DIB |
| Asset file name | Keep `appicon.ico` | The csproj, `MainWindow.axaml` and CLAUDE.md name it; the uniform `app.ico` name is the output copy's (FEATURE-0156) | Renaming to `app.ico` |
| SVG masters | Not copied | No consumer in this repo; narrowest reading of "update app icons" | `packaging/icon/` as GitClient |
| Test | None in this dev; FEATURE-0156's test pins the shipped bytes | An asset swap has no behaviour to test | A frame-shape test reading the source tree |
