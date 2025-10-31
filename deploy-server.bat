@echo off
chcp 65001 >nul
echo ========================================
echo   远程服务器部署工具
echo ========================================
echo.

set SERVER=172.16.10.110
set USER=root
set PASS=fzwater@163.com

echo 目标服务器: %USER%@%SERVER%
echo 密码: %PASS%
echo.
echo ========================================
echo   开始部署
echo ========================================
echo.

echo [步骤 1/2] 上传部署脚本...
echo.
echo 提示: 请输入密码 %PASS%
echo.

scp auto-deploy-server.sh %USER%@%SERVER%:/tmp/auto-deploy.sh

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [错误] 上传失败
    echo.
    echo 请手动执行:
    echo   scp auto-deploy-server.sh %USER%@%SERVER%:/tmp/auto-deploy.sh
    echo   ssh %USER%@%SERVER%
    echo   chmod +x /tmp/auto-deploy.sh
    echo   /tmp/auto-deploy.sh
    echo.
    pause
    exit /b 1
)

echo.
echo [步骤 2/2] 执行部署脚本...
echo.
echo 提示: 请再次输入密码 %PASS%
echo.

ssh %USER%@%SERVER% "chmod +x /tmp/auto-deploy.sh && bash /tmp/auto-deploy.sh"

echo.
echo ========================================
echo   部署完成!
echo ========================================
echo.
echo 访问地址: http://%SERVER%:5233/login
echo.
echo 管理命令:
echo   ssh %USER%@%SERVER% "systemctl status ekp-casdoor-sync"
echo   ssh %USER%@%SERVER% "systemctl restart ekp-casdoor-sync"
echo   ssh %USER%@%SERVER% "journalctl -u ekp-casdoor-sync -f"
echo.
pause
