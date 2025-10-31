# Test OAuth Authentication
Write-Host "`n=== OAuth Authentication Test ===`n" -ForegroundColor Cyan

$baseUrl = "http://syn-ekp.fzcsps.com:9000"

# Test 1: Access home page (should redirect to login)
Write-Host "[1] Testing home page access..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 302) {
        Write-Host "  [OK] Redirects to login (302)" -ForegroundColor Green
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        Write-Host "  [OK] Redirects to login (302)" -ForegroundColor Green
    } else {
        Write-Host "  [INFO] Response: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

# Test 2: Access login page
Write-Host "`n[2] Testing login page..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/login" -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Login page accessible (200)" -ForegroundColor Green
        if ($response.Content -match "Casdoor") {
            Write-Host "  [OK] Contains Casdoor reference" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "  [FAIL] Cannot access login page" -ForegroundColor Red
}

# Test 3: Check OAuth challenge endpoint
Write-Host "`n[3] Testing OAuth challenge endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/challenge" -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        $location = $_.Exception.Response.Headers.Location
        Write-Host "  [OK] Redirects to Casdoor (302)" -ForegroundColor Green
        Write-Host "  Location: $location" -ForegroundColor Gray
        if ($location -match "sso.fzcsps.com") {
            Write-Host "  [OK] Correct authorization server" -ForegroundColor Green
        }
    }
}

# Test 4: Check access denied page
Write-Host "`n[4] Testing access denied page..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/access-denied" -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Access denied page accessible (200)" -ForegroundColor Green
        if ($response.Content -match "built-in") {
            Write-Host "  [OK] Contains owner restriction message" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "  [FAIL] Cannot access denied page" -ForegroundColor Red
}

Write-Host "`n=== Configuration ===`n" -ForegroundColor Cyan
Write-Host "App URL: $baseUrl" -ForegroundColor White
Write-Host "SSO URL: http://sso.fzcsps.com" -ForegroundColor White
Write-Host "Client ID: aecd00a352e5c560ffe6" -ForegroundColor White
Write-Host "Redirect URI: $baseUrl/callback" -ForegroundColor White
Write-Host "Allowed Owner: built-in" -ForegroundColor White

Write-Host "`n=== Next Steps ===`n" -ForegroundColor Cyan
Write-Host "1. Open browser to: $baseUrl" -ForegroundColor Yellow
Write-Host "2. Click login button" -ForegroundColor Yellow
Write-Host "3. Login with built-in user" -ForegroundColor Yellow
Write-Host "4. Verify access granted`n" -ForegroundColor Yellow
