# Restart and Test Script
Write-Host "Stopping application..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*SyncEkpToCasdoor.Web*" } | Stop-Process -Force

Start-Sleep -Seconds 2

Write-Host "Rebuilding..." -ForegroundColor Yellow
cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    Write-Host "Starting application..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web ; dotnet run"
    
    Write-Host "Waiting for application to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 8
    
    Write-Host "Running tests..." -ForegroundColor Yellow
    cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker
    .\test-auto.ps1
} else {
    Write-Host "Build failed!" -ForegroundColor Red
}
