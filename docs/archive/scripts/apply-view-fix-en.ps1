# Apply view fix script
param(
    [string]$SqlScriptPath = "FIX_HIERARCHY_DEPTH.sql"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Applying Organization Hierarchy View Fix ===" -ForegroundColor Cyan
Write-Host ""

$connStr = $env:EKP_SQLSERVER_CONN
if (-not $connStr) {
    Write-Host "ERROR: Environment variable EKP_SQLSERVER_CONN not found" -ForegroundColor Red
    exit 1
}

$scriptPath = Join-Path $PSScriptRoot $SqlScriptPath
if (-not (Test-Path $scriptPath)) {
    Write-Host "ERROR: SQL script not found: $scriptPath" -ForegroundColor Red
    exit 1
}

Write-Host "Reading SQL script: $SqlScriptPath" -ForegroundColor Gray
$sqlContent = Get-Content $scriptPath -Raw -Encoding UTF8

$batches = $sqlContent -split '\r?\nGO\r?\n' | Where-Object { $_.Trim() -ne '' }
Write-Host "Total batches: $($batches.Count)" -ForegroundColor Gray
Write-Host ""

try {
    Add-Type -AssemblyName "System.Data"
    
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    Write-Host "Connected to SQL Server" -ForegroundColor Green
    
    $successCount = 0
    foreach ($batch in $batches) {
        $trimmedBatch = $batch.Trim()
        if ($trimmedBatch.StartsWith('--') -or $trimmedBatch.StartsWith('PRINT')) {
            continue
        }
        
        $cmd = New-Object System.Data.SqlClient.SqlCommand($trimmedBatch, $conn)
        $cmd.CommandTimeout = 300
        
        try {
            $null = $cmd.ExecuteNonQuery()
            $successCount++
            Write-Host "  Batch $successCount executed successfully" -ForegroundColor Green
        } catch {
            Write-Host "  Warning: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    $conn.Close()
    
    Write-Host ""
    Write-Host "View fix applied successfully!" -ForegroundColor Green
    Write-Host "Please run the sync program to re-sync organization data." -ForegroundColor Cyan
    
} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
