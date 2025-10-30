param(
  [string]$Endpoint = 'http://sso.fzcsps.com',
  [string]$ClientId,
  [string]$ClientSecret
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'Provide ClientId/ClientSecret via params or env'; exit 2 }

try {
  $id1 = 'built-in/fzcsps-app'
  $uri1 = "$Endpoint/api/get-application?id=$([System.Net.WebUtility]::UrlEncode($id1))&clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri1"
  $r1 = Invoke-RestMethod -Method Get -Uri $uri1 -ErrorAction SilentlyContinue
  if ($r1) { Write-Output ($r1 | ConvertTo-Json -Depth 6) } else { Write-Output 'No response or error for built-in/fzcsps-app' }

  $id2 = 'app/fzcsps-app'
  $uri2 = "$Endpoint/api/get-application?id=$([System.Net.WebUtility]::UrlEncode($id2))&clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri2"
  $r2 = Invoke-RestMethod -Method Get -Uri $uri2 -ErrorAction SilentlyContinue
  if ($r2) { Write-Output ($r2 | ConvertTo-Json -Depth 6) } else { Write-Output 'No response or error for app/fzcsps-app' }

  $uri3 = "$Endpoint/api/get-applications?clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri3"
  $r3 = Invoke-RestMethod -Method Get -Uri $uri3 -ErrorAction SilentlyContinue
  if ($r3) { Write-Output ($r3 | ConvertTo-Json -Depth 6) } else { Write-Output 'No response or error for get-applications' }
}
catch { Write-Error "Failed to query applications: $_"; exit 4 }
