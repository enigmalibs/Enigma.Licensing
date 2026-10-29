# FEATURE-F319 — Linux install and uninstall scripts

**Status:** TODO
**Type:** single-phase
**Branch:** `feature/feature-f319-linux-install-scripts`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

`packaging/linux/install.sh` and `uninstall.sh`, as Enigma.GitClient ships them: a per-user, no-root
install of the desktop app into the XDG directories, with a launcher entry and icons, and its exact removal.

## Context & constraints

- **The reference** is Enigma.GitClient's `packaging/linux/` @ `2c781d3`: `install.sh` (options `--from DIR`,
  `--framework-dependent`, `--rid RID`, `-h`; refuses root; validates XDG paths; publishes into a sibling
  staging dir and swaps it in by rename; symlink in `$XDG_BIN_HOME`; hicolor icons at
  16/24/32/48/64/96/128/256; a `.desktop` file from `<app-id>.desktop.in` with `@EXEC@` substituted in bash;
  `DOTNET_ROOT` pinned when a framework-dependent build's runtime is not where a launcher looks; cache
  refreshes as conveniences), `uninstall.sh` (removes exactly that; the symlink only if it points into the
  install dir; refuses an install dir not ending in the app id), `icons/<app-id>-<N>.png`, mode `100755`.
- This repo has no `packaging/` folder. `.gitattributes` already has `*.sh text eol=lf`.
- App id `enigma-licensing`; launcher `Enigma.Licensing.Desktop`; display name `Enigma Licensing`
  (`AppInfo.Title`). Preferences: `ApplicationData/Enigma.Licensing/settings.json` →
  `~/.config/Enigma.Licensing` on Linux (appearance only). Key files and licence profiles are files the
  user chose — never the installer's to touch.
- The app is Avalonia on net10.0 and runs on Linux; nothing in it is Windows-only.
- Icons: `Branding/Apps/final/licensing/png/enigma-licensing-<N>.png`, the same bytes as the `.ico`'s
  frames (FEATURE-1ECA verified).

## Steps

1. `packaging/linux/icons/enigma-licensing-<N>.png` × 8 (16, 24, 32, 48, 64, 96, 128, 256), copied byte
   for byte.
2. `packaging/linux/enigma-licensing.desktop.in` — the reference's keys; `Comment=Generate signing keys,
   sign software licences and validate them`; `Categories=Development;Security;`;
   `Keywords=licence;license;key;signature;signing;rsa;ml-dsa;`; `StartupWMClass=Enigma.Licensing.Desktop`.
3. `packaging/linux/install.sh` — the reference adapted: names, help text, sizes, the closing notes (no git
   check; settings in `~/.config/Enigma.Licensing`).
4. `packaging/linux/uninstall.sh` — the reference adapted (no former-launcher name).
5. Both staged `100755` (`git update-index --chmod=+x`), LF.
6. Verify through Git Bash: `bash -n` on both; `--help` on both; `uninstall.sh` on an empty scratchpad
   `XDG_DATA_HOME` ("Nothing to remove"); `install.sh --from` a `linux-x64` publish into scratchpad
   `XDG_DATA_HOME`/`XDG_BIN_HOME`, then `uninstall.sh` removing it all. What cannot run on Windows is
   recorded as owed on Linux.
7. README: a *Linux* paragraph in the desktop section; `CLAUDE.md` *Project layout*.

## Acceptance criteria

- Both scripts parse, print their help, are `100755` in the index and LF in the tree.
- The eight icons are byte-identical to the `.ico`'s frames of the same size.
- The install → uninstall round trip leaves the scratchpad XDG tree with no `enigma-licensing` file (or the
  part not runnable here is recorded).
- Release build warning-free; the whole suite green (nothing in it changes).

## Out of scope

- `publish.sh` (FEATURE-11BB); distro packages (deb/rpm/AppImage/Flatpak).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Default build | Self-contained, as the reference | Works on a machine with no .NET runtime | Framework-dependent default |
| Icons | The Branding PNGs, verified against the `.ico` | Byte-identical to the shipped icon, no tool dependency | Re-rendering from the SVG |
| Categories | `Development;Security;` | A developer's signing tool; both registered categories | `Utility;` |
| Tests | Script checks + a scratchpad round trip, no unit test | Shell scripts; the round trip is the real proof | A test project for shell |
