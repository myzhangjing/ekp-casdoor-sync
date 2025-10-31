# Detailed Test - Verify Page Content
$baseUrl = "http://localhost:5233"
Write-Host "`n=== Detailed Page Content Test ===`n" -ForegroundColor Cyan

function Test-Content {
    param($path, $name, $keywords)
    Write-Host "`nTesting: $name ($path)" -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl$path" -UseBasicParsing
        $content = $response.Content
        $foundCount = 0
        foreach ($keyword in $keywords) {
            if ($content -match $keyword) {
                Write-Host "  [PASS] Found: $keyword" -ForegroundColor Green
                $foundCount++
            } else {
                Write-Host "  [MISS] Missing: $keyword" -ForegroundColor Gray
            }
        }
        Write-Host "  Result: $foundCount / $($keywords.Count) keywords found" -ForegroundColor $(if ($foundCount -ge $keywords.Count * 0.7) { "Green" } else { "Yellow" })
        return $foundCount
    } catch {
        Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
        return 0
    }
}

# Test Schedule Page
$scheduleKeywords = @(
    "schedule|定时",
    "task|任务", 
    "daily|每日",
    "interval|间隔",
    "cron",
    "full|全量",
    "incremental|增量"
)
Test-Content "/schedule" "Schedule Tasks Page" $scheduleKeywords

# Test Query Page - User Query
$userQueryKeywords = @(
    "query|查询",
    "user|用户",
    "single|单个",
    "batch|批量",
    "list|列表"
)
Test-Content "/query" "Data Query Page - User" $userQueryKeywords

# Test Query Page - Organization
$orgQueryKeywords = @(
    "organization|组织",
    "tree|树",
    "search|搜索",
    "path|路径",
    "expand|展开",
    "collapse|收起"
)
$response = Invoke-WebRequest -Uri "$baseUrl/query" -UseBasicParsing
$content = $response.Content
Write-Host "`nTesting: Organization Query Features" -ForegroundColor Yellow
$foundCount = 0
foreach ($keyword in $orgQueryKeywords) {
    if ($content -match $keyword) {
        Write-Host "  [PASS] Found: $keyword" -ForegroundColor Green
        $foundCount++
    } else {
        Write-Host "  [MISS] Missing: $keyword" -ForegroundColor Gray
    }
}
Write-Host "  Result: $foundCount / $($orgQueryKeywords.Count) keywords found" -ForegroundColor $(if ($foundCount -ge 3) { "Green" } else { "Yellow" })

# Test Navigation Menu
Write-Host "`nTesting: Navigation Menu Links" -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing
$content = $response.Content
$links = @("sync", "companies", "schedule", "query", "manage", "settings")
$foundLinks = 0
foreach ($link in $links) {
    if ($content -match "href=`"$link`"") {
        Write-Host "  [PASS] Found link: $link" -ForegroundColor Green
        $foundLinks++
    } else {
        Write-Host "  [MISS] Missing link: $link" -ForegroundColor Gray
    }
}
Write-Host "  Result: $foundLinks / $($links.Count) menu links found" -ForegroundColor $(if ($foundLinks -eq $links.Count) { "Green" } else { "Yellow" })

Write-Host "`n=== Test Complete ===`n" -ForegroundColor Cyan
