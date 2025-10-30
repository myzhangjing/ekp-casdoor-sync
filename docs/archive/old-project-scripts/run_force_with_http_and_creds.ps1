param(
  [string]$ClientId,
  [string]$ClientSecret,
  [string]$Endpoint = 'http://sso.fzcsps.com'
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'Provide clientId/secret via params or env'; exit 2 }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
Set-Location $projectRoot

$env:CASDOOR_CLIENT_ID = $ClientId
$env:CASDOOR_CLIENT_SECRET = $ClientSecret
$env:CASDOOR_ENDPOINT = $Endpoint
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'

Write-Output "Starting forced full sync (HTTP) with endpoint=$Endpoint"
& "$projectRoot\run-sync.ps1"
