#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes Enigma Licensing (Enigma.Licensing.Desktop) for Windows x64.

.DESCRIPTION
    Runs, from wherever it is started:

        dotnet publish .\src\Enigma.Licensing.Desktop\ -c Release -r win-x64 -p:PublishReadyToRun=true

    A Release build for win-x64, compiled ReadyToRun so it starts faster, which runs on an installed
    .NET 10 runtime. The result lands in

        src\Enigma.Licensing.Desktop\bin\Release\net10.0\win-x64\publish\

    and dotnet's last line names that folder. Start Enigma.Licensing.Desktop.exe in it.

    This is the plain publish every Enigma app ships. The release zips — self-contained, single-file,
    named after the version — are built by scripts\publish.ps1 instead (docs\RELEASE.md step 7).

.EXAMPLE
    .\packaging\windows\publish.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File packaging\windows\publish.ps1

    Windows PowerShell's default policy refuses to run local scripts; this runs this one without
    changing the policy.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolved from this script rather than the current directory, so it runs from anywhere.
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..\src\Enigma.Licensing.Desktop')).Path

dotnet publish $project -c Release -r win-x64 -p:PublishReadyToRun=true

# A native command's failure is not a PowerShell error: pass its exit code on, so a failed publish
# fails the script.
exit $LASTEXITCODE
