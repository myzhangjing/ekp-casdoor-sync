@echo off
cd /d "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\bin\Release\net8.0"
echo ========== 应用修复后的视图 ==========
SyncEkpToCasdoor.exe --apply-optimized-views
echo.
echo ========== 查询张璟 ==========
SyncEkpToCasdoor.exe --peek-user 张璟
echo.
pause
