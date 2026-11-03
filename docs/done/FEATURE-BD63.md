# FEATURE-BD63 — New icon set without the tile

**Branch:** `feature/feature-bd63-new-icon-set`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The application icon is now the family's tile-less set from `Branding/Apps/final2/licensing`: the same
certificate and green seal as FEATURE-1ECA's, on a transparent background instead of a dark rounded square.
Every place the repository carries the icon holds the delivered file byte for byte:

- `src/Enigma.Licensing.Desktop/Assets/appicon.ico` ← `enigma-licensing.ico` (25,761 → 16,965 bytes). It is
  the executable's `<ApplicationIcon>`, the window's `Icon`, and the `app.ico` copied beside the executable;
- `src/Enigma.Licensing.Desktop/Assets/app-256.png` ← `png/enigma-licensing-256.png` (the About dialog);
- `packaging/linux/icons/enigma-licensing-{16,24,32,48,64,96,128,256}.png` ← the matching PNGs (what
  `install.sh` installs).

No code, XAML or csproj changed: the file names and the frame set are unchanged.

## Files/modules touched

- Modified: `src/Enigma.Licensing.Desktop/Assets/appicon.ico`, `src/Enigma.Licensing.Desktop/Assets/app-256.png`,
  `packaging/linux/icons/enigma-licensing-{16,24,32,48,64,96,128,256}.png`, `CLAUDE.md`,
  `docs/roadmap.md`, `docs/plan/FEATURE-BD63.md`.
- Created: `docs/done/FEATURE-BD63.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| A visual check in the running app | Not done | The artwork is the Branding repository's, delivered by the user; the files are its output unchanged, and the frame set the title bar and About dialog read is the one they already render |

## Deviations & follow-ups

- None against the plan.
- The 2.1.0 release notes still describe the icon "on a dark rounded square" — correct for 2.1.0, so left
  alone; 2.2.0's notes (FEATURE-FF4B) describe the change.

## Build/test evidence

- **Run baseline** (on `feature/2026-11-03-icons-msi-release` @ `39314a6`, before this dev's branch):
  `dotnet build Enigma.Licensing.slnx -c Release` **0 warnings, 0 errors**; `AVLN` count **0**;
  `dotnet test --solution Enigma.Licensing.slnx -c Release` **783/783 passed** across the library and desktop
  suites — no failing test, no pre-existing warning. Later devs of this run cite this record.
- This dev: build **0 warnings, 0 errors**; `AVLN` **0**; tests **783/783 passed** (`AppIconTests` among them:
  the output `app.ico` equals the new asset).
- `cmp`: the `.ico`, `app-256.png` and all eight Linux PNGs equal their source files.
- Frame script (`struct` over the ICO directory): ten frames, each a 32-bit RGBA PNG (bit depth 8, colour
  type 6) at 16, 20, 24, 32, 40, 48, 64, 96, 128, 256; each of the eight Linux PNGs is the frame of its size
  byte for byte, and `app-256.png` is the 256 frame byte for byte. The 20 and 40 px frames have no Linux copy
  (hicolor has no such directory), as before.
- The build outputs (`src/…/bin/Release/net10.0/app.ico`, the desktop test output's `app.ico`) equal the new
  asset.
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md` *The application icon is real branding now*: a bullet recording the tile-less set and where it
  comes from; *Project layout*: `Assets/appicon.ico` no longer "on a dark square".
