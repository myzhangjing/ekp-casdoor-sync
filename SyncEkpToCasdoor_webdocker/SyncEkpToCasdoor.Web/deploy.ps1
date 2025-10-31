# -*- coding: utf-8 -*-
# Deploy SyncEkpToCasdoor.Web to Server
param(
    [string]$ServerIP = "172.16.10.110",
    [string]$ServerUser = "root",
    [string]$ServerPassword = "fwater@163.com",
    [string]$DeployPath = "/opt/syncekp-web",
    [string]$AppName = "syncekp-casdoor-web"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "Deploy SyncEkpToCasdoor.Web to Server"
Write-Host "Server: $ServerIP"
Write-Host "Port: 9000"
Write-Host "========================================"
Write-Host ""

# Step 1: Create deployment package
Write-Host "[1/5] Creating deployment package..." -ForegroundColor Green
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$packageName = "deploy_$timestamp.zip"

$files = @(
    "Dockerfile",
    "docker-compose.yml",
    ".dockerignore",
    "appsettings.json",
    "appsettings.Production.json",
    "SyncEkpToCasdoor.Web.csproj",
    "Program.cs",
    "Components",
    "Controllers",
    "Models",
    "Services",
    "wwwroot"
)

Compress-Archive -Path $files -DestinationPath $packageName -Force
Write-Host "Package created: $packageName" -ForegroundColor Gray
Write-Host ""

# Step 2: Upload to server
Write-Host "[2/5] Uploading to server..." -ForegroundColor Green
Write-Host "Password: $ServerPassword" -ForegroundColor Yellow

try {
    # Upload file
    & scp -o StrictHostKeyChecking=no $packageName ${ServerUser}@${ServerIP}:/tmp/
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Upload failed. Please install OpenSSH client." -ForegroundColor Red
        exit 1
    }
    Write-Host "Upload completed" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 3: Extract files on server
Write-Host "[3/5] Extracting files on server..." -ForegroundColor Green

$cmd1 = @"
mkdir -p $DeployPath && cd $DeployPath && unzip -o /tmp/$packageName && rm /tmp/$packageName && echo 'Extract completed'
"@

& ssh -o StrictHostKeyChecking=no ${ServerUser}@${ServerIP} $cmd1
Write-Host ""

# Step 4: Stop old container
Write-Host "[4/5] Stopping old container..." -ForegroundColor Green

$cmd2 = @"
cd $DeployPath && docker stop $AppName 2>/dev/null || true && docker rm $AppName 2>/dev/null || true && echo 'Old container removed'
"@

& ssh ${ServerUser}@${ServerIP} $cmd2
Write-Host ""

# Step 5: Build and start new container
Write-Host "[5/5] Building and starting new container..." -ForegroundColor Green

$cmd3 = @"
cd $DeployPath && docker-compose build && docker-compose up -d && sleep 5 && docker ps | grep $AppName
"@

& ssh ${ServerUser}@${ServerIP} $cmd3

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "========================================"
    Write-Host ""
    Write-Host "Access URL:" -ForegroundColor Yellow
    Write-Host "  Internal: http://${ServerIP}:9000"
    Write-Host "  External: http://syn-ekp.fzcsps.com:9000"
    Write-Host ""
    Write-Host "Check logs:" -ForegroundColor Yellow
    Write-Host "  ssh ${ServerUser}@${ServerIP}"
    Write-Host "  docker logs -f $AppName"
    Write-Host ""
} else {
    Write-Host "Deployment failed!" -ForegroundColor Red
}

# Cleanup
Remove-Item $packageName -Force -ErrorAction SilentlyContinue
