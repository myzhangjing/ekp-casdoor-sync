$connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;"

Write-Host "正在连接到EKP数据库..." -ForegroundColor Cyan

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    Write-Host "✓ 连接成功" -ForegroundColor Green
    
    # 检查用户视图是否存在
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_NAME = 'vw_casdoor_users_sync'"
    $viewExists = $cmd.ExecuteScalar()
    
    if ($viewExists -eq 1) {
        Write-Host "✓ 视图 vw_casdoor_users_sync 存在" -ForegroundColor Green
        
        # 查询总记录数
        $cmd.CommandText = "SELECT COUNT(*) FROM vw_casdoor_users_sync"
        $totalCount = $cmd.ExecuteScalar()
        Write-Host "视图总记录数: $totalCount" -ForegroundColor Yellow
        
        if ($totalCount -gt 0) {
            # 显示前10条记录
            Write-Host "`n前10条用户记录:" -ForegroundColor Cyan
            $cmd.CommandText = "SELECT TOP 10 id, username, display_name, dept_id, company_name, affiliation FROM vw_casdoor_users_sync ORDER BY updated_at DESC"
            $reader = $cmd.ExecuteReader()
            
            Write-Host ("=" * 150)
            Write-Host ("{0,-20} {1,-20} {2,-15} {3,-35} {4,-30} {5,-30}" -f "ID", "Username", "Display Name", "Dept ID", "Company", "Affiliation")
            Write-Host ("=" * 150)
            
            while ($reader.Read()) {
                $id = if ($reader.IsDBNull(0)) { "" } else { $reader.GetString(0) }
                $username = if ($reader.IsDBNull(1)) { "" } else { $reader.GetString(1) }
                $display = if ($reader.IsDBNull(2)) { "" } else { $reader.GetString(2) }
                $dept = if ($reader.IsDBNull(3)) { "" } else { $reader.GetString(3) }
                $company = if ($reader.IsDBNull(4)) { "" } else { $reader.GetString(4) }
                $aff = if ($reader.IsDBNull(5)) { "" } else { $reader.GetString(5) }
                
                Write-Host ("{0,-20} {1,-20} {2,-15} {3,-35} {4,-30} {5,-30}" -f $id, $username, $display, $dept, $company, $aff)
            }
            $reader.Close()
            
            # 搜索"张"姓用户
            Write-Host "`n搜索包含'张'的用户:" -ForegroundColor Cyan
            $cmd.CommandText = "SELECT id, username, display_name, affiliation FROM vw_casdoor_users_sync WHERE display_name LIKE '%张%'"
            $reader = $cmd.ExecuteReader()
            
            $found = 0
            while ($reader.Read()) {
                $found++
                $id = if ($reader.IsDBNull(0)) { "" } else { $reader.GetString(0) }
                $username = if ($reader.IsDBNull(1)) { "" } else { $reader.GetString(1) }
                $display = if ($reader.IsDBNull(2)) { "" } else { $reader.GetString(2) }
                $aff = if ($reader.IsDBNull(3)) { "" } else { $reader.GetString(3) }
                
                Write-Host "  $display ($username) - $aff"
            }
            $reader.Close()
            
            if ($found -eq 0) {
                Write-Host "  未找到包含'张'的用户" -ForegroundColor Red
            } else {
                Write-Host "  共找到 $found 个用户" -ForegroundColor Green
            }
        } else {
            Write-Host "⚠ 视图为空！可能原因:" -ForegroundColor Yellow
            Write-Host "  1. 目标公司ID配置不正确"
            Write-Host "  2. 用户未设置登录名"
            Write-Host "  3. 用户未关联到有效部门"
        }
    } else {
        Write-Host "✗ 视图 vw_casdoor_users_sync 不存在！" -ForegroundColor Red
        Write-Host "请运行: .\SyncEkpToCasdoor.exe --apply-optimized-views 创建视图" -ForegroundColor Yellow
    }
    
    $conn.Close()
} catch {
    Write-Host "✗ 错误: $_" -ForegroundColor Red
}
