# Try reading the latest log with multiple encodings to detect correct one
$logDir = Join-Path $PSScriptRoot '..\logs'
if (-not (Test-Path $logDir)) { Write-Output "LOG_DIR_NOT_FOUND: $logDir"; exit 0 }
$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { Write-Output "NO_LOGS"; exit 0 }
Write-Output "LATEST_LOG: $($latest.FullName)"

$encodings = @('UTF8','Unicode','BigEndianUnicode','UTF7','UTF32','Default','ASCII')
foreach ($enc in $encodings) {
    Write-Output "\n=== READING AS $enc ==="
    try {
        Get-Content -Path $latest.FullName -Encoding $enc -TotalCount 200 | ForEach-Object { Write-Output $_ }
    } catch {
        Write-Output "(failed to read as $enc): $_"
    }
}
