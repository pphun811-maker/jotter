$exe = Join-Path $env:LOCALAPPDATA 'Programs\Jotter\Jotter.exe'
if (-not (Test-Path $exe)) { Write-Output "NOT-INSTALLED: $exe"; exit 1 }
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
Write-Output ("PID: " + $p.Id + "  HasExited: " + $p.HasExited)
