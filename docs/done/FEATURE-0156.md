# FEATURE-0156 — App icon in the output directory

## Summary

The application icon is now written as a file, `app.ico`, at the root of the build output and of every
publish folder, next to `Enigma.Licensing.Desktop(.exe)`. That is the name every Enigma app ships it under.
Shortcuts, launchers and installers can point at it. The icon is still embedded as an `AvaloniaResource`:
the window and the About dialog read it from there, unchanged.

The mechanism is one item, `<None Update="Assets/appicon.ico" CopyToOutputDirectory="PreserveNewest"
TargetPath="app.ico" />`. `dotnet msbuild -getItem:None` showed that the SDK's default glob already holds the
file, so `Update` adds nothing twice. An item copied to the output is published too, and it flows to the
desktop test project's output through the project reference.

## Files/modules touched

- Modified:
  - `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj`
  - `CLAUDE.md` (layout lines; the desktop suite's fixture-file sentence)
  - `docs/roadmap.md`, `docs/plan/FEATURE-0156.md`
- Created:
  - `tests/Enigma.Licensing.Desktop.UnitTests/AppIconTests.cs`
  - `docs/done/FEATURE-0156.md`

## Deviations & follow-ups

- None from the plan.
- **Follow-up for FEATURE-B6BB:** the release zips (`scripts/publish.*`, single-file) zip the whole publish
  folder, so they now carry `app.ico` as a fifth entry. `docs/RELEASE.md` §7 lists four entries and is
  updated by the release dev, which builds the zips.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `Include` or `Update` | `Update` | The default `None` glob already includes `Assets/appicon.ico`. `Include` would add a second item |
| Locating the source asset in the test | Walk up from `AppContext.BaseDirectory` to `Enigma.Licensing.slnx` | No fixture copy to keep in step. The test compares against the real asset |
| A second test | `app.ico` is not also under `Assets/` | `TargetPath` is what puts it at the root. A `Link` mistake would copy the folder structure instead |

## Build/test evidence

- `dotnet build Enigma.Licensing.slnx -c Release`: **0 warnings, 0 errors**. No `.axaml` was touched.
- `src/Enigma.Licensing.Desktop/bin/Release/net10.0/app.ico`: `cmp`-identical to `Assets/appicon.ico`. There is
  no `Assets/` folder in the output.
- `dotnet publish src/Enigma.Licensing.Desktop -c Release -r win-x64 -o <scratchpad>`: `app.ico` sits next to
  `Enigma.Licensing.Desktop.exe`, `cmp`-identical. The scratch folder was deleted.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **737/737 passed** (735 + the 2 `AppIconTests`).

## Documentation sweep

- `CLAUDE.md`: the layout lines and the desktop suite's "loads no fixture files" sentence, which is no
  longer exactly true. Fixed.
- `docs/RELEASE.md` §7 (zip contents): deferred to FEATURE-B6BB, which builds the zips that change.
