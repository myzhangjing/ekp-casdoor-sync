$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

Write-Output "Setting SYNC_SINCE_UTC to epoch and removing checkpoint (if exists)"
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
if (Test-Path -Path 'sync_state.json') {
    $ts = Get-Date -Format 'yyyyMMdd_HHmmss'
    Copy-Item sync_state.json "sync_state.json.$ts.bak"
    Remove-Item sync_state.json -Force
    Write-Output "Backed up and removed sync_state.json"
} else {
    Write-Output "No sync_state.json found"
}

Write-Output "Calling run-sync.ps1 (this will perform write operations to Casdoor)"
& "$PSScriptRoot\..\run-sync.ps1"
Write-Output "run-sync.ps1 finished"