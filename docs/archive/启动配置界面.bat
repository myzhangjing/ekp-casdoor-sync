@echo off
chcp 65001 >nul
echo =====================================
echo   EKP-Casdoor 同步工具 - 配置界面
echo =====================================
echo.

cd /d "%~dp0"

echo 检查项目文件...
if not exist "SyncEkpToCasdoor.UI\SyncEkpToCasdoor.UI.csproj" (
    echo [错误] 未找到项目文件
    pause
    exit /b 1
)

echo 检查编译文件...
if exist "SyncEkpToCasdoor.UI\bin\Release\net8.0-windows\SyncEkpToCasdoor.UI.exe" (
    echo [√] 找到已编译的程序，正在启动...
    start "" "SyncEkpToCasdoor.UI\bin\Release\net8.0-windows\SyncEkpToCasdoor.UI.exe"
    echo.
    echo [√] 程序已启动！
    timeout /t 2 >nul
    exit /b 0
)

echo 未找到编译文件，正在编译项目...
echo.
echo 提示: 首次编译可能需要下载 NuGet 包，请耐心等待...
echo.
dotnet build SyncEkpToCasdoor.UI\SyncEkpToCasdoor.UI.csproj -c Release

if %ERRORLEVEL% neq 0 (
    echo.
    echo [错误] 编译失败
    echo 常见原因:
    echo   1. 未安装 .NET 8 SDK
    echo   2. NuGet 包下载失败（检查网络连接）
    echo   3. 项目文件损坏
    echo.
    pause
    exit /b 1
)

echo.
echo [√] 编译成功，正在启动程序...
start "" "SyncEkpToCasdoor.UI\bin\Release\net8.0-windows\SyncEkpToCasdoor.UI.exe"
echo.
echo [√] 程序已启动！
echo.
echo 提示：
echo   - 首次使用请先配置 EKP 和 Casdoor 连接信息
echo   - 配置将加密保存到 sync_config.json
echo   - 点击"测试连接"按钮验证配置
echo.
timeout /t 3 >nul
