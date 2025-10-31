# EKP-Casdoor Sync Status Checker
# UTF-8 with BOM encoding

$ErrorActionPreference = "Stop"

# Directory paths
$scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot   = Split-Path -Parent $scriptsDir
$logDir     = Join-Path $repoRoot "logs"
$stateFile  = Join-Path $repoRoot "SyncEkpToCasdoor\sync_state.json"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor Sync Status Checker" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check sync state file
Write-Host "[1] Checking sync state file" -ForegroundColor Yellow
if (Test-Path $stateFile) {
    Write-Host "  State file exists: $stateFile" -ForegroundColor Green
    
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        
        $lastRun = if ($state.LastRunUtc) { 
            [DateTime]::Parse($state.LastRunUtc).ToLocalTime() 
        } else { 
            $null 
        }
        
        $lastGroupSync = if ($state.LastGroupSyncUtc) { 
            [DateTime]::Parse($state.LastGroupSyncUtc).ToLocalTime() 
        } else { 
            $null 
        }
        
        $lastUserSync = if ($state.LastUserSyncUtc) { 
            [DateTime]::Parse($state.LastUserSyncUtc).ToLocalTime() 
        } else { 
            $null 
        }
        
        Write-Host ""
        Write-Host "  Last Run: " -NoNewline
        if ($lastRun) {
            $timeSince = (Get-Date) - $lastRun
            Write-Host "$($lastRun.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White -NoNewline
            Write-Host " ($([Math]::Floor($timeSince.TotalHours)) hours $([Math]::Floor($timeSince.TotalMinutes % 60)) minutes ago)" -ForegroundColor Yellow
            
            if ($timeSince.TotalHours -gt 24) {
                Write-Host "  WARNING: Last sync was over 24 hours ago!" -ForegroundColor Red
            }
        } else {
            Write-Host "Never executed" -ForegroundColor Gray
        }
        
        Write-Host "  Group Sync: " -NoNewline
        if ($lastGroupSync) {
            Write-Host "$($lastGroupSync.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
        } else {
            Write-Host "Never executed" -ForegroundColor Gray
        }
        
        Write-Host "  User Sync: " -NoNewline
        if ($lastUserSync) {
            Write-Host "$($lastUserSync.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
        } else {
            Write-Host "Never executed" -ForegroundColor Gray
        }
        
    } catch {
        Write-Host "  Failed to read state file: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  State file does not exist (may never executed)" -ForegroundColor Red
}

Write-Host ""

# Check log files
Write-Host "[2] Checking sync logs" -ForegroundColor Yellow
if (Test-Path $logDir) {
    $logFiles = Get-ChildItem -Path $logDir -Filter "sync_*.log" -File | 
                Sort-Object LastWriteTime -Descending
    
    if ($logFiles.Count -gt 0) {
        $latestLog = $logFiles[0]
        Write-Host "  Found $($logFiles.Count) log files" -ForegroundColor Green
        Write-Host "  Latest log: $($latestLog.Name)" -ForegroundColor White
        Write-Host "  Created: $($latestLog.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
        Write-Host "  Size: $([Math]::Round($latestLog.Length / 1KB, 2)) KB" -ForegroundColor White
        
        Write-Host ""
        Write-Host "  Last 20 lines:" -ForegroundColor Cyan
        Write-Host "  " + ("-" * 60) -ForegroundColor Gray
        
        $lastLines = Get-Content $latestLog.FullName -Tail 20 -Encoding UTF8 -ErrorAction SilentlyContinue
        foreach ($line in $lastLines) {
            if ($line -match "失败|错误|error|failed|exception") {
                Write-Host "  $line" -ForegroundColor Red
            } elseif ($line -match "成功|完成|success|finished|completed") {
                Write-Host "  $line" -ForegroundColor Green
            } elseif ($line -match "警告|warning") {
                Write-Host "  $line" -ForegroundColor Yellow
            } else {
                Write-Host "  $line" -ForegroundColor Gray
            }
        }
        Write-Host "  " + ("-" * 60) -ForegroundColor Gray
        
    } else {
        Write-Host "  No log files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Log directory does not exist: $logDir" -ForegroundColor Red
}

Write-Host ""

# Check Windows Scheduled Task
Write-Host "[3] Checking Windows Scheduled Task" -ForegroundColor Yellow
try {
    $tasks = Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { 
        $_.TaskName -like "*EKP*" -or 
        $_.TaskName -like "*Casdoor*" -or
        $_.TaskName -like "*Sync*"
    }
    
    if ($tasks -and $tasks.Count -gt 0) {
        Write-Host "  Found $($tasks.Count) related task(s):" -ForegroundColor Green
        
        foreach ($task in $tasks) {
            Write-Host ""
            Write-Host "  Task: $($task.TaskName)" -ForegroundColor White
            Write-Host "    State: $($task.State)" -ForegroundColor $(
                if ($task.State -eq "Ready") { "Green" } 
                elseif ($task.State -eq "Running") { "Cyan" } 
                elseif ($task.State -eq "Disabled") { "Red" } 
                else { "Yellow" }
            )
            
            $taskInfo = Get-ScheduledTaskInfo -TaskName $task.TaskName -ErrorAction SilentlyContinue
            if ($taskInfo) {
                Write-Host "    Last Run: " -NoNewline
                if ($taskInfo.LastRunTime -and $taskInfo.LastRunTime.Year -gt 1) {
                    Write-Host "$($taskInfo.LastRunTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
                } else {
                    Write-Host "Never run" -ForegroundColor Gray
                }
                
                Write-Host "    Next Run: " -NoNewline
                if ($taskInfo.NextRunTime -and $taskInfo.NextRunTime.Year -gt 1) {
                    Write-Host "$($taskInfo.NextRunTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
                } else {
                    Write-Host "Not scheduled" -ForegroundColor Gray
                }
                
                Write-Host "    Last Result: " -NoNewline
                if ($taskInfo.LastTaskResult -eq 0) {
                    Write-Host "Success (0x0)" -ForegroundColor Green
                } else {
                    Write-Host "Failed (0x$($taskInfo.LastTaskResult.ToString('X')))" -ForegroundColor Red
                }
            }
        }
    } else {
        Write-Host "  No related scheduled tasks found" -ForegroundColor Yellow
        Write-Host "  Tip: Create a Windows Scheduled Task for automatic sync" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Failed to check scheduled tasks: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Check environment variables
Write-Host "[4] Checking environment variables" -ForegroundColor Yellow
$requiredVars = @(
    "EKP_SQLSERVER_CONN",
    "CASDOOR_ENDPOINT",
    "CASDOOR_CLIENT_ID",
    "CASDOOR_CLIENT_SECRET",
    "CASDOOR_DEFAULT_OWNER"
)

$allConfigured = $true
foreach ($varName in $requiredVars) {
    $value = [Environment]::GetEnvironmentVariable($varName, "Machine")
    Write-Host "  $varName`: " -NoNewline
    
    if ([string]::IsNullOrWhiteSpace($value)) {
        Write-Host "Not configured" -ForegroundColor Red
        $allConfigured = $false
    } else {
        if ($varName -match "PASSWORD|SECRET|CONN") {
            Write-Host "Configured (******)" -ForegroundColor Green
        } else {
            $displayValue = if ($value.Length -gt 50) { $value.Substring(0, 50) + "..." } else { $value }
            Write-Host "Configured ($displayValue)" -ForegroundColor Green
        }
    }
}

if (-not $allConfigured) {
    Write-Host ""
    Write-Host "  WARNING: Some required environment variables are not configured" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
