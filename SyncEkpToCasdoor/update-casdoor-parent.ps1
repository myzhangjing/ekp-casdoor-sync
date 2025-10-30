# 直接更新Casdoor数据库中的group parentName字段
# 由于API不支持设置parentName,需要直接操作数据库

param(
    [Parameter(Mandatory=$false)]
    [string]$CsvFile = "",
    
    [Parameter(Mandatory=$false)]
    [string]$Server = "192.168.52.3",
    
    [Parameter(Mandatory=$false)]
    [int]$Port = 13306,
    
    [Parameter(Mandatory=$false)]
    [string]$Database = "casdoor",
    
    [Parameter(Mandatory=$false)]
    [string]$User = "root",
    
    [Parameter(Mandatory=$false)]
    [string]$Password = "Abc123456"
)

# 如果没有指定CSV文件,查找最新的
if ([string]::IsNullOrWhiteSpace($CsvFile)) {
    $latestCsv = Get-ChildItem -Path "logs" -Filter "organization_hierarchy_*.csv" | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1
    
    if ($latestCsv) {
        $CsvFile = $latestCsv.FullName
        Write-Host "使用最新的CSV文件: $($latestCsv.Name)" -ForegroundColor Green
    } else {
        Write-Host "错误: 未找到组织层级CSV文件" -ForegroundColor Red
        exit 1
    }
}

# 读取CSV文件
Write-Host "`n正在读取CSV文件..." -ForegroundColor Cyan
$data = Import-Csv -Path $CsvFile -Encoding UTF8

$withParent = $data | Where-Object { $_.'Casdoor父组织名称' -ne "" }
Write-Host "找到 $($data.Count) 个组织,其中 $($withParent.Count) 个有父组织" -ForegroundColor Yellow

# 生成MySQL更新SQL脚本
$sqlFile = "logs\update_parent_name.sql"
$sqlContent = @"
-- 更新Casdoor group表的parent_name字段
-- 生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- 数据源: $CsvFile

USE $Database;

-- 开始事务
START TRANSACTION;

-- 显示更新前的统计
SELECT 
    '更新前' AS status,
    COUNT(*) AS total_groups,
    SUM(CASE WHEN parent_name IS NOT NULL AND parent_name != '' THEN 1 ELSE 0 END) AS with_parent,
    SUM(CASE WHEN parent_name IS NULL OR parent_name = '' THEN 1 ELSE 0 END) AS without_parent
FROM ``group``
WHERE owner = 'fzswjtOrganization';

-- 批量更新parent_name
"@

$updateCount = 0
foreach ($row in $withParent) {
    $groupName = $row.'组织ID'
    $parentName = $row.'Casdoor父组织名称'
    
    # 转义单引号
    $groupNameEscaped = $groupName -replace "'", "''"
    $parentNameEscaped = $parentName -replace "'", "''"
    
    $sqlContent += @"

UPDATE ``group`` 
SET parent_name = '$parentNameEscaped'
WHERE owner = 'fzswjtOrganization' AND name = '$groupNameEscaped';
"@
    $updateCount++
}

$sqlContent += @"


-- 显示更新后的统计
SELECT 
    '更新后' AS status,
    COUNT(*) AS total_groups,
    SUM(CASE WHEN parent_name IS NOT NULL AND parent_name != '' THEN 1 ELSE 0 END) AS with_parent,
    SUM(CASE WHEN parent_name IS NULL OR parent_name = '' THEN 1 ELSE 0 END) AS without_parent
FROM ``group``
WHERE owner = 'fzswjtOrganization';

-- 显示部分更新结果
SELECT name, display_name, parent_name
FROM ``group``
WHERE owner = 'fzswjtOrganization' AND parent_name IS NOT NULL AND parent_name != ''
ORDER BY display_name
LIMIT 10;

-- 提交事务
COMMIT;
"@

# 保存SQL文件
$sqlContent | Out-File -FilePath $sqlFile -Encoding UTF8 -Force
Write-Host "`n✓ 已生成SQL脚本: $sqlFile" -ForegroundColor Green
Write-Host "  包含 $updateCount 条UPDATE语句" -ForegroundColor Gray

# 询问是否执行
Write-Host "`n" -NoNewline
Write-Host "警告: " -ForegroundColor Yellow -NoNewline
Write-Host "即将通过MySQL直接更新Casdoor数据库"
Write-Host "数据库: ${Server}:${Port}/${Database}" -ForegroundColor Gray
Write-Host "影响表: group" -ForegroundColor Gray
Write-Host "影响记录: $updateCount 个组织的parent_name字段" -ForegroundColor Gray

$confirm = Read-Host "`n是否继续执行? (输入 YES 确认)"

if ($confirm -ne "YES") {
    Write-Host "`n已取消操作。SQL脚本已保存,可稍后手动执行:" -ForegroundColor Yellow
    Write-Host "  mysql -h $Server -P $Port -u $User -p $Database < $sqlFile" -ForegroundColor Gray
    exit 0
}

# 检查是否安装了MySQL客户端
$mysqlCmd = Get-Command mysql -ErrorAction SilentlyContinue
if (-not $mysqlCmd) {
    Write-Host "`n错误: 未找到mysql命令。请先安装MySQL客户端。" -ForegroundColor Red
    Write-Host "可以手动执行SQL文件:" -ForegroundColor Yellow
    Write-Host "  mysql -h $Server -P $Port -u $User -p $Database < $sqlFile" -ForegroundColor Gray
    exit 1
}

# 执行SQL脚本
Write-Host "`n正在执行SQL脚本..." -ForegroundColor Cyan
try {
    $env:MYSQL_PWD = $Password
    $result = & mysql -h $Server -P $Port -u $User -D $Database -e "source $sqlFile" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✓ SQL脚本执行成功!" -ForegroundColor Green
        Write-Host "`n执行结果:" -ForegroundColor Cyan
        Write-Host $result
        
        Write-Host "`n请在Casdoor UI中刷新页面查看组织结构" -ForegroundColor Yellow
    } else {
        Write-Host "`n✗ SQL脚本执行失败" -ForegroundColor Red
        Write-Host $result
        exit 1
    }
} catch {
    Write-Host "`n✗ 执行出错: $_" -ForegroundColor Red
    exit 1
} finally {
    Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue
}
