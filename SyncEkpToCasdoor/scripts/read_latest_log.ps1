# Read the latest log from SyncEkpToCasdoor/logs and print the last 1000 lines
$logDir = Join-Path $PSScriptRoot '..\logs'
if (-not (Test-Path $logDir)) {
    Write-Output "LOG_DIR_NOT_FOUND: $logDir"
    exit 0
}

$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) {
    Write-Output "NO_LOGS"
    exit 0
}

Write-Output "LATEST_LOG: $($latest.FullName)"
Get-Content $latest.FullName -Tail 1000
