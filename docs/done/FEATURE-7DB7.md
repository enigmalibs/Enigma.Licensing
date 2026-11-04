# FEATURE-7DB7 — Release 2.3.0

**Branch:** `feature/feature-7db7-release-2-3-0`
**Run:** feature/2026-11-04-final3-icons-release

## Summary

**Enigma.Licensing.Desktop 2.3.0** is prepared. It is an application-only release carrying this run's
FEATURE-1A5D (the icon filling its frame). The library is untouched and stays at **2.1.0**.

- App `<Version>` 2.2.0 → **2.3.0**; `app.manifest` `assemblyIdentity` 2.2.0.0 → **2.3.0.0**; both comments
  updated.
- **`buildmsi.ps1` is unchanged, on purpose.** It reads the app csproj's `<Version>`, so the bump above is its
  version update. The MSI it built is `Enigma.Licensing.2.3.0.msi` with `ProductVersion` 2.3.0 (evidence below).
- `RELEASENOTES.md`: a `# Enigma.Licensing v2.3.0 Release Notes` section on top. It opens by saying the library is
  unchanged at 2.1.0, then has *New Features*, *Fixes*, *Dependencies*, *Compatibility* and *Version*.
- `SECURITY.md`: the application's supported line is 2.3.x; 2.2.x moves to unsupported. The library row is
  unchanged.

## Files/modules touched

- Modified: `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj`, `src/Enigma.Licensing.Desktop/app.manifest`,
  `RELEASENOTES.md`, `SECURITY.md`, `CLAUDE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-7DB7.md`.
- Created: `docs/done/FEATURE-7DB7.md`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The Avalonia set (12.1.4 → 12.1.5 available) | Held | It is a coupled set, and Enigma.Avalonia.Desktop 1.3.0, still the newest on nuget.org, is built against 12.1.4 |
| Other packages | Nothing to apply | `dotnet list package --outdated` reported no other update in any of the four projects |
| `docs/RELEASE.md` | Not changed | Its only version-specific lines ("2.2.0 was the first" application-only release, "`app.ico` since 2.1.0") are still true |
| `DOTNET_ROOT` for the MSI build | Unset | With `~/.dotnet` set, `wix.exe` found no runtime (details below). That is an environment fact, not a repo defect, so it used no fix cycle |
| Launching the 2.3.0 zip | Not done | The built assembly's version was read directly (2.3.0), and the About dialog reads that same attribute (`AppInfoTests`) |

## Deviations & follow-ups

- **`DOTNET_ROOT=~/.dotnet` breaks `buildmsi` on this machine.** The first MSI attempt in the scratch clone
  failed with `Cannot find WiX extension 'WixToolset.UI.wixext'`, preceded by `You must install .NET to run this
  application … DOTNET_ROOT = C:\Users\jcl\.dotnet`. The cause:
  - `dotnet` here is `C:\Program Files\dotnet`, and the muxer ignores the variable;
  - `~/.dotnet` holds only global tools;
  - `wix.exe` is a tool apphost that honours `DOTNET_ROOT`, so it found no runtime and WixSharp's extension
    lookup came back empty.

  With the variable unset, the build succeeded in 35 s. Follow-ups:
  - CLAUDE.md's *Build & test* still says "the SDK lives in `~/.dotnet` on this machine";
  - `docs/RELEASE.md` prefixes every command with `DOTNET_ROOT=~/.dotnet`.

  Both deserve a look by the maintainer. This dev only recorded the hazard (CLAUDE.md, the 2.3.0 *Shipped*
  section).
- **2.1.0's package is still not on nuget.org.** On 2026-11-04 the registry answered `["1.0.0","2.0.0"]`.
  Unchanged since FEATURE-FF4B, and the maintainer's call.
- **The MSI holds 78 files where FEATURE-6D42-PHASE02 recorded 80.** The 2.3.0 publish folder has exactly 78
  files, and the MSI packages it one for one. Nothing in `Directory.Packages.props` or the app csproj's
  references moved since, so the earlier figure was probably counted another way. It was not chased further.
- No pack-verify and no snippet gate: neither the package nor the guides or the README quick start moved.
- `artifacts/` (the zip built for the pre-flight) and the scratch clone were deleted afterwards.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-1A5D.md` (0 warnings, `AVLN` 0, 786/786).
- `git tag --list 2.3.0`: empty.
- `dotnet build Enigma.Licensing.slnx -c Release --no-incremental`: **0 warnings, 0 errors**; `AVLN` count **0**.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **786/786 passed**.
- Built versions:
  - `Enigma.Licensing.Desktop.dll`: file 2.3.0.0, product `2.3.0+ed579cc…`;
  - `Enigma.Licensing.dll`: file 2.1.0.0, product `2.1.0+ed579cc…`;
  - `app.manifest`: 2.3.0.0.
- `scripts/publish.ps1 win-x64`: exit 0. It produced `artifacts/Enigma.Licensing.Desktop-2.3.0-win-x64.zip`
  (48.8 MB) holding the executable, `app.ico` (22,117 bytes, FEATURE-1A5D's), `README.md` and `LICENSE.md`.
- **`buildmsi.bat` end to end, in a scratch clone** of this branch with the dev's tree committed there:
  - exit 0 in 35 s; an untracked `canary.txt` was cleaned;
  - `artifacts/` held `Enigma.Licensing.2.3.0.msi` (69,464,348 bytes) and `Enigma.Licensing.2.3.0.msipkg.json`.
- **The MSI's tables**, read through `msi.dll`:
  - `ProductName` Enigma Licensing, `Manufacturer` Josué Clément, `ProductVersion` **2.3.0**;
  - `UpgradeCode` `{4BAD4F52-DD12-4B57-8D94-52FE1936F034}` (unchanged), `ALLUSERS` 1;
  - `Upgrade` rows: `VersionMax 2.3.0` (attributes 1) and `VersionMin 2.3.0` (attributes 2);
  - two "Enigma Licensing.lnk" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
    `[INSTALLDIR]\Enigma.Licensing.Desktop.exe`;
  - both `Icon` streams (`app_icon.ico`, the `ARPPRODUCTICON`; `IconFile1_app.ico`, the shortcuts') and the
    installed `app.ico` are 22,117 bytes, SHA-256 `466FEE05…FDAF`, the asset byte for byte;
  - 78 files.
- `RELEASENOTES.md`'s one quoted path (`packaging/windows/buildmsi.ps1`) exists.
- Nothing ran `git tag`, `git push`, `dotnet pack` or `dotnet nuget push`, installed the MSI, or ran `buildmsi`
  in this repository.
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md`:
  - *Current state*: 2.2.0 is tagged (`417e490`, no longer "nothing was tagged"), plus this run's paragraph;
  - a *Shipped* section for this release, which records the `DOTNET_ROOT` hazard;
  - *Conventions → Packaging metadata*: the app moved alone again;
  - *Dev workflow*: this run's items.
