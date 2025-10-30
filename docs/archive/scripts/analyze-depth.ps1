param([string]$CsvPath)
$data = Import-Csv $CsvPath -Encoding UTF8
$cols = $data[0].PSObject.Properties.Name
$colId = $cols[0]
$colName = $cols[1]
$colParentId = $cols[2]

Write-Host "分析组织层级深度..." -ForegroundColor Cyan

$byId = @{}
foreach($r in $data){
  $id = $r.$colId
  $byId[$id] = $r
}

function Get-Depth($id){
  if(-not $id){ return 0 }
  $visited = @{}
  $current = $id
  $depth = 0
  while($current -and $byId.ContainsKey($current)){
    if($visited.ContainsKey($current)){ return $depth }
    $visited[$current] = $true
    $parentId = $byId[$current].$colParentId
    if($parentId -and $parentId.Trim() -ne ''){
      $depth++
      $current = $parentId
    } else {
      break
    }
  }
  return $depth
}

$depths = @{}
foreach($r in $data){
  $id = $r.$colId
  $depth = Get-Depth $id
  if(-not $depths.ContainsKey($depth)){ $depths[$depth] = 0 }
  $depths[$depth]++
}

Write-Host "`n层级分布:" -ForegroundColor Yellow
$depths.Keys | Sort-Object | ForEach-Object {
  Write-Host "  层级 $_ : $($depths[$_]) 个组织"
}

Write-Host "`n3层及以上的示例:" -ForegroundColor Green
$samples = $data | Where-Object {
  $id = $_.$colId
  (Get-Depth $id) -ge 2
} | Select-Object -First 5

foreach($s in $samples){
  $id = $s.$colId
  $name = $s.$colName
  $depth = Get-Depth $id
  Write-Host "  - $name (层级: $depth)"
  $current = $id
  $level = 0
  while($current -and $byId.ContainsKey($current)){
    $r = $byId[$current]
    $indent = "    " * $level
    $rName = $r.$colName
    $line = $indent + "  " + $rName + " [" + $current + "]"
    Write-Host $line
    $parentId = $r.$colParentId
    if($parentId -and $parentId.Trim() -ne ''){
      $current = $parentId
      $level++
    } else {
      break
    }
  }
}
