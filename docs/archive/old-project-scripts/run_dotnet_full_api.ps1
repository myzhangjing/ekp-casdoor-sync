$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

Write-Output "Running dotnet directly with Casdoor HTTP API creds (full write)"
$env:CASDOOR_ENDPOINT = 'http://sso.fzcsps.com'
$env:CASDOOR_CLIENT_ID = 'aecd00a352e5c560ffe6'
$env:CASDOOR_CLIENT_SECRET = '4402518b20dd191b8b48d6240bc786a4f847899a'
$env:CASDOOR_ORGANIZATION = 'fzswjtOrganization'
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
$env:EKP_SQLSERVER_CONN = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;"

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dll = Join-Path (Get-Location) "bin\Release\net8.0\SyncEkpToCasdoor.dll"

& $dotnet $dll
Write-Output "Dotnet run finished."