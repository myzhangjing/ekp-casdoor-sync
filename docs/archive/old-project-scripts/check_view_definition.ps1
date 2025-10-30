# 检查视图定义是否使用了正确的字段优先级
$connectionString = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True"

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

Write-Host "1. 查看视图定义中的 COALESCE 语句..." -ForegroundColor Cyan
$query = @"
SELECT definition 
FROM sys.sql_modules 
WHERE object_id = OBJECT_ID('vw_org_structure_sync')
"@

$cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    $definition = $reader["definition"].ToString()
    
    # 提取关键的 COALESCE 行
    $lines = $definition -split "`n" | Where-Object { $_ -match "COALESCE.*fd_parent" }
    
    Write-Host "`n关键字段定义:" -ForegroundColor Yellow
    foreach ($line in $lines) {
        Write-Host "  $($line.Trim())" -ForegroundColor White
    }
    
    # 检查字段顺序
    if ($definition -match "COALESCE\(e\.fd_parentid\s*,\s*e\.fd_parentorgid\)") {
        Write-Host "`n✓ 视图使用正确的字段顺序: fd_parentid 优先" -ForegroundColor Green
    }
    elseif ($definition -match "COALESCE\(e\.fd_parentorgid\s*,\s*e\.fd_parentorgid\)") {
        Write-Host "`n✗ 视图使用错误的字段顺序: fd_parentorgid 优先 (会跳过中间层级!)" -ForegroundColor Red
    }
    else {
        Write-Host "`n? 未检测到 COALESCE 语句" -ForegroundColor Yellow
    }
}
$reader.Close()

Write-Host "`n2. 检查视图返回的记录数..." -ForegroundColor Cyan
$countQuery = "SELECT COUNT(*) as total FROM vw_org_structure_sync"
$cmd2 = New-Object System.Data.SqlClient.SqlCommand($countQuery, $conn)
$total = $cmd2.ExecuteScalar()
Write-Host "  视图返回记录数: $total" -ForegroundColor $(if ($total -eq 368) { "Green" } else { "Red" })

Write-Host "`n3. 检查各层级数量分布..." -ForegroundColor Cyan
$depthQuery = @"
SELECT 
    CASE 
        WHEN parent_id IS NULL THEN 0
        WHEN parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL) THEN 1
        WHEN parent_id IN (
            SELECT id FROM vw_org_structure_sync 
            WHERE parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL)
        ) THEN 2
        WHEN parent_id IN (
            SELECT id FROM vw_org_structure_sync v2
            WHERE v2.parent_id IN (
                SELECT id FROM vw_org_structure_sync v3
                WHERE v3.parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL)
            )
        ) THEN 3
        ELSE 4
    END as level,
    COUNT(*) as cnt
FROM vw_org_structure_sync v
GROUP BY 
    CASE 
        WHEN parent_id IS NULL THEN 0
        WHEN parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL) THEN 1
        WHEN parent_id IN (
            SELECT id FROM vw_org_structure_sync 
            WHERE parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL)
        ) THEN 2
        WHEN parent_id IN (
            SELECT id FROM vw_org_structure_sync v2
            WHERE v2.parent_id IN (
                SELECT id FROM vw_org_structure_sync v3
                WHERE v3.parent_id IN (SELECT id FROM vw_org_structure_sync WHERE parent_id IS NULL)
            )
        ) THEN 3
        ELSE 4
    END
ORDER BY level
"@

$cmd3 = New-Object System.Data.SqlClient.SqlCommand($depthQuery, $conn)
$reader3 = $cmd3.ExecuteReader()
while ($reader3.Read()) {
    $level = $reader3["level"]
    $cnt = $reader3["cnt"]
    Write-Host "  层级 $level : $cnt 个组织" -ForegroundColor White
}
$reader3.Close()

$conn.Close()
Write-Host "`n完成！" -ForegroundColor Cyan
