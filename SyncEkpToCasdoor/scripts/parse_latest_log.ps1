$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$logsDir = Join-Path $root '..\logs'
$latest = Get-ChildItem -Path (Join-Path $logsDir 'sync_*.log') | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $latest) { Write-Error "No log files found in $logsDir"; exit 1 }
Write-Output "Latest: $($latest.FullName)"

$patterns = @('\[Groups\] upserted:','\[Users\] upserted:','\[Membership\] ensured:','组织同步完成','用户同步完成','Membership','upserted:','ensured:','Checkpoint saved','Sync finished')
$results = Select-String -Path $latest.FullName -Pattern $patterns -AllMatches -Encoding UTF8
if ($results) {
    Write-Output "-- Matched summary lines --"
    $results | ForEach-Object { $_.Line } | Select-Object -Unique
} else {
    Write-Output "No direct summary lines matched; printing last 300 lines as fallback:"
    Get-Content $latest.FullName -Encoding UTF8 -Tail 300
}
Write-Output "\n-- Full last 500 lines (for debugging) --"
Get-Content $latest.FullName -Encoding UTF8 -Tail 500
