param(
  [string]$Endpoint = "http://sso.fzcsps.com",
  [string]$ClientId,
  [string]$ClientSecret
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) {
  Write-Error "Provide ClientId/ClientSecret via params or environment variables."
  exit 2
}

try {
  $id1 = 'built-in/app'
  $uri1 = "$Endpoint/api/get-organization?id=$([System.Net.WebUtility]::UrlEncode($id1))&clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri1"
  $r1 = Invoke-RestMethod -Method Get -Uri $uri1 -ErrorAction Stop
  Write-Output "Response for $id1"
  Write-Output ($r1 | ConvertTo-Json -Depth 6)

  $id2 = 'app'
  $uri2 = "$Endpoint/api/get-organization?id=$([System.Net.WebUtility]::UrlEncode($id2))&clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri2"
  $r2 = Invoke-RestMethod -Method Get -Uri $uri2 -ErrorAction Stop
  Write-Output "Response for $id2"
  Write-Output ($r2 | ConvertTo-Json -Depth 6)

  # Try get-organizations list
  $uri3 = "$Endpoint/api/get-organizations?clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri3"
  $r3 = Invoke-RestMethod -Method Get -Uri $uri3 -ErrorAction Stop
  Write-Output "get-organizations response"
  Write-Output ($r3 | ConvertTo-Json -Depth 6)
}
catch {
  Write-Error "Failed to query organizations: $_"
  exit 4
}
