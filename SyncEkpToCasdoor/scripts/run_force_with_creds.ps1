Param(
    [string]$ClientId = 'aecd00a352e5c560ffe6',
    [string]$ClientSecret = '4402518b20dd191b8b48d6240bc786a4f847899a'
)

$ErrorActionPreference = 'Stop'
$env:CASDOOR_CLIENT_ID = $ClientId
$env:CASDOOR_CLIENT_SECRET = $ClientSecret

Write-Output "Set CASDOOR_CLIENT_ID and CASDOOR_CLIENT_SECRET in this session."

$script = Split-Path -Parent $MyInvocation.MyCommand.Definition
$wrap = Join-Path $script 'run_force_full.ps1'
& $wrap
