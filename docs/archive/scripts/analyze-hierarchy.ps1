param(
  [string]$CsvPath = "logs/organization_hierarchy_*.csv",
  [int]$MaxRows = 10000
)
$ErrorActionPreference = "Stop"

# Resolve file
$files = Get-ChildItem -Path $CsvPath | Sort-Object LastWriteTime -Descending
if(-not $files){ Write-Host "No CSV found by pattern: $CsvPath" -ForegroundColor Yellow; exit 1 }
$csv = $files[0].FullName
Write-Host ("Using CSV: {0}" -f $csv) -ForegroundColor Cyan

# Load CSV (UTF8)
$data = Import-Csv -Path $csv -Encoding UTF8
if(-not $data -or $data.Count -eq 0){
  # fallback to default encoding (Windows-1252/GBK depending on system)
  try { $data = Import-Csv -Path $csv } catch {}
}
if(-not $data -or $data.Count -eq 0){ Write-Host "CSV empty" -ForegroundColor Yellow; exit 0 }

# Map to neutral keys to avoid Chinese property name issues
$mapped = @()
foreach($row in $data){
  $id = $row."组织ID"
  $pval = $row."父组织ID"
  if([string]::IsNullOrWhiteSpace($id)){ continue }
  $mapped += [pscustomobject]@{ Id = $id; ParentId = $pval }
}

# Build lookup
$byId = @{}
foreach($r in $mapped){ $byId[$r.Id] = $r }

# Compute depth
$depthCache = @{}
function Get-Depth([string]$id, [System.Collections.Generic.HashSet[string]]$stack){
  if($depthCache.ContainsKey($id)){ return $depthCache[$id] }
  if(-not $byId.ContainsKey($id)){ $depthCache[$id] = 0; return 0 }
  if(-not $stack){ $stack = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase) }
  if(-not $stack.Add($id)){ $depthCache[$id] = 0; return 0 }
  $p = $byId[$id].ParentId
  if([string]::IsNullOrWhiteSpace($p) -or -not $byId.ContainsKey($p)){
    $depth = 0
  } else {
    $depth = 1 + (Get-Depth -id $p -stack $stack)
  }
  $stack.Remove($id) | Out-Null
  $depthCache[$id] = $depth
  return $depth
}

$depthGroups = @{}
foreach($r in $mapped){
  $d = Get-Depth -id $r.Id -stack $null
  if(-not $depthGroups.ContainsKey($d)){ $depthGroups[$d] = 0 }
  $depthGroups[$d]++
}

Write-Host "Depth distribution:" -ForegroundColor White
$keys = $depthGroups.Keys | Sort-Object
foreach($k in $keys){ Write-Host ("  depth {0} : {1}" -f $k, $depthGroups[$k]) -ForegroundColor $(if($k -ge 2){'Green'}else{'Gray'}) }

# Find any depth>=2 samples
$samples = @()
foreach($r in $mapped){
  $d = Get-Depth -id $r.Id -stack $null
  if($d -ge 2){ $samples += $r; if($samples.Count -ge 5){ break } }
}
if($samples.Count -gt 0){
  Write-Host "\nFound depth>=2 samples (showing chains):" -ForegroundColor Green
  foreach($s in $samples){
    $chain = New-Object System.Collections.Generic.List[string]
    $cur = $s.Id
    while($cur){ $chain.Add($cur); $cur = ($byId[$cur].ParentId); if($chain.Count -gt 10){ break } }
    Write-Host ("  " + ($chain -join ' -> ')) -ForegroundColor Green
  }
} else {
  Write-Host "\nNo nodes deeper than depth 1 found (only 2 levels)." -ForegroundColor Yellow
}
