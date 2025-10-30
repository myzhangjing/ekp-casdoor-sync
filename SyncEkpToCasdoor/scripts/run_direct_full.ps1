$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

# Set required env variables (mirror run-sync.ps1)
$env:EKP_SQLSERVER_CONN = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;"
$env:CASDOOR_MYSQL_CONN = "Server=sso.fzcsps.com;Port=3306;Database=casdoor;User Id=root;Password=zhangjing;SslMode=None;"
$env:CASDOOR_DEFAULT_OWNER = "built-in"
$env:SYNC_SINCE_UTC = '1970-01-01T00:00:00Z'

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dll = Join-Path (Get-Location) "bin\Release\net8.0\SyncEkpToCasdoor.dll"

Write-Output "Running direct dotnet (FULL WRITE): $dotnet $dll"
# Run the binary directly so we can see console output live while it performs writes.
& $dotnet $dll

Write-Output "Direct full run finished."