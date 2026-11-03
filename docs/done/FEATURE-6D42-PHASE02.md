# FEATURE-6D42-PHASE02 — The buildmsi script

**Item:** FEATURE-6D42 — Windows MSI installer
**Branch:** `feature/feature-6d42-phase02-buildmsi-script`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

`packaging/windows/buildmsi.ps1` builds the Windows installer in one command, from any directory. It is
Enigma.GitClient's script with the product's identity swapped in and the version read from the app's csproj
(Enigma.Msi's own adaptation, since the library versions separately):

1. checks `git`, `dotnet`, `wix` and the vendored worker **before** anything is cleaned;
2. reads `<Version>` from `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` and checks `X.Y.Z`;
3. `git -C <root> clean -dxf`; 4. `publish.ps1`, then checks the exe and `app.ico` in the publish folder;
5. writes `artifacts/Enigma.Licensing.X.Y.Z.msipkg.json` — upgrade code `4bad4f52-dd12-4b57-8d94-52fe1936f034`,
   a new product code, "Enigma Licensing", "Josué Clément", `PerMachine`, `%ProgramFiles%\Enigma Licensing`,
   `compression` `None`, `app.ico` for the Control Panel and both shortcuts (`%Desktop%`, `%ProgramMenu%`);
6. runs `Enigma.Msi.Worker.exe build` on it and prints the MSI's path and the elapsed time.

Every failure prints `buildmsi: <reason>` and exits non-zero with the failing tool's code. `buildmsi.bat` (CRLF)
runs it from `cmd` with `-ExecutionPolicy Bypass`. `WindowsInstallerIdentityTests` pins the three constants.

## Files/modules touched

- Created: `packaging/windows/buildmsi.ps1`, `packaging/windows/buildmsi.bat`,
  `tests/Enigma.Licensing.Desktop.UnitTests/WindowsInstallerIdentityTests.cs`, `docs/done/FEATURE-6D42-PHASE02.md`.
- Modified: `README.md`, `docs/RELEASE.md`, `CLAUDE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-6D42.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Differences from GitClient's script | Only the identity constants, the csproj version source (+ `$project`) and one help paragraph telling the MSI from the release zips | "Exactly like Enigma.GitClient"; the diff against it is the review |
| What the test pins | `$UpgradeCode`, `$ProductName` = `AppInfo.Title`, `$Assembly` = the app assembly's name, and that `src/<Assembly>/<Assembly>.csproj` exists | The script derives the install folder, shortcuts, version source, publish folder and exe from those three |
| Where the test lives | Desktop suite, beside `AppIconTests` | It is about the app; the desktop suite runs on this machine |
| The MSI's managed `.pdb`s and XML docs | Packaged, as the plain publish leaves them | The publish is FEATURE-11BB's; GitClient's MSI packages its publish folder the same way |

## Deviations & follow-ups

- **Follow-up — managed symbols and XML docs in the MSI.** The plain publish keeps `Enigma.Licensing(.Desktop).pdb`
  and `.xml` (about 0.4 MB of 69 MB). `scripts/publish.*` passes `-p:DebugType=none
  -p:GenerateDocumentationFile=false` for the zips; doing so for the MSI would change `packaging/windows/publish.ps1`,
  so it is left as GitClient has it.
- **A 32-bit package**: Enigma.Msi has no platform setting (template `Intel;1033`), so `%ProgramFiles%` is
  `Program Files (x86)` on 64-bit Windows. `docs/RELEASE.md` says so.
- **WixSharp's "source base directory"** printed the caller's current directory (this repository). It resolves
  relative paths only, and the profile's are absolute: this repository had no new file, ignored or not, after the run.
- **Not run, by design:** installing, launching or uninstalling the MSI.
- Line endings: `buildmsi.bat` is CRLF in the working tree, as `.gitattributes` asks; the `.ps1` is LF and ASCII.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BD63.md` (0 warnings, `AVLN` 0, 783/783).
- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**. No `.axaml` touched.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **786/786 passed** (783 + the 3 new tests).
- Mutation check: changing the upgrade code and the product name in the script fails
  `TheInstaller_KeepsTheProductLinesUpgradeCode` and `TheInstaller_NamesTheProductAsTheApplicationDoes`; the
  script was restored from a copy.
- **End to end, in a scratch clone** of this branch with the dev's tree committed there:
  - **`wix` removed from the `PATH`:** `buildmsi: wix is not on the PATH: dotnet tool install --global wix.`,
    exit **1**, and an untracked `canary.txt` was still there — nothing was cleaned.
  - **`buildmsi.bat`:** git removed the canary, the publish ran, Enigma.Msi built the MSI (WiX 7.0.0); exit
    **0** in **18 s**. `artifacts/` held `Enigma.Licensing.2.1.0.msi` (69,431,573 bytes) and its
    `.msipkg.json` (`"compression": "None"`). `git status --porcelain` empty afterwards; only
    `packaging/windows/Enigma.Msi.Worker/.wixsharp/` shows, as ignored.
  - **The MSI's tables** (Windows Installer API): `ProductName` Enigma Licensing, `Manufacturer` Josué Clément,
    `ProductVersion` 2.1.0 (the app csproj's), `UpgradeCode` `{4BAD4F52-DD12-4B57-8D94-52FE1936F034}`,
    `ALLUSERS` 1, `ARPPRODUCTICON` `app_icon.ico`; `INSTALLDIR` "Enigma Licensing" under `ProgramFilesFolder`;
    two "Enigma Licensing.lnk" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
    `[INSTALLDIR]\Enigma.Licensing.Desktop.exe`, icon `IconFile1_app.ico`; `Upgrade` rows `VersionMax 2.1.0`
    (attributes 1) and `VersionMin 2.1.0` (attributes 2); 80 files, `app.ico` 16,965 bytes (FEATURE-BD63's);
    no native `.pdb`; template `Intel;1033`.
- Fix budget: 0 cycles used.

## Documentation sweep

- `README.md` *Desktop application*: the installer, the `git clean -dxf` warning and its prerequisites, in prose
  (the README is packed).
- `docs/RELEASE.md`: the intro names the installer; new §8 *Windows installer* and *The Windows installer's
  identity*.
- `CLAUDE.md`: a *Shipped* section for the installer; the layout lists the two scripts, the worker folder and the
  new test; the runbook line mentions the MSI.
