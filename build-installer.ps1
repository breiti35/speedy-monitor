# Baut beide Setups:
#   installer\Output\SpeedyMonitor_Setup_<Version>.exe             (klein, braucht .NET 8 Desktop Runtime)
#   installer\Output\SpeedyMonitor_Setup_<Version>_Standalone.exe  (mit eingebauter Laufzeit)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src\NetSpeedMonitor\NetSpeedMonitor.csproj'

dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (framework-abhaengig) fehlgeschlagen" }

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-standalone
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (Standalone) fehlgeschlagen" }

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 nicht gefunden: $iscc" }

& $iscc /Q installer\SpeedyMonitor.iss
if ($LASTEXITCODE -ne 0) { throw "ISCC fehlgeschlagen" }
& $iscc /Q /DStandalone installer\SpeedyMonitor.iss
if ($LASTEXITCODE -ne 0) { throw "ISCC (Standalone) fehlgeschlagen" }

Get-ChildItem installer\Output\*.exe | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } }
