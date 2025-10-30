# Force full sync wrapper
$ErrorActionPreference = 'Stop'

$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
$env:CASDOOR_ENDPOINT = 'https://sso.fzcsps.com'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$runSync = Join-Path $root '..\run-sync.ps1'

Write-Output "Running force full sync (SYNC_SINCE_UTC=$env:SYNC_SINCE_UTC, CASDOOR_ENDPOINT=$env:CASDOOR_ENDPOINT)"
& $runSync
