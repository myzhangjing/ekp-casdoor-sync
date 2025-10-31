# Test New Features - Simplified Version
$baseUrl = "http://localhost:5233"
$testCount = 0
$passCount = 0

Write-Host "`n=== Testing New Features ===`n" -ForegroundColor Cyan

function Test-Page {
    param($path, $name)
    $script:testCount++
    Write-Host "[$script:testCount] Testing: $name" -NoNewline
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl$path" -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            Write-Host " - OK" -ForegroundColor Green
            $script:passCount++
            return $true
        }
    } catch {
        Write-Host " - FAIL: $($_.Exception.Message)" -ForegroundColor Red
    }
    return $false
}

# Test all pages
Test-Page "/" "Home Page"
Test-Page "/sync" "Sync Management"
Test-Page "/companies" "Company Sync"
Test-Page "/schedule" "Schedule Tasks (NEW)"
Test-Page "/query" "Data Query (NEW)"
Test-Page "/manage" "Data Management"
Test-Page "/settings" "System Settings"

# Test API
Test-Page "/api/sync/status" "API Status"

Write-Host "`n=== Results ===`n" -ForegroundColor Cyan
Write-Host "Total: $testCount | Pass: $passCount | Fail: $($testCount - $passCount)" -ForegroundColor White

if ($passCount -eq $testCount) {
    Write-Host "`nAll tests passed!`n" -ForegroundColor Green
} else {
    Write-Host "`nSome tests failed!`n" -ForegroundColor Yellow
}
