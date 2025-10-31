@echo off
REM Deploy SyncEkpToCasdoor.Web to Server using PSCP/PLINK

set SERVER_IP=172.16.10.110
set SERVER_USER=root
set SERVER_PASS=fwater@163.com
set DEPLOY_PATH=/opt/syncekp-web
set APP_NAME=syncekp-casdoor-web

echo ========================================
echo Deploy SyncEkpToCasdoor.Web to Server
echo Server: %SERVER_IP%:9000
echo ========================================
echo.

REM Step 1: Create package
echo [1/5] Creating deployment package...
for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c%%a%%b)
for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a%%b)
set PACKAGE_NAME=deploy_%mydate%_%mytime%.zip

powershell -Command "Compress-Archive -Path 'Dockerfile','docker-compose.yml','.dockerignore','appsettings.json','appsettings.Production.json','SyncEkpToCasdoor.Web.csproj','Program.cs','Components','Controllers','Models','Services','wwwroot' -DestinationPath '%PACKAGE_NAME%' -Force"

echo Package created: %PACKAGE_NAME%
echo.

REM Step 2: Upload using pscp (PuTTY SCP)
echo [2/5] Uploading to server...
echo Using PSCP (PuTTY)...

REM Try pscp first
where pscp >nul 2>&1
if %errorlevel% equ 0 (
    echo y | pscp -pw %SERVER_PASS% %PACKAGE_NAME% %SERVER_USER%@%SERVER_IP%:/tmp/
) else (
    echo PSCP not found, trying scp...
    echo Note: You may need to enter password: %SERVER_PASS%
    scp -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null %PACKAGE_NAME% %SERVER_USER%@%SERVER_IP%:/tmp/
)

if %errorlevel% neq 0 (
    echo Upload failed!
    echo Please install PuTTY PSCP or ensure OpenSSH works
    pause
    exit /b 1
)

echo Upload completed
echo.

REM Step 3-5: Execute remote commands
echo [3/5] Extracting files...
echo [4/5] Stopping old container...
echo [5/5] Building and starting new container...
echo.

REM Try plink first
where plink >nul 2>&1
if %errorlevel% equ 0 (
    plink -batch -pw %SERVER_PASS% %SERVER_USER%@%SERVER_IP% "mkdir -p %DEPLOY_PATH% && cd %DEPLOY_PATH% && unzip -o /tmp/%PACKAGE_NAME% && rm /tmp/%PACKAGE_NAME% && docker stop %APP_NAME% 2>/dev/null || true && docker rm %APP_NAME% 2>/dev/null || true && docker-compose build && docker-compose up -d && sleep 5 && docker ps | grep %APP_NAME%"
) else (
    echo PLINK not found, trying ssh...
    ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null %SERVER_USER%@%SERVER_IP% "mkdir -p %DEPLOY_PATH% && cd %DEPLOY_PATH% && unzip -o /tmp/%PACKAGE_NAME% && rm /tmp/%PACKAGE_NAME% && docker stop %APP_NAME% 2>/dev/null || true && docker rm %APP_NAME% 2>/dev/null || true && docker-compose build && docker-compose up -d && sleep 5 && docker ps | grep %APP_NAME%"
)

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo Deployment completed successfully!
    echo ========================================
    echo.
    echo Access URL:
    echo   Internal: http://%SERVER_IP%:9000
    echo   External: http://syn-ekp.fzcsps.com:9000
    echo.
    echo Check logs:
    echo   ssh %SERVER_USER%@%SERVER_IP%
    echo   docker logs -f %APP_NAME%
    echo.
) else (
    echo.
    echo Deployment failed!
    echo.
)

REM Cleanup
del %PACKAGE_NAME% 2>nul

pause
