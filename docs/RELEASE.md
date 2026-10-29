# Release runbook

Reusable checklist for publishing a new **Enigma.Licensing** version to NuGet. Only the packable
library project (`src/Enigma.Licensing/`) is published; the desktop application ships as a
self-contained zip per platform (step 7) and is never pushed to NuGet.

Replace `X.Y.Z` with the version being released (e.g. `1.0.0`) throughout. The library's version lives
in `src/Enigma.Licensing/Enigma.Licensing.csproj` (`<Version>`); the desktop app carries its own
independent `<Version>` in `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` — there is no
solution-wide version, so a release that moves both moves both files.

The SDK lives in `~/.dotnet` on the maintainer's machine, so every command below is shown with a
`DOTNET_ROOT=~/.dotnet` prefix. Drop it if `dotnet` is on the default path.

## 1. Pre-release checks

Run from the repository root, on the branch that will be merged:

- [ ] `<Version>X.Y.Z</Version>` set in `src/Enigma.Licensing/Enigma.Licensing.csproj`.
- [ ] The desktop app's `<Version>` set in `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj`
      if this release ships the app too — it names the zips in step 7.
- [ ] `RELEASENOTES.md` has a top `X.Y.Z` section describing the release (newest-first; any
      `(unreleased)` heading renamed to `X.Y.Z`).
- [ ] `<PackageReleaseNotes>` in the library csproj summarizes the release and ends by pointing at
      `RELEASENOTES.md` — the two are written together and must not disagree.
- [ ] README badges and the "what's new" callout reflect `X.Y.Z`. The NuGet badge tracks the published
      version by itself; only the callout is edited per release.
- [ ] `<TargetFrameworks>` reflect the `netstandard2.0` + `net10.0` policy (no out-of-support .NET); any
      change was proposed, confirmed and logged in `RELEASENOTES.md` *Compatibility*.
- [ ] Guide and README snippets re-verified against the public surface if either was touched
      (there is no compile harness for doc snippets — see `docs/guides/`).
- [ ] Clean, warning-free build across all TFMs:
      ```bash
      DOTNET_ROOT=~/.dotnet dotnet build Enigma.Licensing.slnx -c Release
      ```
- [ ] No XAML warnings in the desktop app — `AVLN…` diagnostics bypass `TreatWarningsAsErrors`, so a
      build can succeed while carrying them:
      ```bash
      DOTNET_ROOT=~/.dotnet dotnet build src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj \
        -c Release --no-incremental -v normal | grep -c AVLN     # must print 0
      ```
- [ ] Full test suite green — note the `--solution` flag: the .NET 10 SDK in Microsoft Testing Platform
      mode rejects `dotnet test <Solution>.slnx`:
      ```bash
      DOTNET_ROOT=~/.dotnet dotnet test --solution Enigma.Licensing.slnx -c Release
      ```

## 2. Merge to the default branch

Merge the release branch into the default (published) branch — `main` — via a pull request (or
fast-forward), then check it out locally:

```bash
git switch main
git pull
```

## 3. Tag the release

This repository tags **bare `X.Y.Z`**, with no `v` prefix. Run `git tag` to confirm the convention has
not moved, then tag the merge commit and push the tag:

```bash
git tag X.Y.Z
git push origin X.Y.Z
```

## 4. Pack

`GeneratePackageOnBuild` is **off** for this library, so no `.nupkg` is produced on an ordinary build —
pack explicitly in Release to get the artifact you publish:

```bash
DOTNET_ROOT=~/.dotnet dotnet pack src/Enigma.Licensing/Enigma.Licensing.csproj -c Release -o ./artifacts
```

This writes `./artifacts/Enigma.Licensing.X.Y.Z.nupkg` — and nothing else; a `.snupkg` beside it means
the symbol opt-in crept back into the csproj (`IncludeSymbols`, `SymbolPackageFormat`,
`PublishRepositoryUrl`, `EmbedUntrackedSources` are deliberately absent and must stay that way).
Confirm the version in the filename matches the tag, and inspect the package contents — it bundles
`README.md` and `LICENSE.md`, ships the XML documentation file for each TFM, and declares these
dependency floors:

| Target framework | Dependencies |
|---|---|
| `netstandard2.0` | Enigma.Core 2.0.0 · Microsoft.Extensions.DependencyInjection.Abstractions 10.0.14 · Ulid 1.4.1 · DeviceId 6.11.0 · System.Buffers 4.6.1 · System.Text.Json 10.0.14 |
| `net10.0` | Enigma.Core 2.0.0 · Microsoft.Extensions.DependencyInjection.Abstractions 10.0.14 · Ulid 1.4.1 · DeviceId 6.11.0 |

```bash
unzip -l ./artifacts/Enigma.Licensing.X.Y.Z.nupkg
unzip -p ./artifacts/Enigma.Licensing.X.Y.Z.nupkg Enigma.Licensing.nuspec
```

## 5. Push to NuGet

Publish with a NuGet API key that has push rights for the `Enigma.Licensing` package:

```bash
DOTNET_ROOT=~/.dotnet dotnet nuget push ./artifacts/Enigma.Licensing.X.Y.Z.nupkg \
  --api-key <NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

`dotnet pack` produces **only** the `.nupkg` — that single file is what gets pushed. This project does
not ship a `.snupkg` symbols package, and `pack` is never run with `--include-symbols`. The API key is
a secret — never commit or echo it.

## 6. Post-publish verification

- [ ] The package page shows the new version:
      <https://www.nuget.org/packages/Enigma.Licensing> (indexing can take a few minutes).
- [ ] The README NuGet badge resolves to `X.Y.Z` (shields.io caches briefly).
- [ ] A scratch project can restore the new version:
      ```bash
      dotnet add package Enigma.Licensing --version X.Y.Z
      ```
- [ ] The GitHub release/tag is present and its notes match `RELEASENOTES.md`.

## 7. Desktop application artifacts

The app is distributed as a self-contained, single-file zip per platform — nothing needs to be
installed on the machine that runs it. Build both from either OS; the two scripts are twins and
produce the same artifacts:

```bash
scripts/publish.sh                    # every RID (win-x64, linux-x64)
scripts/publish.sh linux-x64          # just one
```

```powershell
./scripts/publish.ps1                 # the PowerShell twin
```

Each run writes `artifacts/Enigma.Licensing.Desktop-<version>-<rid>.zip`, where `<version>` is read
back from the app csproj — the scripts never take a version argument, so an artifact cannot be named
something the executable disagrees with. `artifacts/` is gitignored; the zips are attached to the
GitHub release rather than committed.

- [ ] Both zips built and their names carry the app's `<Version>`.
- [ ] Each zip expands into its own directory containing the executable, `README.md` and `LICENSE.md`.
- [ ] The linux-x64 binary is executable after unzipping — `Compress-Archive` (the PowerShell script)
      does not carry the Unix executable bit, so an artifact built on Windows needs `chmod +x` after
      extraction. `scripts/publish.sh` uses Info-ZIP, which preserves it.
- [ ] Both zips attached to the GitHub release for the tag pushed in step 3.
