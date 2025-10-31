#!/usr/bin/env pwsh
# Automated deployment script using .NET SSH library

$SERVER_IP = "172.16.10.110"
$SERVER_USER = "root"
$SERVER_PASS = "fwater@163.com"
$DEPLOY_PATH = "/opt/syncekp-web"
$APP_NAME = "syncekp-casdoor-web"
$PACKAGE = "deploy_20251031_165043.zip"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Starting automated deployment..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Using plain scp with expect-like behavior via cmdkey (Windows credential manager)
Write-Host "[1/3] Uploading package to server..." -ForegroundColor Green

# Create a temporary expect-like script for Windows
$uploadScript = @"
`$pass = '$SERVER_PASS'
`$secpass = ConvertTo-SecureString `$pass -AsPlainText -Force
`$cred = New-Object System.Management.Automation.PSCredential ('$SERVER_USER', `$secpass)

# Using WinSCP COM object if available
try {
    `$sessionOptions = New-Object WinSCP.SessionOptions -Property @{
        Protocol = [WinSCP.Protocol]::Sftp
        HostName = '$SERVER_IP'
        UserName = '$SERVER_USER'
        Password = '$SERVER_PASS'
        GiveUpSecurityAndAcceptAnySshHostKey = `$true
    }
    
    `$session = New-Object WinSCP.Session
    `$session.Open(`$sessionOptions)
    `$session.PutFiles('$PACKAGE', '/tmp/').Check()
    `$session.Close()
    Write-Host 'Upload completed via WinSCP'
} catch {
    Write-Host 'WinSCP not available, using scp with password input'
    # Fallback to manual scp
    `$null
}
"@

# Try alternative: Use plink if available
$plinkPath = Get-Command plink -ErrorAction SilentlyContinue

if ($plinkPath) {
    Write-Host "Using PLINK for deployment..." -ForegroundColor Yellow
    
    # Upload with pscp
    $pscp = Get-Command pscp -ErrorAction SilentlyContinue
    if ($pscp) {
        Write-Host "Uploading with PSCP..." -ForegroundColor Gray
        echo y | pscp -batch -pw $SERVER_PASS $PACKAGE ${SERVER_USER}@${SERVER_IP}:/tmp/
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Upload completed!" -ForegroundColor Green
            Write-Host ""
            
            Write-Host "[2/3] Deploying on server..." -ForegroundColor Green
            
            $deployCmd = "cd /opt && mkdir -p syncekp-web && cd syncekp-web && unzip -o /tmp/$PACKAGE && rm /tmp/$PACKAGE && docker stop $APP_NAME 2>/dev/null || true && docker rm $APP_NAME 2>/dev/null || true && docker-compose build && docker-compose up -d && sleep 5 && docker ps | grep syncekp"
            
            plink -batch -pw $SERVER_PASS ${SERVER_USER}@${SERVER_IP} $deployCmd
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "[3/3] Verifying deployment..." -ForegroundColor Green
                Start-Sleep -Seconds 3
                
                try {
                    $response = Invoke-WebRequest -Uri "http://${SERVER_IP}:9000/login" -TimeoutSec 10 -UseBasicParsing
                    if ($response.StatusCode -eq 200) {
                        Write-Host ""
                        Write-Host "========================================" -ForegroundColor Cyan
                        Write-Host "Deployment completed successfully!" -ForegroundColor Green
                        Write-Host "========================================" -ForegroundColor Cyan
                        Write-Host ""
                        Write-Host "Access URLs:" -ForegroundColor Yellow
                        Write-Host "  Internal: http://${SERVER_IP}:9000" -ForegroundColor White
                        Write-Host "  External: http://syn-ekp.fzcsps.com:9000" -ForegroundColor White
                        Write-Host ""
                    }
                } catch {
                    Write-Host "Application may still be starting..." -ForegroundColor Yellow
                    Write-Host "Please check: http://${SERVER_IP}:9000" -ForegroundColor White
                }
            }
        }
    } else {
        Write-Host "PSCP not found. Please install PuTTY tools." -ForegroundColor Red
    }
} else {
    Write-Host "PLINK not found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please use one of these methods:" -ForegroundColor Yellow
    Write-Host "1. Install PuTTY from: https://www.putty.org/" -ForegroundColor White
    Write-Host "2. Use WinSCP from: https://winscp.net/" -ForegroundColor White
    Write-Host "3. Manually run: ssh root@$SERVER_IP" -ForegroundColor White
    Write-Host "   Password: $SERVER_PASS" -ForegroundColor Gray
}
