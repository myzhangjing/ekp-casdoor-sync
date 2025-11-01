$exe = "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\bin\Release\net8.0\SyncEkpToCasdoor.exe"

Write-Host "========== 应用修复后的视图 ==========" -ForegroundColor Cyan
& $exe --apply-optimized-views
Write-Host ""

Write-Host "========== 查询张璟 ==========" -ForegroundColor Cyan
& $exe --peek-user "张璟"
Write-Host ""

Write-Host "========== 查询技术管理部所有人员 ==========" -ForegroundColor Cyan
& $exe --peek-user "技术管理"
