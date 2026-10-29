@echo off
rem Publishes Enigma Licensing (Enigma.Licensing.Desktop) for Windows x64. Runs, from wherever it is started:
rem
rem     dotnet publish .\src\Enigma.Licensing.Desktop\ -c Release -r win-x64 -p:PublishReadyToRun=true
rem
rem A Release build for win-x64, compiled ReadyToRun so it starts faster, which runs on an installed
rem .NET 10 runtime. The result lands in
rem
rem     src\Enigma.Licensing.Desktop\bin\Release\net10.0\win-x64\publish\
rem
rem and dotnet's last line names that folder. Start Enigma.Licensing.Desktop.exe in it.
rem
rem The same publish as publish.ps1, for a cmd prompt: no PowerShell execution policy applies. The release
rem zips - self-contained, single-file, named after the version - are built by scripts\publish.ps1 instead.

setlocal

rem Resolved from this script rather than the current directory, so it runs from anywhere.
set "project=%~dp0..\..\src\Enigma.Licensing.Desktop"

dotnet publish "%project%" -c Release -r win-x64 -p:PublishReadyToRun=true

rem dotnet's exit code is the script's, so a failed publish fails the script.
exit /b %ERRORLEVEL%
