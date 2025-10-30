# 批量用 parentId 修复 Casdoor 组织父子关系（基于导出的 CSV）
param(
  [string]$Endpoint = $env:CASDOOR_ENDPOINT,
  [string]$Owner = $env:CASDOOR_DEFAULT_OWNER,
  [string]$ClientId = $env:CASDOOR_CLIENT_ID,
  [string]$ClientSecret = $env:CASDOOR_CLIENT_SECRET
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$logs = Join-Path $root 'logs'
$csv = Get-ChildItem -Path $logs -Filter 'organization_hierarchy_*.csv' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $csv) { Write-Host "未找到CSV文件" -ForegroundColor Red; exit 1 }

Write-Host "使用CSV: $($csv.Name)" -ForegroundColor Green
$data = Import-Csv -Path $csv.FullName -Encoding UTF8
# 动态解析列名（避免在PS5.1中中文字面量编码问题）
$first = $data | Select-Object -First 1
$cols = $first.psobject.Properties.Name
$colId = $cols[0]
$colName = $cols[1]
$colParentId = $cols[2]

$rows = $data # 顶层也需要处理，空父级将被回退为$Owner

$fixed = 0; $errors = 0
foreach ($row in $rows) {
  $gid = $row.$colId
  $parentId = $row.$colParentId
  $display = $row.$colName
  $idFull = "$Owner/$gid"
  $url = "$Endpoint/api/update-group?id=$idFull&clientId=$ClientId&clientSecret=$ClientSecret"
  if (-not $parentId -or $parentId.Trim() -eq '') { $parentId = $Owner }
  $body = @{ id=$idFull; owner=$Owner; name=$gid; displayName=$display; parentId=$parentId; isEnabled=$true } | ConvertTo-Json -Compress
  try {
    $resp = Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType 'application/json; charset=utf-8'
    if ($resp.status -eq 'ok') { $fixed++ } else { $errors++ }
  }
  catch {
    $errors++
  }
}

Write-Host "修复完成: 成功=$fixed, 失败=$errors, 总计=$($rows.Count)" -ForegroundColor Cyan

# 快速抽样验证
try {
  $checkUrl = "$Endpoint/api/get-groups?owner=$Owner&clientId=$ClientId&clientSecret=$ClientSecret"
  $resp = Invoke-RestMethod -Uri $checkUrl -Method Get
  $all = $resp.data
  $withParentId = ($all | Where-Object { $_.parentId -and $_.parentId -ne '' }).Count
  Write-Host "当前组织总数=$($all.Count)，其中parentId已设置=$withParentId" -ForegroundColor Yellow
}
catch {}
