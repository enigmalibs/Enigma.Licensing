#!/usr/bin/env bash
#
# Builds the Enigma.Licensing.Desktop release artifacts: one self-contained, single-file build of the
# issuer application per runtime identifier, each zipped under artifacts/.
#
# Self-contained means the .NET runtime travels inside the executable — the machine that runs the
# artifact needs nothing installed. The library is what consumers install from NuGet; the application
# is a tool an issuer runs occasionally to mint keys and sign licences, and asking them to install an
# SDK first would be the larger part of the work.
#
# Usage:
#   scripts/publish.sh                    # every RID
#   scripts/publish.sh linux-x64          # just one
#   scripts/publish.sh win-x64 linux-x64  # an explicit list
#
# Requires: the .NET SDK pinned in global.json, and `zip`.

set -euo pipefail

readonly DEFAULT_RIDS=(win-x64 linux-x64)

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly ROOT="$(dirname -- "$SCRIPT_DIR")"
readonly PROJECT="$ROOT/src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj"
readonly ARTIFACTS="$ROOT/artifacts"

# Asked of MSBuild rather than scraped out of the csproj: the app and the library version
# independently, so there is no solution-wide <Version> to read, and -getProperty evaluates the
# project the same way the build does. The artifact names therefore cannot drift from what the
# executable reports about itself.
version="$(dotnet msbuild "$PROJECT" -getProperty:Version | tr -d '[:space:]')"
if [[ -z "$version" ]]; then
    echo "error: no <Version> found in $PROJECT" >&2
    exit 1
fi

rids=("$@")
if [[ ${#rids[@]} -eq 0 ]]; then
    rids=("${DEFAULT_RIDS[@]}")
fi

command -v zip >/dev/null 2>&1 || { echo "error: 'zip' is not installed" >&2; exit 1; }

echo "Enigma.Licensing.Desktop $version"
echo

for rid in "${rids[@]}"; do
    name="Enigma.Licensing.Desktop-$version-$rid"
    stage="$ARTIFACTS/publish/$name"
    archive="$ARTIFACTS/$name.zip"

    echo "==> $rid"
    rm -rf -- "$stage" "$archive"

    # EnableCompressionInSingleFile pays a little startup time for a much smaller download, which is
    # the right trade for a tool that is launched now and then rather than in a hot loop.
    # DebugType=none keeps the managed symbol files out of the artifact, and
    # GenerateDocumentationFile=false the XML doc files: both are development material and neither
    # means anything to someone running the app. (The far larger *native* symbol files are dropped by
    # the csproj, however the publish was invoked; see the TrimNativeSymbolsFromPublish target.)
    dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -p:DebugType=none \
        -p:GenerateDocumentationFile=false \
        --output "$stage"

    # Shipped alongside the binary so an unpacked artifact is self-explanatory to someone who has the
    # zip and not the repository — including what it is licensed under.
    cp -- "$ROOT/README.md" "$ROOT/LICENSE.md" "$stage/"

    # Zipped from the parent so the archive expands into its own directory rather than scattering a
    # 100 MB executable across the user's Downloads folder.
    (cd -- "$ARTIFACTS/publish" && zip --quiet --recurse-paths "$archive" "$name")

    echo "    $archive ($(du -h -- "$archive" | cut -f1))"
    echo
done

echo "Done. Artifacts in $ARTIFACTS."
