# FEATURE-A0EB — Drop the net8.0 targets

## Summary

The library now targets `netstandard2.0;net10.0`, and its test suite targets `net10.0` alone. .NET 8
leaves support on 2026-11-10. A net8.0 or net9.0 consumer of the package now resolves the
`netstandard2.0` build, so nobody is excluded. Every other framework is unchanged: the library keeps
`netstandard2.0`, and the desktop app and its suite stay `net10.0`. `IsAotCompatible` was conditioned on
`!= 'netstandard2.0'`, so it now applies to net10.0 alone with no edit.

## Files/modules touched

- Modified:
  - `src/Enigma.Licensing/Enigma.Licensing.csproj` (TFMs and comments)
  - `tests/Enigma.Licensing.UnitTests/Enigma.Licensing.UnitTests.csproj` (single `TargetFramework`; the `NETSDK1138` comment is gone)
  - `Directory.Packages.props` (comments only)
  - `README.md` (*Installation* target line)
  - `docs/RELEASE.md` (pre-flight TFM line, dependency table)
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-A0EB.md`
- Created: `docs/done/FEATURE-A0EB.md`

## Deviations & follow-ups

- **`git grep net8` finds more than the plan listed.** The extra matches are new explanatory history: the
  csproj comments and CLAUDE.md say *that* `net8.0` was dropped and why. Without that, the next reader
  would wonder whether it was an oversight. They are not targets.
- **The `docs/RELEASE.md` dependency table still names DI abstractions 9.0.18.** FEATURE-9083 moves the
  versions, and FEATURE-B6BB checks the table against the real nuspec.
- **Follow-up:** nothing runs the `netstandard2.0` build. The old net8.0 leg did not either: it ran the
  library's own net8.0 build. A `net472` test leg would close that gap, but it would be new
  infrastructure. Left to the maintainer.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| "all three TFMs" phrasing in comments | Reworded to "both of our TFMs" | It became factually wrong. Enigma.Core still multi-targets net8.0 too, and that is said where it matters |
| README *Installation* | Says a .NET 8/9 app uses the .NET Standard 2.0 build | The README is packed. A reader on .NET 8 needs to know the package still serves them |
| CLAUDE.md suite count | Updated to 712 (426 + 286) | The library suite now runs once. The old figure (1174) predated FEATURE-B086 anyway |

## Build/test evidence

- `src/Enigma.Licensing/bin` and `obj` and the library suite's `bin`/`obj` were deleted, then
  `dotnet build Enigma.Licensing.slnx -c Release` ran: **0 warnings, 0 errors**.
  `src/Enigma.Licensing/bin/Release/` now holds `net10.0/` and `netstandard2.0/` only.
- `dotnet test --solution Enigma.Licensing.slnx -c Release`: **712/712 passed**. Library suite 426 (net10.0),
  desktop suite 286. Before: 1138, because the library suite's 426 ran twice (net8.0 + net10.0).
- No `.axaml` was touched, so there was no AVLN check.

## Documentation sweep

- `README.md` *Installation*: the targets line was wrong. Fixed.
- `docs/RELEASE.md`: the pre-flight TFM policy and the dependency table's `net8.0` row. Fixed.
- `CLAUDE.md`: the TFM bullets, the IsAotCompatible note, the .NET 8 runtime paragraph (removed), the
  `NETSDK1138` bullet (generalised) and the suite count. Fixed.
- `RELEASENOTES.md` and `docs/migrations/`: history, left as is.
