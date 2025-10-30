# Read the latest log using UTF8 encoding to avoid mojibake
$logDir = Join-Path $PSScriptRoot '..\logs'
if (-not (Test-Path $logDir)) {
    Write-Output "LOG_DIR_NOT_FOUND: $logDir"
    exit 0
}
$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { Write-Output "NO_LOGS"; exit 0 }
Write-Output "LATEST_LOG: $($latest.FullName)"
Get-Content -Path $latest.FullName -Tail 1000 -Encoding utf8
