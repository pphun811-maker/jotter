$exe = Join-Path $env:LOCALAPPDATA 'Programs\Jotter\Jotter.exe'
$p = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $p -Name 'Jotter' -Value ('"' + $exe + '"')
Write-Output ("Registered: " + (Get-ItemProperty -Path $p).Jotter)
