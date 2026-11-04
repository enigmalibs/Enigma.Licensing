# FEATURE-1A5D — Icon set filling the frame

**Branch:** `feature/feature-1a5d-final3-icon-set`
**Run:** feature/2026-11-04-final3-icons-release

## Summary

The application icon is now the family's `Branding/Apps/final3/licensing` set. It is FEATURE-BD63's tile-less
certificate and green seal, scaled by 1.1915 so the drawing fills the frame, with a heavier simplified master for
the 16–32 px frames. Every place the repository carries the icon now holds the delivered file byte for byte:

- `src/Enigma.Licensing.Desktop/Assets/appicon.ico` ← `enigma-licensing.ico` (16,965 → 22,117 bytes). This file
  is:
  - the executable's `<ApplicationIcon>`;
  - the window and taskbar `Icon`;
  - the `app.ico` copied beside the executable, which `buildmsi.ps1` hands the MSI for its shortcuts and
    *Installed apps*;
- `src/Enigma.Licensing.Desktop/Assets/app-256.png` ← `png/enigma-licensing-256.png` (the About dialog);
- `packaging/linux/icons/enigma-licensing-{16,24,32,48,64,96,128,256}.png` ← the matching PNGs (what
  `install.sh` installs).

No code, XAML, csproj or script changed: the file names and the frame set are the same as before.

## Files/modules touched

- Modified: `src/Enigma.Licensing.Desktop/Assets/appicon.ico`, `src/Enigma.Licensing.Desktop/Assets/app-256.png`,
  `packaging/linux/icons/enigma-licensing-{16,24,32,48,64,96,128,256}.png`, `CLAUDE.md`,
  `docs/roadmap.md`, `docs/plan/FEATURE-1A5D.md`.
- Created: `docs/done/FEATURE-1A5D.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| A visual check in the running app | Not done | The artwork is the Branding repository's, delivered by the user, and the files are its output unchanged. The frame set the title bar and the About dialog read is the shape they already render |
| The installer's icon | Checked through the plain win-x64 publish folder (`packaging/windows/publish.ps1`), which is what the MSI packages | `buildmsi.ps1` runs `git clean -dxf`. FEATURE-7DB7 builds the MSI itself, in a scratch clone |

## Deviations & follow-ups

- None against the plan.
- The 2.1.0 and 2.2.0 release notes still describe the earlier icons. They are correct for those versions, so
  they were left alone. The 2.3.0 notes (FEATURE-7DB7) describe this change.

## Build/test evidence

**Run baseline**, taken on `feature/2026-11-04-final3-icons-release` @ `5d99b3b` before this dev's branch.
Later devs of this run cite this record.
- `dotnet build Enigma.Licensing.slnx -c Release --no-incremental`: **0 warnings, 0 errors**; `AVLN` count **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **786/786 passed** across the library and desktop
  suites.
- No failing test and no pre-existing warning.

**This dev**
- Build: **0 warnings, 0 errors**; `AVLN` **0**.
- Tests: **786/786 passed**. `AppIconTests` is among them: the output `app.ico` equals the new asset.
- `cmp`: the `.ico`, `app-256.png` and all eight Linux PNGs equal their source files.
- Frame script (`struct` over the ICO directory):
  - ten frames, each a 32-bit RGBA PNG (bit depth 8, colour type 6), at 16, 20, 24, 32, 40, 48, 64, 96, 128 and
    256 px;
  - each of the eight Linux PNGs is the frame of its size byte for byte, and `app-256.png` is the 256 frame byte
    for byte;
  - the 20 and 40 px frames have no Linux copy, as before, because hicolor has no directory for those sizes.
- Build outputs: `src/…/bin/Release/net10.0/app.ico` and the desktop test output's `app.ico` equal the new asset.
- `packaging/windows/publish.ps1`: exit 0. The publish folder's `app.ico` has SHA-256 `466FEE05…FDAF`, the same as
  the asset (22,117 bytes).
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md`:
  - *The application icon is real branding now*: a bullet recording the set that fills the frame and where it
    comes from;
  - *Project layout*: the `Assets/appicon.ico` line names FEATURE-1A5D.
