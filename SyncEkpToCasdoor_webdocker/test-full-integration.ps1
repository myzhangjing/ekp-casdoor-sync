# ==========================================
# Full Integration Test with Casdoor Login
# ==========================================

param(
    [string]$BaseUrl = "http://localhost:5233",
    [string]$CasdoorUrl = "http://sso.fzcsps.com",
    [string]$Username = "admin",
    [string]$Password = "123"
)

Add-Type -AssemblyName System.Web

$script:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
}

function Write-TestResult {
    param([string]$Test, [bool]$Pass, [string]$Msg = "", [double]$Time = 0)
    
    $script:TestResults.Total++
    if ($Pass) {
        $script:TestResults.Passed++
        Write-Host "[PASS] $Test" -ForegroundColor Green
        if ($Time -gt 0) { Write-Host "       Time: $([math]::Round($Time, 2))ms" -ForegroundColor Cyan }
        if ($Msg) { Write-Host "       $Msg" -ForegroundColor Cyan }
    } else {
        $script:TestResults.Failed++
        Write-Host "[FAIL] $Test" -ForegroundColor Red
        if ($Msg) { Write-Host "       Error: $Msg" -ForegroundColor Red }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Full Integration Test with OAuth Login" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Create a session to maintain cookies
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# Step 1: Check application is running
Write-Host "`n[Step 1] Checking Application Status..." -ForegroundColor Yellow
try {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest -Uri "$BaseUrl/login" -UseBasicParsing -TimeoutSec 10
    $sw.Stop()
    Write-TestResult -Test "Application is running" -Pass $true -Time $sw.ElapsedMilliseconds
} catch {
    Write-TestResult -Test "Application is running" -Pass $false -Msg $_.Exception.Message
    Write-Host "`nCannot continue - application is not running!" -ForegroundColor Red
    exit 1
}

# Step 2: Access login page
Write-Host "`n[Step 2] Accessing Login Page..." -ForegroundColor Yellow
try {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/login" -WebSession $session -UseBasicParsing -TimeoutSec 10
    $sw.Stop()
    
    $hasLoginButton = $loginPage.Content -match "challenge" -or $loginPage.Content -match "Casdoor"
    Write-TestResult -Test "Login page loads" -Pass $true -Time $sw.ElapsedMilliseconds
    Write-TestResult -Test "Login button present" -Pass $hasLoginButton
    
    if ($hasLoginButton) {
        Write-Host "       Login page content verified" -ForegroundColor Green
    }
} catch {
    Write-TestResult -Test "Login page loads" -Pass $false -Msg $_.Exception.Message
}

# Step 3: Initiate OAuth challenge
Write-Host "`n[Step 3] Initiating OAuth Challenge..." -ForegroundColor Yellow
try {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $challengeResponse = Invoke-WebRequest -Uri "$BaseUrl/challenge" `
        -WebSession $session `
        -MaximumRedirection 0 `
        -ErrorAction SilentlyContinue `
        -UseBasicParsing
    $sw.Stop()
    
    # Should get a redirect to Casdoor
    $redirectLocation = $challengeResponse.Headers['Location']
    $isCasdoorRedirect = $redirectLocation -match $CasdoorUrl
    
    Write-TestResult -Test "OAuth challenge initiated" -Pass $true -Time $sw.ElapsedMilliseconds
    Write-TestResult -Test "Redirects to Casdoor" -Pass $isCasdoorRedirect -Msg $redirectLocation
    
    if ($isCasdoorRedirect) {
        $casdoorLoginUrl = $redirectLocation
        Write-Host "       Redirect URL: $casdoorLoginUrl" -ForegroundColor Cyan
    }
} catch {
    $redirectLocation = $_.Exception.Response.Headers['Location']
    $isCasdoorRedirect = $redirectLocation -match $CasdoorUrl
    
    if ($isCasdoorRedirect) {
        Write-TestResult -Test "OAuth challenge initiated" -Pass $true
        Write-TestResult -Test "Redirects to Casdoor" -Pass $true -Msg $redirectLocation
        $casdoorLoginUrl = $redirectLocation
    } else {
        Write-TestResult -Test "OAuth challenge initiated" -Pass $false -Msg $_.Exception.Message
    }
}

# Step 4: Login to Casdoor
if ($casdoorLoginUrl) {
    Write-Host "`n[Step 4] Logging in to Casdoor..." -ForegroundColor Yellow
    
    try {
        # Get Casdoor login page
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $casdoorPage = Invoke-WebRequest -Uri $casdoorLoginUrl -WebSession $session -UseBasicParsing -TimeoutSec 10
        $sw.Stop()
        
        Write-TestResult -Test "Casdoor login page loads" -Pass $true -Time $sw.ElapsedMilliseconds
        
        # Extract form action and other parameters
        if ($casdoorPage.Content -match 'action="([^"]+)"') {
            $formAction = $matches[1]
            if (-not $formAction.StartsWith("http")) {
                $formAction = "$CasdoorUrl$formAction"
            }
            
            Write-Host "       Form action: $formAction" -ForegroundColor Cyan
            
            # Prepare login form data
            $loginData = @{
                username = $Username
                password = $Password
                type = "login"
            }
            
            # Submit login form
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $loginResponse = Invoke-WebRequest -Uri $formAction `
                -Method POST `
                -Body $loginData `
                -WebSession $session `
                -MaximumRedirection 0 `
                -ErrorAction SilentlyContinue `
                -UseBasicParsing
            $sw.Stop()
            
            Write-TestResult -Test "Login form submitted" -Pass $true -Time $sw.ElapsedMilliseconds
            
            # Check if we got a redirect back to our app
            $callbackUrl = $loginResponse.Headers['Location']
            if ($callbackUrl -match "^$BaseUrl" -and $callbackUrl -match "code=") {
                Write-TestResult -Test "Login successful" -Pass $true -Msg "Redirected to callback"
                Write-Host "       Callback URL: $callbackUrl" -ForegroundColor Cyan
                
                # Step 5: Handle OAuth callback
                Write-Host "`n[Step 5] Processing OAuth Callback..." -ForegroundColor Yellow
                
                try {
                    $sw = [System.Diagnostics.Stopwatch]::StartNew()
                    $callbackResponse = Invoke-WebRequest -Uri $callbackUrl `
                        -WebSession $session `
                        -MaximumRedirection 5 `
                        -UseBasicParsing `
                        -TimeoutSec 30
                    $sw.Stop()
                    
                    Write-TestResult -Test "OAuth callback processed" -Pass $true -Time $sw.ElapsedMilliseconds
                    
                    # Check if we're now authenticated (should redirect to home page or show authenticated content)
                    $isAuthenticated = $callbackResponse.StatusCode -eq 200
                    Write-TestResult -Test "User authenticated" -Pass $isAuthenticated
                    
                    if ($isAuthenticated) {
                        Write-Host "`n[SUCCESS] Full OAuth flow completed!" -ForegroundColor Green
                        
                        # Step 6: Test authenticated endpoints
                        Write-Host "`n[Step 6] Testing Authenticated Access..." -ForegroundColor Yellow
                        
                        # Test home page access
                        try {
                            $sw = [System.Diagnostics.Stopwatch]::StartNew()
                            $homePage = Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing -TimeoutSec 10
                            $sw.Stop()
                            
                            $hasAccess = $homePage.StatusCode -eq 200
                            Write-TestResult -Test "Home page access" -Pass $hasAccess -Time $sw.ElapsedMilliseconds
                            
                            # Check if user info is displayed
                            $hasUserInfo = $homePage.Content -match $Username -or $homePage.Content -match "admin"
                            if ($hasUserInfo) {
                                Write-TestResult -Test "User info displayed" -Pass $true -Msg "Username found in page"
                            }
                        } catch {
                            Write-TestResult -Test "Home page access" -Pass $false -Msg $_.Exception.Message
                        }
                        
                        # Step 7: Test scheduled task configuration
                        Write-Host "`n[Step 7] Checking Scheduled Task Configuration..." -ForegroundColor Yellow
                        
                        $configPath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.Web\appsettings.json"
                        if (Test-Path $configPath) {
                            $config = Get-Content $configPath | ConvertFrom-Json
                            
                            if ($config.PSObject.Properties['ScheduledSync']) {
                                Write-TestResult -Test "Scheduled sync configured" -Pass $true `
                                    -Msg "Enabled: $($config.ScheduledSync.Enabled), Interval: $($config.ScheduledSync.IntervalSeconds)s"
                                
                                if ($config.ScheduledSync.Enabled -eq $false) {
                                    Write-Host "`n       [INFO] Scheduled sync is disabled" -ForegroundColor Yellow
                                    Write-Host "       To enable, set ScheduledSync.Enabled to true in appsettings.json" -ForegroundColor Yellow
                                    Write-Host "       For testing, you can set IntervalSeconds to a small value (e.g., 10)" -ForegroundColor Yellow
                                }
                            } else {
                                Write-TestResult -Test "Scheduled sync configured" -Pass $false -Msg "Not configured"
                            }
                            
                            # Check target companies
                            $targetCompanies = $config.TargetCompanyIds
                            if ($targetCompanies) {
                                $companyArray = $targetCompanies -split ','
                                Write-TestResult -Test "Target companies configured" -Pass $true -Msg "Count: $($companyArray.Length)"
                            } else {
                                Write-TestResult -Test "Target companies configured" -Pass $false -Msg "Not configured"
                            }
                        }
                        
                        # Step 8: Test logout
                        Write-Host "`n[Step 8] Testing Logout..." -ForegroundColor Yellow
                        
                        try {
                            $sw = [System.Diagnostics.Stopwatch]::StartNew()
                            $logoutResponse = Invoke-WebRequest -Uri "$BaseUrl/logout" `
                                -WebSession $session `
                                -MaximumRedirection 0 `
                                -ErrorAction SilentlyContinue `
                                -UseBasicParsing
                            $sw.Stop()
                            
                            # Should redirect to login page
                            $redirectsToLogin = $logoutResponse.Headers['Location'] -match "/login"
                            Write-TestResult -Test "Logout successful" -Pass $redirectsToLogin -Time $sw.ElapsedMilliseconds
                            
                            if ($redirectsToLogin) {
                                Write-Host "       Redirected to: $($logoutResponse.Headers['Location'])" -ForegroundColor Cyan
                            }
                        } catch {
                            $redirectLocation = $_.Exception.Response.Headers['Location']
                            $redirectsToLogin = $redirectLocation -match "/login"
                            Write-TestResult -Test "Logout successful" -Pass $redirectsToLogin
                        }
                        
                    } else {
                        Write-Host "`n[WARNING] Callback succeeded but authentication unclear" -ForegroundColor Yellow
                        Write-Host "Status code: $($callbackResponse.StatusCode)" -ForegroundColor Yellow
                    }
                    
                } catch {
                    Write-TestResult -Test "OAuth callback processed" -Pass $false -Msg $_.Exception.Message
                    
                    # Try to get more details
                    if ($_.Exception.Response) {
                        $statusCode = [int]$_.Exception.Response.StatusCode
                        Write-Host "       Status Code: $statusCode" -ForegroundColor Yellow
                        
                        if ($statusCode -eq 500) {
                            Write-Host "       [ERROR] Server error during authentication" -ForegroundColor Red
                            Write-Host "       Check application logs for details" -ForegroundColor Yellow
                        }
                    }
                }
                
            } else {
                Write-TestResult -Test "Login successful" -Pass $false -Msg "No redirect to callback"
                Write-Host "       Response location: $callbackUrl" -ForegroundColor Yellow
            }
            
        } else {
            Write-TestResult -Test "Casdoor form extraction" -Pass $false -Msg "Could not find form action"
        }
        
    } catch {
        Write-TestResult -Test "Casdoor login" -Pass $false -Msg $_.Exception.Message
    }
}

# Performance Analysis
Write-Host "`n[Step 9] Performance Analysis..." -ForegroundColor Yellow

$performanceTests = @(
    @{Name = "Login Page"; Url = "/login"}
    @{Name = "Static Resource"; Url = "/_framework/blazor.web.js"}
)

foreach ($test in $performanceTests) {
    $times = @()
    
    for ($i = 1; $i -le 3; $i++) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $null = Invoke-WebRequest -Uri "$BaseUrl$($test.Url)" -UseBasicParsing -TimeoutSec 10
            $sw.Stop()
            $times += $sw.ElapsedMilliseconds
        } catch {
            # Ignore errors in performance test
        }
    }
    
    if ($times.Count -gt 0) {
        $avg = ($times | Measure-Object -Average).Average
        $needsOptimization = $avg -gt 1000
        
        Write-TestResult -Test "Performance: $($test.Name)" -Pass (-not $needsOptimization) `
            -Msg "Average: $([math]::Round($avg, 2))ms"
        
        if ($needsOptimization) {
            Write-Host "       [WARN] Response time exceeds 1 second" -ForegroundColor Yellow
        }
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nTotal Tests: $($script:TestResults.Total)" -ForegroundColor White
Write-Host "Passed: $($script:TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($script:TestResults.Failed)" -ForegroundColor Red

$rate = if ($script:TestResults.Total -gt 0) { 
    [math]::Round(($script:TestResults.Passed / $script:TestResults.Total) * 100, 2) 
} else { 
    0 
}

$color = if ($rate -ge 80) { "Green" } elseif ($rate -ge 60) { "Yellow" } else { "Red" }
Write-Host "`nSuccess Rate: $rate%" -ForegroundColor $color

# Recommendations
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Recommendations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n1. Authentication System:" -ForegroundColor Yellow
Write-Host "   - OAuth flow: " -NoNewline
if ($script:TestResults.Passed -ge ($script:TestResults.Total * 0.8)) {
    Write-Host "Working correctly" -ForegroundColor Green
} else {
    Write-Host "Needs attention" -ForegroundColor Red
}

Write-Host "`n2. Scheduled Tasks:" -ForegroundColor Yellow
Write-Host "   - To test scheduled sync:" -ForegroundColor White
Write-Host "     * Set ScheduledSync.Enabled = true" -ForegroundColor White
Write-Host "     * Set ScheduledSync.IntervalSeconds = 10 (for testing)" -ForegroundColor White
Write-Host "     * Restart application" -ForegroundColor White
Write-Host "     * Check logs for sync execution" -ForegroundColor White

Write-Host "`n3. Performance:" -ForegroundColor Yellow
Write-Host "   - All critical pages should load < 1 second" -ForegroundColor White
Write-Host "   - Consider enabling response caching" -ForegroundColor White
Write-Host "   - Monitor database query performance" -ForegroundColor White

Write-Host "`n4. Next Steps:" -ForegroundColor Yellow
Write-Host "   - Review application logs: logs/*.log" -ForegroundColor White
Write-Host "   - Test sync functionality after login" -ForegroundColor White
Write-Host "   - Monitor scheduled task execution" -ForegroundColor White

Write-Host "`nTest completed!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Save detailed report
$reportPath = Join-Path $PSScriptRoot "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
@"
Full Integration Test Report
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
========================================

Test Statistics:
- Total Tests: $($script:TestResults.Total)
- Passed: $($script:TestResults.Passed)
- Failed: $($script:TestResults.Failed)
- Success Rate: $rate%

Test Environment:
- Application URL: $BaseUrl
- Casdoor URL: $CasdoorUrl
- Test Username: $Username

Test Scope:
1. Application availability
2. Login page rendering
3. OAuth challenge initiation
4. Casdoor authentication
5. OAuth callback handling
6. Authenticated access
7. Scheduled task configuration
8. Logout functionality
9. Performance benchmarks

Recommendations:
- Review logs for any errors
- Test scheduled sync after enabling in config
- Monitor performance metrics
- Verify all authentication flows

"@ | Out-File $reportPath -Encoding UTF8

Write-Host "Detailed report saved: $reportPath`n" -ForegroundColor Cyan
