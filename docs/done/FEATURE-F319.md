# FEATURE-F319 — Linux install and uninstall scripts

## Summary

`packaging/linux/install.sh` and `uninstall.sh` now exist. They are Enigma.GitClient's per-user installer,
adapted to app id `enigma-licensing`, launcher `Enigma.Licensing.Desktop` and display name *Enigma Licensing*.

`install.sh`:
- builds the app (self-contained by default; `--framework-dependent`, `--rid`) or takes a published
  directory (`--from`);
- stages it beside the install directory and swaps it in by rename;
- links the launcher into `$XDG_BIN_HOME`;
- installs eight hicolor icons;
- writes the desktop entry from `enigma-licensing.desktop.in`, pinning `DOTNET_ROOT` when needed.

`uninstall.sh` removes exactly that. It never touches `~/.config/Enigma.Licensing` or a user's key,
licence or profile files.

## Files/modules touched

- Created:
  - `packaging/linux/install.sh` (LF, mode `100755`)
  - `packaging/linux/uninstall.sh` (LF, mode `100755`)
  - `packaging/linux/enigma-licensing.desktop.in`
  - `packaging/linux/icons/enigma-licensing-{16,24,32,48,64,96,128,256}.png`
  - `docs/done/FEATURE-F319.md`
- Modified:
  - `README.md` (desktop section: one sentence on the Linux installer)
  - `CLAUDE.md` (*Shipped* section, layout lines)
  - `docs/roadmap.md`, `docs/plan/FEATURE-F319.md`

## Deviations & follow-ups

- **Changes from the reference:**
  - no git check;
  - no former-launcher name in `uninstall.sh` (this app never had another);
  - the help text names the appearance preference and the user's own files instead of accounts and tokens;
  - the self-contained/framework-dependent size figures are left out of the help (not measured here).
- **Owed on a real Linux session:**
  - the launcher entry appearing in a desktop environment;
  - the icon cache refresh;
  - a self-contained build (`install.sh` with no `--from`).

  Git Bash on Windows cannot show a launcher, and the round trip used a framework-dependent `linux-x64`
  publish.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Symlinks in the Windows dry run | `MSYS=winsymlinks:lnk` | `nativestrict` failed: native symlinks need Developer Mode. `lnk` is what `-L`/`readlink` understand in MSYS, so the uninstall's symlink check was exercised for real |
| The publish used for `--from` | Framework-dependent `linux-x64` | Faster, and it exercises the `DOTNET_ROOT` pinning branch, which a self-contained build skips |
| Closing note | Only the PATH hint | The reference's git note does not apply; this app needs nothing else on the machine |

## Build/test evidence

- Icons, checked with a scratch script that parses the `.ico` directory: all eight are byte-identical to
  `appicon.ico`'s frames of the same size.
- `bash -n` on both scripts: OK. `--help` on both prints the usage. No CR byte in the scripts or the
  template.
- Round trip through Git Bash, in a scratchpad `XDG_DATA_HOME`/`XDG_BIN_HOME`:
  1. `dotnet publish src/Enigma.Licensing.Desktop -c Release -r linux-x64 -o <scratch>/pub` (it holds the
     `Enigma.Licensing.Desktop` launcher and `app.ico`).
  2. `install.sh --from <pub>`, first run: failed at `ln` (`Operation not permitted`, the
     native-symlink case above). It left the install directory in place, as the reference does for a
     post-swap failure.
  3. Second run: exit 0, through the **replace** path. It made the symlink (`-L`, pointing at the
     launcher), 8 icons and the entry (`Exec=env DOTNET_ROOT="/c/Program Files/dotnet" "…/Enigma.Licensing.Desktop"`,
     `Icon=enigma-licensing`). No staging or `.previous` directory was left behind.
  4. `uninstall.sh`: exit 0. It removed the directory, the symlink, the entry and 8 icons. Afterwards
     `find` counts **0** `enigma-licensing` files.
  5. `uninstall.sh` again: "Nothing to remove".
  6. Refusals: a relative `XDG_DATA_HOME` makes the uninstall exit 1 with a message, and an unknown option
     makes the install exit 1.

  The scratch tree was deleted.
- `dotnet build Enigma.Licensing.slnx -c Release`: 0 warnings. `dotnet test --solution …`: 783/783 (no code
  changed).

## Documentation sweep

- `README.md`: the desktop section now names the Linux installer, in prose (the README is packed, so it has
  no relative link).
- `CLAUDE.md`: a *Shipped* section and the layout lines.
