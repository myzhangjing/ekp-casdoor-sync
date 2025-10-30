# Collect EKP view samples, fetch Casdoor swagger, then run full sync
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

$cs = 'Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;'
$views = @('vw_casdoor_users_sync','vw_org_structure_sync')
$logDir = Join-Path $root '..\SyncEkpToCasdoor\logs'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# Function to export a view to CSV and return row count
function Export-ViewSample {
    param($viewName, $top=100)
    $sql = "SELECT TOP $top * FROM [$viewName]"
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $conn.Open()
    $reader = $cmd.ExecuteReader()
    $table = New-Object System.Data.DataTable
    $table.Load($reader)
    $conn.Close()

    $csvPath = Join-Path $logDir ("${viewName}_sample.csv")
    try {
        $table | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    } catch {
        Write-Output "Failed to export $viewName to CSV: $_"
    }
    $count = $table.Rows.Count
    $count
}

# Export samples and counts
$results = @{}
foreach ($v in $views) {
    Write-Output "Exporting view $v ..."
    $c = Export-ViewSample -viewName $v -top 100
    $results[$v] = $c
    Write-Output "$v rows exported: $c"
}

# Save counts to file
$countsFile = Join-Path $logDir 'view_counts.txt'
$results.Keys | ForEach-Object { "$_ : $($results[$_])" } | Set-Content -Path $countsFile -Encoding UTF8

# Try fetch Casdoor swagger
$casdoorBase = 'http://sso.fzcsps.com'
$swaggerPaths = @('/swagger', '/swagger/index.html', '/swagger/v1/swagger.json', '/swagger.json')
foreach ($p in $swaggerPaths) {
    $url = "$casdoorBase$p"
    try {
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
        $outPath = Join-Path $logDir (('casdoor_swagger' + ($p -replace '/','_') + '.html').Trim('_'))
        $resp.Content | Out-File -FilePath $outPath -Encoding UTF8
        Write-Output "Saved swagger response from $url to $outPath"
    } catch {
        Write-Output "Could not fetch $url : $_"
    }
}

# Run full sync: remove checkpoint then call run-sync.ps1 with ExecutionPolicy Bypass in a child process
$fullSyncCmd = {
    param($envs)
    foreach ($k in $envs.Keys) { $env:$k = $envs[$k] }
    # Remove checkpoint if exists
    $ck = Join-Path $root '..\sync_state.json'
    if (Test-Path $ck) { Move-Item -Path $ck -Destination ($ck + '.bak') -Force }
    & "$root\run-sync.ps1"
}

$envVars = @{ 'CASDOOR_ENDPOINT' = 'http://sso.fzcsps.com'; 'CASDOOR_CLIENT_ID' = 'aecd00a352e5c560ffe6'; 'CASDOOR_CLIENT_SECRET' = '4402518b20dd191b8b48d6240bc786a4f847899a'; 'CASDOOR_ORGANIZATION' = 'fzswjtOrganization' }

# Launch child PowerShell to run full sync (bypass policy)
$encodedScript = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes("& {`$envs = @(); foreach (
`$k in $((New-Object System.Collections.Hashtable)@())) { } }"))
# Simpler: call powershell -ExecutionPolicy Bypass -NoProfile -Command "& { set envs; Remove checkpoint; & run-sync.ps1 }"
$childCmd = "powershell.exe -ExecutionPolicy Bypass -NoProfile -Command &{"
foreach ($k in $envVars.Keys) { $v = $envVars[$k]; $childCmd += " `$env:$k='$v';" }
$childCmd += " if (Test-Path '$root\..\sync_state.json') { Move-Item -Path '$root\..\sync_state.json' -Destination '$root\..\sync_state.json.bak' -Force }; & '$root\run-sync.ps1' }"

Write-Output "Starting full sync child process..."
Write-Output $childCmd

# Start child and wait
Invoke-Expression $childCmd

Write-Output "Collect and sync script completed."
