# Auto Test Script - EKP Casdoor Sync
$baseUrl = "http://localhost:5233"
$apiUrl = "$baseUrl/api/sync"

Write-Host "====================================="
Write-Host "  Auto Test - EKP Casdoor Sync"
Write-Host "====================================="
Write-Host ""

$testsPassed = 0
$testsFailed = 0
$testsTotal = 7

function Test-Step {
    param([string]$name, [int]$num, [scriptblock]$test)
    Write-Host ""
    Write-Host "[$num/$testsTotal] $name" -ForegroundColor Yellow
    Write-Host "-------------------------------------"
    try {
        & $test
    } catch {
        Write-Host "  X Failed: $($_.Exception.Message)" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Test 1: Check app running
Test-Step "Check Application" 1 {
    $response = Invoke-WebRequest -Uri "$baseUrl/sync" -TimeoutSec 5 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "  OK: App is running" -ForegroundColor Green
        $script:testsPassed++
    }
}

# Test 2: Test connections
Test-Step "Test Connections" 2 {
    Write-Host "  -> Calling API: GET $apiUrl/test-connections" -ForegroundColor Cyan
    $response = Invoke-RestMethod -Uri "$apiUrl/test-connections" -Method Get -TimeoutSec 30
    
    if ($response.success) {
        Write-Host "  OK: Connections tested successfully" -ForegroundColor Green
        Write-Host "  -> EKP Database: OK" -ForegroundColor Gray
        Write-Host "  -> Casdoor API: OK" -ForegroundColor Gray
        $script:testsPassed++
    } else {
        Write-Host "  X Connection test failed: $($response.message)" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Test 3: Preview sync
Test-Step "Preview Sync" 3 {
    Write-Host "  -> Calling API: GET $apiUrl/preview" -ForegroundColor Cyan
    $response = Invoke-RestMethod -Uri "$apiUrl/preview" -Method Get -TimeoutSec 60
    
    if ($response.success) {
        Write-Host "  OK: Preview completed" -ForegroundColor Green
        if ($response.details) {
            foreach ($detail in $response.details) {
                Write-Host "    - $detail" -ForegroundColor Gray
            }
        }
        $script:testsPassed++
    } else {
        Write-Host "  X Preview failed: $($response.message)" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Test 4: Check status
Test-Step "Check Status" 4 {
    Write-Host "  -> Calling API: GET $apiUrl/status" -ForegroundColor Cyan
    $status = Invoke-RestMethod -Uri "$apiUrl/status" -Method Get -TimeoutSec 10
    
    Write-Host "  -> Current status: $(if($status.IsRunning){'Running'}else{'Idle'})" -ForegroundColor Cyan
    if ($status.LastSyncTime) {
        Write-Host "  -> Last sync: $($status.LastSyncTime)" -ForegroundColor Gray
    }
    
    if (-not $status.IsRunning) {
        Write-Host "  OK: Status is idle" -ForegroundColor Green
        $script:testsPassed++
    } else {
        Write-Host "  X Sync is already running" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Test 5: Full sync
Test-Step "Full Sync" 5 {
    Write-Host "  -> Calling API: POST $apiUrl/full-sync" -ForegroundColor Cyan
    Write-Host "  -> This may take several minutes..." -ForegroundColor Yellow
    
    $job = Start-Job -ScriptBlock {
        param($url)
        Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 600
    } -ArgumentList "$apiUrl/full-sync"
    
    $startTime = Get-Date
    $maxWaitSeconds = 300
    $checkInterval = 5
    $lastLogCount = 0
    
    Write-Host "  -> Monitoring progress..." -ForegroundColor Cyan
    while ($job.State -eq 'Running') {
        $elapsed = ((Get-Date) - $startTime).TotalSeconds
        
        if ($elapsed -gt $maxWaitSeconds) {
            Write-Host "  X Sync timeout (> $maxWaitSeconds seconds)" -ForegroundColor Red
            Stop-Job -Job $job
            Remove-Job -Job $job
            $script:testsFailed++
            break
        }
        
        try {
            $logs = Invoke-RestMethod -Uri "$apiUrl/logs?count=10" -Method Get -TimeoutSec 5
            if ($logs.Count -gt $lastLogCount) {
                foreach ($log in $logs[0..([Math]::Min(4, $logs.Count-1))]) {
                    $level = $log.Level
                    $color = switch ($level) {
                        "Error" { "Red" }
                        "Warning" { "Yellow" }
                        default { "Cyan" }
                    }
                    Write-Host "    [$($log.Timestamp.ToString('HH:mm:ss'))] $($log.Message)" -ForegroundColor $color
                }
                $lastLogCount = $logs.Count
            }
        } catch {
            # Ignore log fetch errors
        }
        
        Write-Host "`r  -> Running: $([int]$elapsed) seconds..." -NoNewline
        Start-Sleep -Seconds $checkInterval
    }
    Write-Host ""
    
    if ($job.State -eq 'Completed') {
        $response = Receive-Job -Job $job
        Remove-Job -Job $job
        
        if ($response.success) {
            Write-Host "  OK: Full sync completed" -ForegroundColor Green
            if ($response.details) {
                foreach ($detail in $response.details) {
                    Write-Host "    - $detail" -ForegroundColor Gray
                }
            }
            $script:testsPassed++
        } else {
            Write-Host "  X Full sync failed: $($response.message)" -ForegroundColor Red
            $script:testsFailed++
        }
    }
}

# Test 6: Verify status recovery
Test-Step "Verify Status Recovery" 6 {
    Write-Host "  -> Waiting for status update..." -ForegroundColor Cyan
    Start-Sleep -Seconds 3
    
    $status = Invoke-RestMethod -Uri "$apiUrl/status" -Method Get -TimeoutSec 10
    
    Write-Host "  -> Current status: $(if($status.IsRunning){'Running'}else{'Idle'})" -ForegroundColor Cyan
    if ($status.LastSyncTime) {
        Write-Host "  -> Last sync: $($status.LastSyncTime)" -ForegroundColor Gray
    }
    
    if (-not $status.IsRunning) {
        Write-Host "  OK: Status recovered to idle" -ForegroundColor Green
        $script:testsPassed++
    } else {
        Write-Host "  X Status still running" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Test 7: Concurrent protection
Test-Step "Test Concurrent Protection" 7 {
    Write-Host "  -> Starting first sync..." -ForegroundColor Cyan
    $job1 = Start-Job -ScriptBlock {
        param($url)
        try {
            Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 60
        } catch {
            @{ success = $false; message = $_.Exception.Message }
        }
    } -ArgumentList "$apiUrl/full-sync"
    
    Start-Sleep -Seconds 2
    
    Write-Host "  -> Attempting second sync (should be rejected)..." -ForegroundColor Cyan
    $job2 = Start-Job -ScriptBlock {
        param($url)
        try {
            Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 10
        } catch {
            @{ success = $false; message = $_.Exception.Message }
        }
    } -ArgumentList "$apiUrl/full-sync"
    
    Wait-Job -Job $job2 -Timeout 15 | Out-Null
    $result2 = Receive-Job -Job $job2
    Remove-Job -Job $job2
    
    Stop-Job -Job $job1
    Remove-Job -Job $job1
    
    if ($result2.success -eq $false) {
        Write-Host "  OK: Concurrent protection works" -ForegroundColor Green
        Write-Host "  -> Rejection reason: $($result2.message)" -ForegroundColor Gray
        $script:testsPassed++
    } else {
        Write-Host "  X Concurrent protection failed" -ForegroundColor Red
        $script:testsFailed++
    }
}

# Summary
Write-Host ""
Write-Host "====================================="
Write-Host "  Test Summary"
Write-Host "====================================="
Write-Host ""
Write-Host "  Total: $testsTotal tests"
Write-Host "  Passed: $testsPassed" -ForegroundColor Green
Write-Host "  Failed: $testsFailed" -ForegroundColor Red
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "OK: All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "X: Some tests failed" -ForegroundColor Red
    exit 1
}
