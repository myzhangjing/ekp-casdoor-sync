param(
  [string]$Endpoint = $env:CASDOOR_ENDPOINT,
  [string]$Owner = $env:CASDOOR_DEFAULT_OWNER,
  [string]$ClientId = $env:CASDOOR_CLIENT_ID,
  [string]$ClientSecret = $env:CASDOOR_CLIENT_SECRET
)

$ErrorActionPreference = 'Stop'

function Get-Groups {
  $url = $Endpoint + '/api/get-groups?owner=' + $Owner + '&clientId=' + $ClientId + '&clientSecret=' + $ClientSecret
  return Invoke-RestMethod -Uri $url -Method Get
}

$resp = Get-Groups
$all = @($resp.data)
$empty = $all | Where-Object { -not $_.parentId -or $_.parentId.Trim() -eq '' }

Write-Host ("Total groups={0}, empty parentId={1}" -f $all.Count, $empty.Count) -ForegroundColor Yellow

$fixed=0; $errors=0
foreach($g in $empty){
  $name = $g.name
  $idFull = $Owner + '/' + $name
  $escapedId = [Uri]::EscapeDataString($idFull)
  $url = $Endpoint + '/api/update-group?id=' + $escapedId + '&clientId=' + $ClientId + '&clientSecret=' + $ClientSecret
  $body = @{ id=$idFull; owner=$Owner; name=$name; displayName=$g.displayName; parentId=$Owner; isEnabled=([bool]$g.isEnabled) } | ConvertTo-Json -Compress
  try{
    $r = Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType 'application/json; charset=utf-8'
    if($r.status -eq 'ok'){ $fixed++ } else { $errors++ }
  }catch{ $errors++ }
}

$resp2 = Get-Groups
$all2 = @($resp2.data)
$empty2 = $all2 | Where-Object { -not $_.parentId -or $_.parentId.Trim() -eq '' }
Write-Host ("Fix done: ok={0}, err={1}; remaining empty parentId={2}" -f $fixed,$errors,$empty2.Count) -ForegroundColor Cyan
