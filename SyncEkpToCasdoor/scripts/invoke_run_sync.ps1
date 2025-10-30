param(
  [string]$ClientId,
  [string]$ClientSecret,
  [string]$Endpoint = 'http://sso.fzcsps.com'
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root
Set-Location ..

if ($ClientId) { $env:CASDOOR_CLIENT_ID = $ClientId }
if ($ClientSecret) { $env:CASDOOR_CLIENT_SECRET = $ClientSecret }
if ($Endpoint) { $env:CASDOOR_ENDPOINT = $Endpoint }
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'

Write-Output "Invoking run-sync.ps1 from $((Get-Location).Path)"
.\run-sync.ps1
