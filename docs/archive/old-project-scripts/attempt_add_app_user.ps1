param(
  [string]$Endpoint = 'http://sso.fzcsps.com'
)

$ClientId = $env:CASDOOR_CLIENT_ID
$ClientSecret = $env:CASDOOR_CLIENT_SECRET
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'CASDOOR_CLIENT_ID/SECRET not set'; exit 2 }

function TryPostJson($obj) {
  $uri = "$Endpoint/api/add-user?clientId=$ClientId&clientSecret=$ClientSecret"
  Write-Output "POST JSON -> $uri"
  $json = $obj | ConvertTo-Json -Depth 6
  Write-Output "Payload: $json"
  try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body $json -ErrorAction Stop
    Write-Output ("Response: " + ($resp | ConvertTo-Json -Depth 6))
  } catch {
    Write-Output ("Error: " + $_.Exception.Message)
    if ($_.Exception.Response) { $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); Write-Output $sr.ReadToEnd() }
  }
}

function TryPostForm($obj) {
  $uri = "$Endpoint/api/add-user?clientId=$ClientId&clientSecret=$ClientSecret"
  $json = $obj | ConvertTo-Json -Depth 6
  $form = "user=" + [System.Net.WebUtility]::UrlEncode($json)
  Write-Output "POST FORM -> $uri"
  Write-Output "Form payload: $form"
  try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/x-www-form-urlencoded' -Body $form -ErrorAction Stop
    Write-Output ("Response: " + ($resp | ConvertTo-Json -Depth 6))
  } catch {
    Write-Output ("Error: " + $_.Exception.Message)
    if ($_.Exception.Response) { $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); Write-Output $sr.ReadToEnd() }
  }
}

Write-Output "=== Attempt A: owner=app, name=app-built-in (json) ==="
TryPostJson @{ owner='app'; name='app-built-in' }

Write-Output "=== Attempt B: wrapper user (json) ==="
TryPostJson @{ user = @{ owner='app'; name='app-built-in' } }

Write-Output "=== Attempt C: form user=... ==="
TryPostForm @{ user = @{ owner='app'; name='app-built-in' } }

Write-Output "=== Attempt D: owner=app name=app/app-built-in (json) ==="
TryPostJson @{ owner='app'; name='app/app-built-in' }
