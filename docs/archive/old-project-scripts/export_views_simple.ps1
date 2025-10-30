# Export two EKP views to CSV files (simple script)
$ErrorActionPreference = 'Stop'
$cs = 'Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;'
$outDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) '..\SyncEkpToCasdoor\logs'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

function Export-View($viewName, $top) {
    Write-Output "Exporting $viewName..."
    $sql = "SELECT TOP $top * FROM [$viewName]"
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $conn.Open()
    $reader = $cmd.ExecuteReader()
    $dt = New-Object System.Data.DataTable
    $dt.Load($reader)
    $conn.Close()
    $csv = Join-Path $outDir "${viewName}_sample.csv"
    $dt | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8
    $countFile = Join-Path $outDir "${viewName}_count.txt"
    $dt.Rows.Count | Out-File -FilePath $countFile -Encoding UTF8
    Write-Output "$viewName exported to $csv ($($dt.Rows.Count) rows)"
}

Export-View 'vw_casdoor_users_sync' 100
Export-View 'vw_org_structure_sync' 100
Write-Output 'Export completed.'
