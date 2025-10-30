param(
    [string]$CasdoorUrl = 'https://sso.fzcsps.com',
    [string]$AdminUser = 'admin',
    [string]$AdminPass = '123',
    [switch]$SkipPlaywright
)

Set-Location -Path $PSScriptRoot
Write-Host "Working directory:" (Get-Location)

if (-not $SkipPlaywright) {
    Write-Host "Running Playwright script with admin: $AdminUser (target: $CasdoorUrl)"
    # set env vars for create_app_users.js
    $env:CASDOOR_URL = $CasdoorUrl
    $env:ADMIN_USER = $AdminUser
    $env:ADMIN_PASS = $AdminPass

    # install node deps if missing
    if (-not (Test-Path node_modules)) {
        Write-Host "Installing Node dependencies (npm install) ..."
        npm install
    }
    Write-Host "Ensuring Playwright browsers installed (npx playwright install) ..."
    npx playwright install

    Write-Host "Launching Playwright automation (visible browser) ..."
    node create_app_users.js
    Write-Host "Playwright run completed. Check screenshots and trace.zip in this directory."
}
else { Write-Host "Skipping Playwright automation as requested." }

# trigger full sync by running run-sync.ps1 from repo root
Set-Location -Path (Resolve-Path .. | Select-Object -First 1)
Write-Host "Changed to repo root:" (Get-Location)

if (-not (Test-Path .\run-sync.ps1)) {
    Write-Host "run-sync.ps1 not found in repo root; please verify and run manually."
    exit 1
}

Write-Host "Triggering full sync via run-sync.ps1 (relies on CASDOOR_* env vars)."
& .\run-sync.ps1

Write-Host "Full sync triggered. Logs are placed under logs\ (use scripts\parse_latest_log.ps1 to view)."
