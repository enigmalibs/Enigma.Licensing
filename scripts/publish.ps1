<#
.SYNOPSIS
    Builds the Enigma.Licensing.Desktop release artifacts.

.DESCRIPTION
    Produces one self-contained, single-file build of the issuer application per runtime identifier,
    each zipped under artifacts/.

    Self-contained means the .NET runtime travels inside the executable — the machine that runs the
    artifact needs nothing installed. The library is what consumers install from NuGet; the
    application is a tool an issuer runs occasionally to mint keys and sign licences, and asking them
    to install an SDK first would be the larger part of the work.

    Requires the .NET SDK pinned in global.json. The PowerShell twin of scripts/publish.sh; either
    script produces the same artifacts, so a release can be cut from Windows or from Linux.

.PARAMETER Rid
    The runtime identifiers to build. Defaults to every supported RID.

.EXAMPLE
    ./scripts/publish.ps1
    Builds every RID.

.EXAMPLE
    ./scripts/publish.ps1 win-x64
    Builds just the Windows artifact.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Rid = @('win-x64', 'linux-x64')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/Enigma.Licensing.Desktop/Enigma.Licensing.Desktop.csproj'
$artifacts = Join-Path $root 'artifacts'

# Asked of MSBuild rather than scraped out of the csproj: the app and the library version
# independently, so there is no solution-wide <Version> to read, and -getProperty evaluates the
# project the same way the build does. The artifact names therefore cannot drift from what the
# executable reports about itself.
$version = (dotnet msbuild $project -getProperty:Version)
if ($LASTEXITCODE -ne 0) { throw "Could not read <Version> from $project." }
$version = "$version".Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "No <Version> found in $project."
}

Write-Host "Enigma.Licensing.Desktop $version"
Write-Host ''

foreach ($currentRid in $Rid) {
    $name = "Enigma.Licensing.Desktop-$version-$currentRid"
    $stage = Join-Path $artifacts "publish/$name"
    $archive = Join-Path $artifacts "$name.zip"

    Write-Host "==> $currentRid"
    foreach ($stale in @($stage, $archive)) {
        if (Test-Path -Path $stale) { Remove-Item -Path $stale -Recurse -Force }
    }

    # EnableCompressionInSingleFile pays a little startup time for a much smaller download, which is
    # the right trade for a tool that is launched now and then rather than in a hot loop.
    # DebugType=none keeps the managed symbol files out of the artifact, and
    # GenerateDocumentationFile=false the XML doc files: both are development material and neither
    # means anything to someone running the app. (The far larger *native* symbol files are dropped by
    # the csproj, however the publish was invoked; see the TrimNativeSymbolsFromPublish target.)
    dotnet publish $project `
        --configuration Release `
        --runtime $currentRid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -p:GenerateDocumentationFile=false `
        --output $stage
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $currentRid." }

    # Shipped alongside the binary so an unpacked artifact is self-explanatory to someone who has the
    # zip and not the repository — including what it is licensed under.
    Copy-Item -Path (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE.md') -Destination $stage

    # Compressed from the staging directory itself so the archive expands into its own folder rather
    # than scattering a 100 MB executable across the user's Downloads folder.
    #
    # Compress-Archive does not carry the Unix executable bit, so a linux-x64 artifact built on
    # Windows needs `chmod +x` after unzipping — docs/RELEASE.md says so. scripts/publish.sh, which
    # uses Info-ZIP, preserves it.
    Compress-Archive -Path $stage -DestinationPath $archive -CompressionLevel Optimal

    $size = '{0:N0} MB' -f ((Get-Item -Path $archive).Length / 1MB)
    Write-Host "    $archive ($size)"
    Write-Host ''
}

Write-Host "Done. Artifacts in $artifacts."
