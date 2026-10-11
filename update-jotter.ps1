# Jotter update: stop running instance -> copy dist\Jotter.exe -> restart.
# Usage: powershell -ExecutionPolicy Bypass -File update-jotter.ps1
$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
$src = Join-Path $repo "dist\Jotter.exe"
$dst = Join-Path $env:LOCALAPPDATA "Programs\Jotter\Jotter.exe"

if (-not (Test-Path $src)) { throw "dist\Jotter.exe not found. Run dotnet publish first." }

Stop-Process -Name Jotter -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
Copy-Item $src $dst -Force
Start-Process $dst
Write-Host "Jotter updated and restarted. (Draft in the popup, if any, was discarded by the restart.)"
