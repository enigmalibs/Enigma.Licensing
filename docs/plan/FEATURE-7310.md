# FEATURE-7310 — Branded desktop application icon

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-7310-app-icon`
**Run:** feature/2026-09-11-icon-trim-release

## Objective

Replace the desktop application's placeholder icon with real branding in the house idiom established by
**Enigma.MarkdownEditor**: a white glyph on a blue rounded square, transparent outside the square.

## Context & constraints

`src/Enigma.Licensing.Desktop/Assets/appicon.ico` is, by its own `CLAUDE.md` entry, *"placeholder
branding, reused from the predecessor app"*. It is wired up in exactly two places, and both stay as they
are — only the bytes behind them change:

- `Enigma.Licensing.Desktop.csproj` → `<ApplicationIcon>Assets/appicon.ico</ApplicationIcon>` — stamps
  the icon into the Windows `.exe` so Explorer and the taskbar show it when the app is not running.
- `Views/MainWindow.axaml` → `Icon="/Assets/appicon.ico"` — the running window's icon, on every platform,
  picked up through the existing `<AvaloniaResource Include="Assets/**" />` glob.

**The reference artwork was measured, not guessed.** `Enigma.MarkdownEditor`'s
`src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png` was decoded and sampled:

| Property | Measured value |
|---|---|
| Canvas | 256 × 256, 32-bit RGBA |
| Square fill | `#3574F0`, fully opaque |
| Glyph | `#FFFFFF`, fully opaque |
| Outside the square | alpha 0 |
| Square bounds | x/y 7 → 249 (a 242 px square inset 7 px on a 256 canvas) |
| Corner radius | ≈ 44 px — the straight top edge begins at x ≈ 51 |

That repository holds **no vector source** for its icon (its own completion record says the PNG was
decoded out of the `.ico`, which itself carries "four uncompressed 32-bpp BMP frames"), so the artwork
here is produced from scratch rather than traced.

The one hard constraint on the output is that **Avalonia/Skia's choice of frame inside an `.ico` is
unspecified**, so every frame must be the same picture and must be in the classic, universally decodable
encoding — the same shape as the reference `.ico`.

## Decisions

1. **A key is the glyph.** The MarkdownEditor could use the markdown mark; licensing has no equivalent
   standard mark, so the choice is between a key, a certificate-with-seal and a shield. The key wins on
   the criterion that actually matters for an application icon: it stays legible at 16 px, where a
   rosette seal or a document with text lines collapses into noise. It is also honest about the product —
   the app's first page generates signing key pairs, and a key is what vouches for every licence it
   issues.
2. **The square is copied exactly, the glyph is new.** `#3574F0`, 7 px inset, 44 px corner radius at 256,
   all expressed as *fractions* of the canvas so every frame is the same picture at a different size.
   Copying the container is what makes the two applications look like one family; copying the glyph would
   make them look like the same application.
3. **Frames are rendered per size, not downscaled from 256.** The geometry is a handful of circles and
   rectangles; rendering it directly at 16 px with antialiasing keeps the shaft and teeth crisp, where a
   bicubic downscale of a 256 px render smears them. Sizes: 16, 24, 32, 48, 64, 128, 256 — the set
   Windows asks for across Explorer views, the taskbar and Alt-Tab.
4. **32-bpp BGRA DIB frames, not PNG-compressed frames.** A PNG-in-ICO frame is legal since Vista and
   would make the file ~20× smaller, but Skia's ICO frame selection is undocumented and the reference
   `.ico` is uncompressed BMP throughout. A ~300 KB asset is not worth a rendering risk on an icon that
   appears in the window's title bar.
5. **The generator script is a throwaway, not a repository file.** It runs once in the session scratchpad
   and the `.ico` is the deliverable. A committed generator would imply the artwork is regenerated on
   demand, which it is not — and neither sibling repository ships one. The geometry is documented in the
   completion record instead, which is what a later redesign actually needs.
6. **No PNG asset alongside it.** MarkdownEditor's `app-256.png` exists because its about dialog displays
   the icon as a bitmap; this app's About section is built from Phosphor glyphs and displays no bitmap,
   so a second asset would ship unreferenced.
7. **No test.** The desktop suite stands up no Avalonia platform, and nothing in the repository asserts
   on an asset's bytes. The evidence is the decoded frame inventory, the rendered preview, and the
   zero-warning build that embeds the icon into the `.exe`.

## Steps

1. Write a throwaway PowerShell generator in the session scratchpad that draws the icon with
   `System.Drawing` at each of the seven sizes and assembles them into a single `.ico`
   (`ICONDIR` + one `ICONDIRENTRY` per frame + 32-bpp BGRA `BITMAPINFOHEADER` DIBs with an all-zero AND
   mask, bottom-up as the format requires).
2. Render a 256 px preview PNG in the scratchpad and look at it. Iterate on the geometry until the glyph
   reads as a key at 256 px **and** at 16 px.
3. Overwrite `src/Enigma.Licensing.Desktop/Assets/appicon.ico` with the generated file.
4. Decode the committed `.ico` back and confirm its directory: seven entries, the expected sizes,
   32 bits per pixel, and the blue and white pixels where they belong.
5. Build the solution in Release (zero warnings) and confirm the desktop project's `AVLN` count is still
   0 — no `.axaml` changes, but the count is cheap and the build can succeed while carrying them.
6. Run the whole test suite: nothing here is reachable from a test, so both suites must be unchanged.
7. Documentation sweep: `CLAUDE.md`'s project-layout line calls `Assets/appicon.ico` *"placeholder
   branding, reused from the predecessor app"*, which stops being true with this dev.

## Acceptance criteria

1. `src/Enigma.Licensing.Desktop/Assets/appicon.ico` is a valid multi-frame icon carrying exactly the
   sizes 16, 24, 32, 48, 64, 128 and 256, each 32-bpp, each the same picture.
2. The artwork is a white key on a `#3574F0` rounded square, transparent outside it, with the square's
   inset and corner radius in the same proportions as the reference (≈ 2.7 % inset, ≈ 18 % radius).
3. Neither `Enigma.Licensing.Desktop.csproj` nor `Views/MainWindow.axaml` changes — the wiring already
   points at this path.
4. `dotnet build Enigma.Licensing.slnx -c Release` succeeds with zero warnings, and the desktop project's
   `AVLN` count is 0.
5. `dotnet test --solution Enigma.Licensing.slnx -c Release` is green, with the same case count as before.
6. No generator script, preview image or scratch file is committed.

## Out of scope

- The Linux packaging icon and a `.desktop` entry — this repository's publish scripts ship a zip with the
  executable, `README.md` and `LICENSE.md`, and nothing else.
- Any in-app display of the icon (an about-box bitmap, a splash screen).
- `Enigma.MarkdownEditor` — its icon is the reference, and it is not touched.
- Application-window chrome, the navigation rail's Phosphor glyphs, and the theme.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| What glyph? | A key | Legible at 16 px and honest about the product; the app's own first page makes key pairs | Certificate with a wax seal (best at 256 px, mush at 16); shield with a keyhole (reads as "antivirus"); the letters "EL" (a monogram says nothing) |
| How closely to copy MarkdownEditor? | The container exactly, the glyph not at all | The blue rounded square is the family mark; a shared glyph would make the two apps indistinguishable | Re-tint the markdown glyph (wrong product); invent a new container shape (loses the family resemblance) |
| Frame encoding | Uncompressed 32-bpp BGRA DIB | Skia's ICO frame selection is undocumented; the reference icon is uncompressed throughout | PNG-compressed frames (≈ 20× smaller, unverified decoder behaviour on the title-bar icon) |
| Render per size or downscale? | Per size | Simple geometry stays crisp at 16 px; a downscale smears the key's teeth | Render once at 256 and bicubic-downscale |
| Commit the generator? | No | The artwork is authored once, not regenerated; neither sibling repository ships one | Commit it under `scripts/` (implies a reproducible pipeline that does not exist) |
| A second PNG asset? | No | Nothing in this app displays a bitmap icon | Ship `appicon-256.png` for symmetry with MarkdownEditor (unreferenced bytes) |
| A regression test? | No | The suite stands up no Avalonia platform, and no test reads an asset | Headless-Avalonia dependency to assert on an icon (infrastructure this repo has consciously gone without) |
