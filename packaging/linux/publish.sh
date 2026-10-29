#!/usr/bin/env bash
#
# Publishes Enigma Licensing (Enigma.Licensing.Desktop) for Linux x64. Runs, from wherever it is started:
#
#   dotnet publish src/Enigma.Licensing.Desktop -c Release -r linux-x64 -p:PublishReadyToRun=true
#
# A Release build for linux-x64, compiled ReadyToRun so it starts faster, which runs on an installed
# .NET 10 runtime. The result lands in
#
#   src/Enigma.Licensing.Desktop/bin/Release/net10.0/linux-x64/publish/
#
# and dotnet's last line names that folder. To build and install in one step, into the desktop
# launcher, use install.sh in this same directory instead. The release zips — self-contained,
# single-file, named after the version — are built by scripts/publish.sh (docs/RELEASE.md step 7).

set -euo pipefail

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
readonly script_dir

# Resolved from this script rather than the current directory, so it runs from anywhere; exec, so
# dotnet's exit code is the script's.
exec dotnet publish "${script_dir}/../../src/Enigma.Licensing.Desktop" -c Release -r linux-x64 -p:PublishReadyToRun=true
