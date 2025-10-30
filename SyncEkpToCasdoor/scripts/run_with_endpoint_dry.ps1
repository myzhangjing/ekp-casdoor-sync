$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:CASDOOR_ENDPOINT = 'http://sso.fzcsps.com'
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'
$env:EKP_SQLSERVER_CONN = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dll = Join-Path (Get-Location) "bin\Release\net8.0\SyncEkpToCasdoor.dll"
Write-Output "Running dry-run with CASDOOR_ENDPOINT set to $env:CASDOOR_ENDPOINT"
& $dotnet $dll --dry-run
Write-Output "Finished."