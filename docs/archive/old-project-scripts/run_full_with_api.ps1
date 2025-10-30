$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

Write-Output "Setting Casdoor HTTP API creds and forcing full sync (SYNC_SINCE_UTC = epoch)"
$env:CASDOOR_ENDPOINT = 'http://sso.fzcsps.com'
$env:CASDOOR_CLIENT_ID = 'aecd00a352e5c560ffe6'
$env:CASDOOR_CLIENT_SECRET = '4402518b20dd191b8b48d6240bc786a4f847899a'
$env:CASDOOR_ORGANIZATION = 'fzswjtOrganization'
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'

if (Test-Path -Path 'sync_state.json') {
    $ts = Get-Date -Format 'yyyyMMdd_HHmmss'
    Copy-Item sync_state.json "sync_state.json.$ts.bak"
    Remove-Item sync_state.json -Force
    Write-Output "Backed up and removed sync_state.json"
} else {
    Write-Output "No sync_state.json found"
}

Write-Output "Calling run-sync.ps1 (this will perform write operations to Casdoor via HTTP API)"
& "$PSScriptRoot\..\run-sync.ps1"
Write-Output "run-sync.ps1 finished"