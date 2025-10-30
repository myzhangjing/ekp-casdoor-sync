param(
  [string]$Endpoint = "http://sso.fzcsps.com",
  [string]$ClientId,
  [string]$ClientSecret,
  [string]$Owner = 'app'
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'Provide clientId/secret via env or params'; exit 2 }

try {
  $uri = "$Endpoint/api/get-users?owner=$([System.Net.WebUtility]::UrlEncode($Owner))&clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  Write-Output "GET $uri"
  $r = Invoke-RestMethod -Method Get -Uri $uri -ErrorAction Stop
  Write-Output ($r | ConvertTo-Json -Depth 6)
}
catch { Write-Error "Failed to get users: $_"; exit 4 }
