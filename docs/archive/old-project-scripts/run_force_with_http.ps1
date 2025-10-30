Param(
    [string]$ClientId = 'aecd00a352e5c560ffe6',
    [string]$ClientSecret = '4402518b20dd191b8b48d6240bc786a4f847899a',
    [string]$Endpoint = 'http://sso.fzcsps.com'
)

$ErrorActionPreference = 'Stop'
$env:CASDOOR_CLIENT_ID = $ClientId
$env:CASDOOR_CLIENT_SECRET = $ClientSecret
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
$env:CASDOOR_ENDPOINT = $Endpoint

Write-Output "Running force full sync with CASDOOR_ENDPOINT=$Endpoint"

$script = Split-Path -Parent $MyInvocation.MyCommand.Definition
$runSync = Join-Path $script '..\run-sync.ps1'
& $runSync
