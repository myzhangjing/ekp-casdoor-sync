param(
  [string]$Endpoint = "http://sso.fzcsps.com",
  [string]$ClientId,
  [string]$ClientSecret,
  [string]$Owner = "built-in",
  [string]$Name = "app",
  [string]$DisplayName = $null,
  [string]$Description = $null
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }

if (-not $ClientId -or -not $ClientSecret) {
  Write-Error "CASDOOR clientId/secret not provided. Set environment variables CASDOOR_CLIENT_ID and CASDOOR_CLIENT_SECRET, or pass -ClientId and -ClientSecret parameters."
  exit 2
}

if (-not $DisplayName) { $DisplayName = $Name }

try {
  $uri = "$Endpoint/api/add-organization?clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"

  $payload = [ordered]@{
    owner = $Owner
    name = $Name
    displayName = $DisplayName
  }
  if ($Description) { $payload.description = $Description }

  $json = $payload | ConvertTo-Json -Depth 6

  Write-Output "POST $uri"
  Write-Output "Payload: $json"

  $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body $json -ErrorAction Stop

  Write-Output "Response:`n$($resp | ConvertTo-Json -Depth 6)"
  if ($resp.status -ne 'ok') {
    Write-Error "Casdoor returned non-ok status: $($resp | ConvertTo-Json -Depth 4)"
    exit 3
  }

  Write-Output "Organization created/added successfully (status ok)."
  exit 0
}
catch {
  Write-Error "Failed to call Casdoor API: $_"
  exit 4
}
