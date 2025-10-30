# Dump top rows from EKP views as JSON for inspection
$cs = 'Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;'
$views = @('vw_casdoor_users_sync','vw_org_structure_sync')
foreach ($v in $views) {
    Write-Output "=== $v ==="
    $sql = "SELECT TOP 20 * FROM [$v]"
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $conn.Open()
    $reader = $cmd.ExecuteReader()
    $table = New-Object System.Data.DataTable
    $table.Load($reader)
    $conn.Close()
    # Print as indented JSON
    $json = $table | ConvertTo-Json -Depth 5
    Write-Output $json
}
