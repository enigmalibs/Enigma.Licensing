# FEATURE-11BB — Publish scripts for Windows and Linux

## Summary

Each platform now has a one-command publish. Each one runs from any directory, takes no argument and
exits with `dotnet`'s exit code.

- **Windows:** `packaging/windows/publish.ps1` and `packaging/windows/publish.bat`. Both run
  `dotnet publish <src/Enigma.Licensing.Desktop> -c Release -r win-x64 -p:PublishReadyToRun=true`.
- **Linux:** `packaging/linux/publish.sh`, next to `install.sh`/`uninstall.sh`. It runs the same command
  with `-r linux-x64`.

They are Enigma.GitClient's scripts, adapted.

The existing `scripts/publish.*` (the release-zip pipeline) are unchanged. Each new header points at them,
and README and `docs/RELEASE.md` §7 tell the two apart.

## Files/modules touched

- Created:
  - `packaging/windows/publish.ps1` (LF)
  - `packaging/windows/publish.bat` (CRLF)
  - `packaging/linux/publish.sh` (LF, mode `100755`)
  - `docs/done/FEATURE-11BB.md`
- Modified:
  - `README.md` (desktop section)
  - `docs/RELEASE.md` (§7: one sentence telling the scripts apart)
  - `CLAUDE.md` (*Shipped* section, layout lines)
  - `docs/roadmap.md`, `docs/plan/FEATURE-11BB.md`

## Deviations & follow-ups

- None from the plan.
- `publish.ps1` is LF in the tree, as `.gitattributes` (`* text=auto eol=lf`) makes it, and runs under
  Windows PowerShell 5.1 as such. That is the same as `scripts/publish.ps1`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `publish.sh` ends with | `exec dotnet publish …` | The script's exit code is `dotnet`'s with no extra line (reference) |
| `publish.bat` ends with | `exit /b %ERRORLEVEL%` | Passes the code to the caller without closing a parent `cmd` |
| Writing the `.bat` | `printf '%s\r\n'` | The file is CRLF in the working tree from the start. `.gitattributes` keeps it CRLF on checkout |
| A test pinning the script text | None | This repo has no packaging test to extend. Running the three scripts is the behavioural proof |

## Build/test evidence

- Each script was run from the session scratchpad, not the repository:
  - `cmd /c …\packaging\windows\publish.bat`: exit 0. It writes `…\net10.0\win-x64\publish\` with
    `Enigma.Licensing.Desktop.exe` and `app.ico`.
  - `powershell -ExecutionPolicy Bypass -File …\packaging\windows\publish.ps1`: exit 0, same folder (wiped
    between the two runs).
  - `bash …/packaging/linux/publish.sh` (Git Bash, a cross-publish): exit 0. It writes
    `…/net10.0/linux-x64/publish/` with the `Enigma.Licensing.Desktop` launcher and `app.ico`.
- ReadyToRun applied: `Enigma.Licensing.dll` is 98,304 bytes as IL, 241,664 in the win-x64 publish and
  237,056 in the linux-x64 one.
- Failures propagate: copies of the three scripts in a directory where the project path does not resolve
  each exit 1.
- `bash -n packaging/linux/publish.sh`: OK.
- CR bytes: 24 in the 24-line `publish.bat`, 0 in `publish.sh` and `publish.ps1`.
- `dotnet build Enigma.Licensing.slnx -c Release`: 0 warnings. `dotnet test --solution …`: 783/783 (no code
  changed).

## Documentation sweep

- `README.md`: the desktop section names the three scripts and their output folder.
- `docs/RELEASE.md` §7: tells the new scripts apart from the release-zip scripts.
- `CLAUDE.md`: a *Shipped* section and the layout lines.
