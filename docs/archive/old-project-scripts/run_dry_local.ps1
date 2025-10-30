$ErrorActionPreference = "Stop"

# Run the Sync program in dry-run to inspect configuration captured by the app.
# This script mirrors the environment variables set by run-sync.ps1 and then runs the DLL with --dry-run.

Set-Location (Join-Path $PSScriptRoot '..')

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dll = Join-Path (Get-Location) "bin\Release\net8.0\SyncEkpToCasdoor.dll"

# Mirror environment variables used by run-sync.ps1
$env:EKP_SQLSERVER_CONN = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;"
$env:CASDOOR_MYSQL_CONN = "Server=sso.fzcsps.com;Port=3306;Database=casdoor;User Id=root;Password=zhangjing;SslMode=None;"
$env:CASDOOR_DEFAULT_OWNER = "built-in"
# Force Since override for testing full-sync behavior
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'

Write-Output "Running dry-run of Sync binary ($dll)"
& $dotnet $dll --dry-run

Write-Output "Dry-run finished."