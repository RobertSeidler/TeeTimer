# Baut die Single-File-EXE und packt sie in ein per-User-MSI.
# Ergebnis: dist\TeeTimer.msi
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent                 # Repo-Root
$app = Join-Path $root 'src\TeeTimer\TeeTimer.csproj'
$dist = Join-Path $root 'dist'
$payload = Join-Path $dist 'app'

Write-Host "==> 1/2  dotnet publish (Single-File-EXE)..."
dotnet publish $app -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $payload | Out-Null

Write-Host "==> 2/2  WiX: MSI bauen..."
$msi = Join-Path $dist 'TeeTimer.msi'
wix build (Join-Path $PSScriptRoot 'TeeTimer.wxs') -o $msi -arch x64 `
    -bindpath $payload `
    -bindpath (Join-Path $root 'src\TeeTimer\Assets')

Write-Host ""
Write-Host "Fertig: $msi ($([math]::Round((Get-Item $msi).Length/1MB,1)) MB)"
Write-Host "Installation:        Doppelklick auf die MSI (per-User, kein Admin)"
Write-Host "Ohne Autostart:     msiexec /i TeeTimer.msi AUTOSTART=`"`""
