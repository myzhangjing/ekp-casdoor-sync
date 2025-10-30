# Simple script to generate SQL update statements
$CsvFile = Get-ChildItem -Path "logs" -Filter "organization_hierarchy_*.csv" | 
           Sort-Object LastWriteTime -Descending | 
           Select-Object -First 1

if (-not $CsvFile) {
    Write-Host "Error: No CSV file found" -ForegroundColor Red
    exit 1
}

Write-Host "Using CSV: $($CsvFile.Name)" -ForegroundColor Green

$data = Import-Csv -Path $CsvFile.FullName -Encoding UTF8
$withParent = $data | Where-Object { $_.'Casdoor父组织名称' -ne "" }

Write-Host "Found $($data.Count) organizations, $($withParent.Count) with parent" -ForegroundColor Yellow

$sqlFile = "logs\update_parent_name.sql"
$sql = @()
$sql += "USE casdoor;"
$sql += "START TRANSACTION;"
$sql += ""

foreach ($row in $withParent) {
    $groupName = $row.'组织ID' -replace "'", "''"
    $parentName = $row.'Casdoor父组织名称' -replace "'", "''"
    $sql += "UPDATE ``group`` SET parent_name = '$parentName' WHERE owner = 'fzswjtOrganization' AND name = '$groupName';"
}

$sql += ""
$sql += "COMMIT;"

$sql -join "`n" | Out-File -FilePath $sqlFile -Encoding UTF8 -Force

Write-Host "`nSQL file generated: $sqlFile" -ForegroundColor Green
Write-Host "Contains $($withParent.Count) UPDATE statements" -ForegroundColor Gray

# Show first 5 lines
Write-Host "`nFirst 5 UPDATE statements:" -ForegroundColor Cyan
Get-Content $sqlFile -Encoding UTF8 | Select-Object -Skip 3 -First 5
