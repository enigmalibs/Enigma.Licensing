# FEATURE-B6BB — Release 2.1.0

## Summary

**Enigma.Licensing 2.1.0** and **Enigma.Licensing.Desktop 2.1.0** are prepared and verified locally. The
version follows the user's rule, `x.y.z → x.(y+1).0`, from 2.0.0. 2.0.0 is published (nuget.org answers
`["1.0.0","2.0.0"]`) and tagged `2.0.0`; `2.1.0` is neither.

The release carries every item of this run:
- FEATURE-A0EB: net8.0 dropped.
- FEATURE-9083: packages updated; the DI floor raised to 10.x.
- FEATURE-1ECA: the new icon.
- FEATURE-CF21: the About dialog.
- FEATURE-0156: `app.ico` in the output.
- FEATURE-5443: the error net.
- FEATURE-F319: Linux install and uninstall.
- FEATURE-11BB: publish scripts.

The spec's splash-screen item produced no work: this app never had one.

**Nothing outward-facing happened.** No tag, no push, no NuGet publish. `docs/RELEASE.md` is the
maintainer's.

## Files/modules touched

- Modified:
  - `src/Enigma.Licensing/Enigma.Licensing.csproj` (`<Version>`, `<PackageReleaseNotes>`, comment)
  - `src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj` (`<Version>`, comment, and
    `ExcludeFromSingleFile` on the `app.ico` item)
  - `src/Enigma.Licensing.Desktop/app.manifest` (`2.1.0.0`)
  - `RELEASENOTES.md` (2.1.0 section prepended)
  - `README.md` (the 2.1 callout)
  - `SECURITY.md` (`2.1.x` supported)
  - `docs/RELEASE.md` (§7 zip contents)
  - `CLAUDE.md` (*Current state*, the 2.0.0 facts corrected, a *Shipped* section, *Conventions*, *Dev workflow*)
  - `docs/roadmap.md`, `docs/plan/FEATURE-B6BB.md`
- Created: `docs/done/FEATURE-B6BB.md`

## Deviations & follow-ups

- **A code fix inside a release dev.** `ExcludeFromSingleFile="true"` was added to the `app.ico` item. The
  zip check showed the win-x64 single-file bundler pulling `app.ico` into the executable (it was in
  `_FilesToBundle`), so the Windows release zip had no icon file. That defeats FEATURE-0156 for the one
  artifact users download. The linux-x64 zip already had it. It is one attribute with the reason in a
  comment, so it was adapted here rather than reopening FEATURE-0156.
- **The snippet gate ran although no guide or README quick start changed.** CLAUDE.md gates every release
  on it. The surface did not move, and the gate confirmed that.
- **No MSI profile.** The `dotnet-release` skill offers one for apps, but this repository has no MSI
  pipeline. Building one would be new infrastructure the spec did not ask for.
- **Owed before tagging** (the maintainer's runbook):
  - the Linux checks recorded in FEATURE-F319;
  - `scripts/publish.sh` (Info-ZIP is absent here, so the zips were built with `scripts/publish.ps1`; the
    linux-x64 zip therefore needs `chmod +x` after unzipping, as RELEASE.md says).

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The dropped TFM vs SemVer | Minor, with the reason in *Compatibility* | No API is guarded by a TFM: `#if` appears nowhere in `src/Enigma.Licensing`. The netstandard2.0 build covers every runtime net8.0 did. The user's rule fixes the number anyway |
| What the notes warn about | Trim/AOT annotations absent from the netstandard2.0 build, and the extra package dependencies there | These are the two observable effects on a .NET 8/9 consumer |
| CLAUDE.md's stale 2.0.0 claims | Corrected in place, with a note that they were true when written | The tag and the registry contradict them |
| Release zips after verification | Deleted (`artifacts/` is gitignored) | They would be stale the moment a commit lands. RELEASE.md §7 rebuilds them from the tag |

## Build/test evidence

- `dotnet clean` and `dotnet build Enigma.Licensing.slnx -c Release --no-incremental`: **0 warnings, 0 errors**.
  AVLN count: **0**. `dotnet test --solution …`: **783/783 passed**, before and after the
  `ExcludeFromSingleFile` change.
- Built assemblies: `Enigma.Licensing.dll` and `Enigma.Licensing.Desktop.dll` report `2.1.0+<sha>`.
- **Snippet gate.** Every ```` ```csharp ```` fence was extracted mechanically from `README.md` and the five
  guides. The executable ones were wrapped (usings hoisted, body in an `async Task`) and compiled in a
  scratch project against the real 2.1.0 project, plus the concrete DI container. The harness suppresses
  warnings, so only errors count. The signature listings were diffed against `src/`.

  | File | Snippets | Compiled | Listings (declarations) | Mismatches |
  |---|---|---|---|---|
  | `README.md` | 1 | 1 | — | 0 |
  | `docs/guides/device-identity.md` | 7 | 6 | 1 (1) | 0 |
  | `docs/guides/generating-licenses.md` | 7 | 6 | 1 (3) | 0 |
  | `docs/guides/keys.md` | 9 | 8 | 1 (5) | 0 |
  | `docs/guides/license-format.md` | 6 | 5 | 1 (4) | 0 |
  | `docs/guides/validating-licenses.md` | 10 | 9 | 1 (3) | 0 |
  | **Total** | **40** | **35, 0 errors** | **5 (16)** | **0** |

  The diff flagged two declarations, and both are false positives:
  - `keys.md` writes the RSA default as `3072`, where the source says the internal
    `KeyFormat.DefaultRsaKeySizeBits` (`= 3072`);
  - `LicenseEnvelope.LoadAsync` is declared `async`, an implementation modifier that is not part of the
    signature.

  The scratch project was deleted.
- **Pack-verify.** `dotnet pack … -o <scratchpad>`:
  - the directory holds `Enigma.Licensing.2.1.0.nupkg` and nothing else (no `.snupkg`);
  - `README.md` (6,860 bytes) and `LICENSE.md` are embedded;
  - nuspec `version` 2.1.0, `title`, `license type="file"`, `readme`, and `releaseNotes` (the new text);
  - `lib/net10.0` and `lib/netstandard2.0`, each with its XML doc, with no stale `HasFeature`/`License.Features`;
  - dependency groups:
    - `net10.0`: DeviceId 6.11.0, Enigma.Core 2.0.0, DI.Abstractions 10.0.14, Ulid 1.4.1;
    - `.NETStandard2.0`: the same, plus System.Buffers 4.6.1 and System.Text.Json 10.0.14;
  - PolySharp and ILLink absent.

  The directory was deleted.
- **Licence audit**, from the nuspecs of the restored graph: 16 packages.
  - MIT expression: DI.Abstractions, Bcl.AsyncInterfaces, System.Text.Json, IO.Pipelines and
    Text.Encodings.Web (all 10.0.14), Memory 4.6.3, Numerics.Vectors 4.6.1, CompilerServices.Unsafe 6.1.2,
    Threading.Tasks.Extensions 4.6.3, Buffers 4.6.1, BouncyCastle 2.7.0, Ulid 1.4.1, DeviceId 6.11.0.
  - Licence file (MIT, as audited by FEATURE-57BC-PHASE01): Enigma.Core 2.0.0 and PlatformAbstractions
    3.1.6.
  - Licence URL (unchanged exception, ships no assembly): Microsoft.NETCore.Platforms 1.1.0.
- **Release zips.** `scripts/publish.ps1` built:
  - `Enigma.Licensing.Desktop-2.1.0-win-x64.zip` (47 MB);
  - `Enigma.Licensing.Desktop-2.1.0-linux-x64.zip` (45 MB).

  After the fix each holds five entries: the folder, the executable, `app.ico` (byte-identical to the
  asset), `README.md` and `LICENSE.md`. The win-x64 executable, extracted and launched, opened its About
  dialog showing **Version 2.1.0** (screenshot taken), and the settings rows show app and library 2.1.0. The
  zips were then deleted.

## Documentation sweep

- `CLAUDE.md`:
  - *Current state*: the 2.0.0 release is now recorded as shipped by the maintainer, and a run paragraph
    was added;
  - a *Shipped* section;
  - the *Conventions* version line and the *Dev workflow* item list.
- `docs/RELEASE.md` §7: the zip contents now include `app.ico`.
- README, RELEASENOTES and SECURITY: updated as release steps.
