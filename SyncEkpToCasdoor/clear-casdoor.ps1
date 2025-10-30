# 清空 Casdoor 中的所有用户和组织
# 警告: 这将删除 fzswjtOrganization 下的所有数据!

param(
    [string]$Endpoint = "http://sso.fzcsps.com",
    [string]$ClientId = "aecd00a352e5c560ffe6",
    [string]$ClientSecret = "4402518b20dd191b8b48d6240bc786a4f847899a",
    [string]$Owner = "fzswjtOrganization"
)

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "警告: 即将删除 $Owner 下的所有用户和组织!" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "如果确认删除,请输入组织名称: $Owner"
$confirmation = Read-Host "请输入"

if ($confirmation -ne $Owner) {
    Write-Host "取消操作" -ForegroundColor Green
    exit
}

Write-Host "`n开始清空数据..." -ForegroundColor Yellow

$baseUrl = "$Endpoint/api"
$auth = "?clientId=$ClientId&clientSecret=$ClientSecret"

# 1. 获取所有用户
Write-Host "`n正在获取用户列表..." -ForegroundColor Cyan
$usersUrl = "$baseUrl/get-users$auth&owner=$Owner"
try {
    $usersResp = Invoke-RestMethod -Uri $usersUrl -Method Get -ContentType "application/json"
    if ($usersResp.status -eq "ok" -and $usersResp.data) {
        $users = $usersResp.data
        Write-Host "找到 $($users.Count) 个用户" -ForegroundColor Green
        
        # 删除每个用户
        $count = 0
        foreach ($user in $users) {
            $count++
            Write-Host "[$count/$($users.Count)] 删除用户: $($user.name) ($($user.displayName))" -ForegroundColor Gray
            
            $deleteData = @{
                owner = $Owner
                name = $user.name
            } | ConvertTo-Json
            
            try {
                $deleteResp = Invoke-RestMethod -Uri "$baseUrl/delete-user$auth" -Method Post -Body $deleteData -ContentType "application/json"
                if ($deleteResp.status -ne "ok") {
                    Write-Host "  失败: $($deleteResp.msg)" -ForegroundColor Red
                }
            } catch {
                Write-Host "  错误: $_" -ForegroundColor Red
            }
        }
        Write-Host "用户删除完成!" -ForegroundColor Green
    } else {
        Write-Host "未找到用户或查询失败" -ForegroundColor Yellow
    }
} catch {
    Write-Host "获取用户列表失败: $_" -ForegroundColor Red
}

# 2. 获取所有组织
Write-Host "`n正在获取组织列表..." -ForegroundColor Cyan
$groupsUrl = "$baseUrl/get-groups$auth&owner=$Owner"
try {
    $groupsResp = Invoke-RestMethod -Uri $groupsUrl -Method Get -ContentType "application/json"
    if ($groupsResp.status -eq "ok" -and $groupsResp.data) {
        $groups = $groupsResp.data
        Write-Host "找到 $($groups.Count) 个组织" -ForegroundColor Green
        
        # 删除每个组织
        $count = 0
        foreach ($group in $groups) {
            $count++
            Write-Host "[$count/$($groups.Count)] 删除组织: $($group.name) ($($group.displayName))" -ForegroundColor Gray
            
            $deleteData = @{
                owner = $Owner
                name = $group.name
            } | ConvertTo-Json
            
            try {
                $deleteResp = Invoke-RestMethod -Uri "$baseUrl/delete-group$auth" -Method Post -Body $deleteData -ContentType "application/json"
                if ($deleteResp.status -ne "ok") {
                    Write-Host "  失败: $($deleteResp.msg)" -ForegroundColor Red
                }
            } catch {
                Write-Host "  错误: $_" -ForegroundColor Red
            }
        }
        Write-Host "组织删除完成!" -ForegroundColor Green
    } else {
        Write-Host "未找到组织或查询失败" -ForegroundColor Yellow
    }
} catch {
    Write-Host "获取组织列表失败: $_" -ForegroundColor Red
}

Write-Host "`n清空操作完成!" -ForegroundColor Green
Write-Host "现在可以运行全量同步重新导入数据" -ForegroundColor Cyan
