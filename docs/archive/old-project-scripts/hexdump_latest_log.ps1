$logDir = Join-Path $PSScriptRoot '..\logs'
$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { Write-Output 'NO_LOGS'; exit 0 }
Write-Output "LATEST_LOG: $($latest.FullName)"
$bytes = [System.IO.File]::ReadAllBytes($latest.FullName)
Write-Output "Size: $($bytes.Length) bytes"
$len = [Math]::Min(128, $bytes.Length)
$hex = ($bytes[0..($len-1)] | ForEach-Object { $_.ToString('X2') }) -join ' '
Write-Output "First $len bytes (hex):"
Write-Output $hex
