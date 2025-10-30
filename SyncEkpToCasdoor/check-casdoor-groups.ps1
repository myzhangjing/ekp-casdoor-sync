# 检查 Casdoor 组织的 parentName 字段
$endpoint = "http://sso.fzcsps.com"
$clientId = "aecd00a352e5c560ffe6"
$clientSecret = "4402518b20dd191b8b48d6240bc786a4f847899a"
$owner = "fzswjtOrganization"

$url = "$endpoint/api/get-groups?owner=$owner&clientId=$clientId&clientSecret=$clientSecret"

try {
    $response = Invoke-RestMethod -Uri $url -Method Get
    
    if ($response.status -eq "ok" -and $response.data) {
        Write-Host "总组织数: $($response.data.Count)" -ForegroundColor Green
        
        $withParent = $response.data | Where-Object { $_.parentName -and $_.parentName -ne "" }
        $withoutParent = $response.data | Where-Object { -not $_.parentName -or $_.parentName -eq "" }
        
        Write-Host "有父级的组织: $($withParent.Count)" -ForegroundColor Yellow
        Write-Host "无父级的组织: $($withoutParent.Count)" -ForegroundColor Cyan
        
        Write-Host "`n前10个有父级的组织:" -ForegroundColor Green
        $withParent | Select-Object -First 10 | ForEach-Object {
            Write-Host "  - $($_.displayName) (id: $($_.name)) parent: $($_.parentName)"
        }
        
        Write-Host "`n前10个无父级的组织:" -ForegroundColor Cyan
        $withoutParent | Select-Object -First 10 | ForEach-Object {
            Write-Host "  - $($_.displayName) (id: $($_.name))"
        }
    } else {
        Write-Host "API响应错误: $($response.msg)" -ForegroundColor Red
    }
} catch {
    Write-Host "请求失败: $($_.Exception.Message)" -ForegroundColor Red
}
