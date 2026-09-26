# Baut die App (framework-abhaengige Single-File-exe) und daraus das Setup.
# Ergebnis: installer\Output\SpeedyMonitor_Setup_<Version>.exe
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet publish src\NetSpeedMonitor\NetSpeedMonitor.csproj -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen" }

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 nicht gefunden: $iscc" }

& $iscc installer\SpeedyMonitor.iss
if ($LASTEXITCODE -ne 0) { throw "ISCC fehlgeschlagen" }

Get-ChildItem installer\Output\*.exe | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } }
