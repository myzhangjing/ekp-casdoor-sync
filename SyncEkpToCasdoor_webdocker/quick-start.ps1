# Quick Start Script - 一键启动并测试

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor Sync - Quick Start" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$scriptPath = $PSScriptRoot
$webPath = Join-Path $scriptPath "SyncEkpToCasdoor.Web"

# Check if application exists
if (-not (Test-Path $webPath)) {
    Write-Host "Error: Cannot find SyncEkpToCasdoor.Web directory" -ForegroundColor Red
    exit 1
}

# Step 1: Start Application
Write-Host "[1] Starting Application..." -ForegroundColor Yellow
Write-Host "    Path: $webPath" -ForegroundColor Cyan

$appProcess = Start-Process powershell -ArgumentList `
    "-NoExit", "-Command", "cd '$webPath' ; Write-Host 'Starting application...' -ForegroundColor Green ; dotnet run" `
    -PassThru

Write-Host "    Application started (PID: $($appProcess.Id))" -ForegroundColor Green
Write-Host "    URL: http://localhost:5233" -ForegroundColor Cyan

# Step 2: Wait for startup
Write-Host "`n[2] Waiting for application to start..." -ForegroundColor Yellow

for ($i = 8; $i -gt 0; $i--) {
    Write-Host "    $i seconds..." -ForegroundColor Gray
    Start-Sleep -Seconds 1
}

Write-Host "    Ready!" -ForegroundColor Green

# Step 3: Test connection
Write-Host "`n[3] Testing connection..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri "http://localhost:5233/login" -UseBasicParsing -TimeoutSec 5
    Write-Host "    Connection successful!" -ForegroundColor Green
} catch {
    Write-Host "    Warning: Cannot connect to application" -ForegroundColor Red
    Write-Host "    Please wait a bit longer and try manually" -ForegroundColor Yellow
}

# Step 4: Run tests
Write-Host "`n[4] Running automated tests..." -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Cyan

$testScript = Join-Path $scriptPath "test-full-integration.ps1"

if (Test-Path $testScript) {
    & powershell -ExecutionPolicy Bypass -File $testScript
} else {
    Write-Host "Warning: Test script not found at $testScript" -ForegroundColor Yellow
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Quick Start Complete" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Browser: http://localhost:5233/login" -ForegroundColor White
Write-Host "2. Username: admin" -ForegroundColor White
Write-Host "3. Password: 123" -ForegroundColor White
Write-Host "`n4. Check test report: test-report-*.txt" -ForegroundColor White
Write-Host "5. View logs: logs\*.log" -ForegroundColor White

Write-Host "`nApplication is running in a separate window" -ForegroundColor Cyan
Write-Host "Close that window to stop the application`n" -ForegroundColor Cyan
