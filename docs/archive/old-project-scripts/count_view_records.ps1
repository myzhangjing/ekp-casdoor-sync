$connectionString = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True"

Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $conn.Open()
    Write-Host "数据库连接成功" -ForegroundColor Green
    
    # 查询视图记录数
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM vw_org_structure_sync"
    $count = $cmd.ExecuteScalar()
    
    Write-Host "`n视图 vw_org_structure_sync 记录数: $count" -ForegroundColor Yellow
    
    if ($count -eq 177) {
        Write-Host "✗ 记录数为 177 - 视图未正确更新!" -ForegroundColor Red
    }
    elseif ($count -eq 368) {
        Write-Host "✓ 记录数为 368 - 视图正确!" -ForegroundColor Green
    }
    else {
        Write-Host "? 记录数为 $count - 需要进一步检查" -ForegroundColor Yellow
    }
    
    # 查询层级分布
    Write-Host "`n层级分布:" -ForegroundColor Cyan
    $cmd2 = $conn.CreateCommand()
    $cmd2.CommandText = @"
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
    
    $reader = $cmd2.ExecuteReader()
    while ($reader.Read()) {
        $level = $reader["level"]
        $cnt = $reader["cnt"]
        Write-Host "  层级 $level : $cnt 个组织" -ForegroundColor White
    }
    $reader.Close()
}
finally {
    $conn.Close()
}
