# FEATURE-0156 — App icon in the output directory

**Status:** DONE
**Type:** single-phase
**Branch:** `feature/feature-0156-icon-in-output-dir`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The application icon is written to the build output directory — and so to every publish directory — next
to `Enigma.Licensing.Desktop(.exe)`, as `app.ico`. Shortcuts, launchers and installers can point at a file
instead of extracting it from the executable. Part of the uniform baseline for every Enigma app.

## Context & constraints

- `src/Enigma.Licensing.Desktop/Assets/appicon.ico` (the new artwork, FEATURE-1ECA) is the
  `<ApplicationIcon>` and `MainWindow`'s `Icon`. `Assets/**` is an `AvaloniaResource` (embedded), so
  nothing lands in `bin/` today.
- Every sibling app ships the output copy as `app.ico` (GitClient FEATURE-5096, Msi FEATURE-C7BF — whose
  asset is also named `appicon.ico`).
- An item with `CopyToOutputDirectory` is also published, and flows to a referencing project's output —
  the desktop test project's `bin/`, where a test can read it.
- `scripts/publish.*` (the release zips, single-file) zip the whole publish directory: a loose `app.ico`
  becomes a fifth zip entry. `docs/RELEASE.md` §7 lists the entries — FEATURE-B6BB updates it.

## Steps

1. `Enigma.Licensing.Desktop.csproj`: `Assets/appicon.ico` copied to the output root as `app.ico`
   (`None` `Update` or `Include` — whichever does not duplicate an item — with
   `CopyToOutputDirectory="PreserveNewest"` and `TargetPath="app.ico"`), with a one-line why. It stays an
   `AvaloniaResource`.
2. `tests/Enigma.Licensing.Desktop.UnitTests/AppIconTests.cs`: `app.ico` sits in
   `AppContext.BaseDirectory`, byte for byte the source asset (found by walking up to
   `Enigma.Licensing.slnx`).
3. Verify: a Release build puts `app.ico` in `bin/Release/net10.0/` and no `Assets/` folder; a `win-x64`
   publish to the scratchpad puts it next to the `.exe`; both byte-identical.

## Acceptance criteria

- `bin/<Configuration>/net10.0/app.ico` and the publish folder's `app.ico` exist, byte for byte the asset.
- The embedded resource still resolves (`/Assets/appicon.ico` in `MainWindow.axaml` unchanged; build clean).
- Release build warning-free; the whole suite green, the new test included.

## Out of scope

- The 256 px PNG (FEATURE-CF21) and the Linux hicolor icons (FEATURE-F319).
- The release runbook's zip contents (FEATURE-B6BB).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Output name | `app.ico` | The name every Enigma app ships | `appicon.ico`; `Enigma.Licensing.Desktop.ico` |
| Source asset name | Unchanged | FEATURE-1ECA kept it; renaming touches XAML for nothing | Renaming to `app.ico` |
| Mechanism | `None` + `CopyToOutputDirectory` + `TargetPath` | Standard MSBuild; flows to publish with no custom target | A custom `Copy` target |
| Test | One desktop-suite test locating the asset from the source tree, plus a manual publish check | Pins the copy where it can be read; the publish is the real proof | No test |
