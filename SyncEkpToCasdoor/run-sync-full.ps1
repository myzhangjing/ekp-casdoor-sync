$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root
$env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"
Write-Host "Using SYNC_SINCE_UTC=$env:SYNC_SINCE_UTC"
. "$root\run-sync.ps1"
