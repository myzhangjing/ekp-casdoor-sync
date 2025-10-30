# Force a full sync by setting SYNC_SINCE_UTC to epoch and calling run-sync.ps1
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
Write-Output "Forcing full sync: setting SYNC_SINCE_UTC to 1970-01-01T00:00:00Z"
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
# Backup and remove existing checkpoint if present
if (Test-Path -Path 'sync_state.json') {
    $ts = Get-Date -Format 'yyyyMMdd_HHmmss'
    Copy-Item sync_state.json "sync_state.json.$ts.bak"
    Remove-Item sync_state.json -Force
    Write-Output "Backed up and removed sync_state.json"
} else {
    Write-Output "No sync_state.json found"
}
# Call the existing runner
Write-Output "Calling run-sync.ps1 (this will perform write operations to Casdoor)"
& "$PSScriptRoot\..\run-sync.ps1"
Write-Output "run-sync.ps1 finished"
