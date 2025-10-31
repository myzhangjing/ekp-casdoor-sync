# ==========================================
# Automated Testing Script - EKP to Casdoor Sync
# ==========================================

param(
    [string]$BaseUrl = "http://localhost:5233",
    [string]$CasdoorUrl = "http://sso.fzcsps.com"
)

$script:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
    Warnings = 0
}

function Write-TestResult {
    param([string]$Test, [bool]$Pass, [string]$Msg = "", [double]$Time = 0)
    
    $script:TestResults.Total++
    if ($Pass) {
        $script:TestResults.Passed++
        Write-Host "[PASS] $Test ($([math]::Round($Time, 2))ms)" -ForegroundColor Green
        if ($Msg) { Write-Host "       $Msg" -ForegroundColor Cyan }
    } else {
        $script:TestResults.Failed++
        Write-Host "[FAIL] $Test" -ForegroundColor Red
        if ($Msg) { Write-Host "       Error: $Msg" -ForegroundColor Red }
    }
    
    if ($Time -gt 1000) {
        $script:TestResults.Warnings++
        Write-Host "       [WARN] Slow response: $([math]::Round($Time, 2))ms" -ForegroundColor Yellow
    }
}

function Test-Endpoint {
    param([string]$Url, [string]$Method = "GET", [int]$Expected = 200)
    
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -Uri $Url -Method $Method -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        $sw.Stop()
        
        return @{
            Success = ($response.StatusCode -eq $Expected)
            StatusCode = $response.StatusCode
            Time = $sw.ElapsedMilliseconds
        }
    } catch {
        $sw.Stop()
        return @{
            Success = $false
            StatusCode = 0
            Time = $sw.ElapsedMilliseconds
            Error = $_.Exception.Message
        }
    }
}

Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "  Automated Testing Started" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Application Status
Write-Host "`n[1] Application Status" -ForegroundColor Yellow
$result = Test-Endpoint -Url "$BaseUrl/login"
Write-TestResult -Test "Application Running" -Pass $result.Success -Time $result.Time

# Test 2: Page Response Time
Write-Host "`n[2] Page Response Time" -ForegroundColor Yellow
$pages = @(
    @{Name = "Login Page"; Url = "/login"; Expected = 200}
    @{Name = "Home Page"; Url = "/"; Expected = 302}
    @{Name = "Challenge"; Url = "/challenge"; Expected = 302}
    @{Name = "Logout"; Url = "/logout"; Expected = 302}
)

foreach ($page in $pages) {
    $result = Test-Endpoint -Url "$BaseUrl$($page.Url)" -Expected $page.Expected
    Write-TestResult -Test "Page: $($page.Name)" -Pass ($result.StatusCode -eq $page.Expected) -Time $result.Time -Msg "HTTP $($result.StatusCode)"
}

# Test 3: Configuration Check
Write-Host "`n[3] Configuration Check" -ForegroundColor Yellow
$configPath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.Web\appsettings.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    
    Write-TestResult -Test "Config File Exists" -Pass $true -Msg $configPath
    
    $checks = @(
        @{Name = "ClientId"; Value = $config.CasdoorAuth.ClientId; Expected = "cb838421e04ecd30f72b"}
        @{Name = "AllowedOwner"; Value = $config.CasdoorAuth.AllowedOwner; Expected = "built-in"}
    )
    
    foreach ($check in $checks) {
        $pass = ($check.Value -eq $check.Expected)
        Write-TestResult -Test "Config: $($check.Name)" -Pass $pass -Msg "Value: $($check.Value)"
    }
} else {
    Write-TestResult -Test "Config File Exists" -Pass $false -Msg "Not found"
}

# Test 4: Scheduled Task Config
Write-Host "`n[4] Scheduled Task Configuration" -ForegroundColor Yellow
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    
    if ($config.PSObject.Properties['ScheduledSync']) {
        Write-TestResult -Test "Scheduled Sync Configured" -Pass $true `
            -Msg "Enabled: $($config.ScheduledSync.Enabled), Interval: $($config.ScheduledSync.IntervalSeconds)s"
    } else {
        Write-TestResult -Test "Scheduled Sync Configured" -Pass $false -Msg "Not configured"
    }
}

# Test 5: Database Connection
Write-Host "`n[5] Database Connection" -ForegroundColor Yellow
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    $conn = $config.EkpConnection
    
    if ($conn -and $conn -match "Server=([^;,]+)") {
        $server = $matches[1]
        Write-Host "       Testing connection to: $server" -ForegroundColor Cyan
        
        try {
            $parts = $server -split ','
            $host = $parts[0]
            $port = if ($parts.Length -gt 1) { $parts[1] } else { 1433 }
            
            $tcp = New-Object System.Net.Sockets.TcpClient
            $connect = $tcp.BeginConnect($host, $port, $null, $null)
            $wait = $connect.AsyncWaitHandle.WaitOne(3000, $false)
            
            if ($wait) {
                $tcp.EndConnect($connect)
                $tcp.Close()
                Write-TestResult -Test "Database Server Reachable" -Pass $true -Msg $server
            } else {
                Write-TestResult -Test "Database Server Reachable" -Pass $false -Msg "Timeout"
            }
        } catch {
            Write-TestResult -Test "Database Server Reachable" -Pass $false -Msg $_.Exception.Message
        }
    }
}

# Test 6: Performance Benchmark
Write-Host "`n[6] Performance Benchmark (5 iterations)" -ForegroundColor Yellow
$times = @()
for ($i = 1; $i -le 5; $i++) {
    $result = Test-Endpoint -Url "$BaseUrl/login"
    if ($result.Success) {
        $times += $result.Time
    }
}

if ($times.Count -gt 0) {
    $avg = ($times | Measure-Object -Average).Average
    $min = ($times | Measure-Object -Minimum).Minimum
    $max = ($times | Measure-Object -Maximum).Maximum
    
    Write-TestResult -Test "Login Page Average" -Pass ($avg -lt 1000) -Time $avg `
        -Msg "Min: $([math]::Round($min, 2))ms, Max: $([math]::Round($max, 2))ms"
}

# Test 7: Critical Files
Write-Host "`n[7] Critical Files Check" -ForegroundColor Yellow
$files = @(
    "SyncEkpToCasdoor.Web\Program.cs",
    "SyncEkpToCasdoor.Web\Components\Pages\Login.razor",
    "SyncEkpToCasdoor.Web\Controllers\AuthController.cs",
    "SyncEkpToCasdoor.Web\Services\ScheduledSyncService.cs",
    "SyncEkpToCasdoor.Web\Services\ISyncService.cs",
    "SyncEkpToCasdoor.Web\Services\SyncService.cs"
)

foreach ($file in $files) {
    $path = Join-Path $PSScriptRoot $file
    $exists = Test-Path $path
    Write-TestResult -Test "File: $(Split-Path $file -Leaf)" -Pass $exists
}

# Test 8: Security Check
Write-Host "`n[8] Security Check" -ForegroundColor Yellow
$result = Test-Endpoint -Url "$BaseUrl/" -Expected 302
Write-TestResult -Test "Home Page Access Control" -Pass ($result.StatusCode -eq 302) `
    -Msg "Unauthenticated users redirected"

# Summary
Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nTotal Tests: $($script:TestResults.Total)" -ForegroundColor White
Write-Host "Passed: $($script:TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($script:TestResults.Failed)" -ForegroundColor Red
Write-Host "Warnings: $($script:TestResults.Warnings)" -ForegroundColor Yellow

$rate = [math]::Round(($script:TestResults.Passed / $script:TestResults.Total) * 100, 2)
$color = if ($rate -ge 80) { "Green" } elseif ($rate -ge 60) { "Yellow" } else { "Red" }
Write-Host "`nSuccess Rate: $rate%" -ForegroundColor $color

# Recommendations
Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "  Recommendations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($script:TestResults.Warnings -gt 0) {
    Write-Host "• $($script:TestResults.Warnings) slow responses detected" -ForegroundColor Yellow
    Write-Host "  Suggestions:" -ForegroundColor White
    Write-Host "  1. Enable response caching" -ForegroundColor White
    Write-Host "  2. Optimize database queries" -ForegroundColor White
    Write-Host "  3. Use CDN for static assets" -ForegroundColor White
} else {
    Write-Host "• All response times are acceptable" -ForegroundColor Green
}

Write-Host "`nTest completed!" -ForegroundColor Cyan

# Save report
$reportPath = "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
@"
Automated Test Report
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
========================================

Statistics:
- Total: $($script:TestResults.Total)
- Passed: $($script:TestResults.Passed)
- Failed: $($script:TestResults.Failed)
- Warnings: $($script:TestResults.Warnings)
- Success Rate: $rate%

Environment:
- Application URL: $BaseUrl
- Casdoor URL: $CasdoorUrl
"@ | Out-File $reportPath -Encoding UTF8

Write-Host "`nReport saved: $reportPath" -ForegroundColor Cyan
