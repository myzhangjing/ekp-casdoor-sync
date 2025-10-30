param(
  [string]$Endpoint = 'http://sso.fzcsps.com',
  [string]$ClientId,
  [string]$ClientSecret
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'Missing clientId/secret'; exit 2 }

$name = 'fzswjtOrganization'
$owners = @('admin','built-in','app')

foreach ($owner in $owners) {
  Write-Output "\n=== Attempt owner=$owner name=$name ==="
  $uri = "$Endpoint/api/add-user?clientId=$ClientId&clientSecret=$ClientSecret"
  $payload = @{ owner = $owner; name = $name } | ConvertTo-Json -Depth 6
  Write-Output "POST $uri"
  Write-Output "Payload: $payload"
  try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body $payload -ErrorAction Stop
    Write-Output ($resp | ConvertTo-Json -Depth 6)
  }
  catch {
    Write-Output "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
      $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
      Write-Output $sr.ReadToEnd()
    }
  }
}
