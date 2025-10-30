param(
  [string]$Endpoint = 'http://sso.fzcsps.com',
  [string]$ClientId,
  [string]$ClientSecret
)

if (-not $ClientId) { $ClientId = $env:CASDOOR_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:CASDOOR_CLIENT_SECRET }
if (-not $ClientId -or -not $ClientSecret) { Write-Error 'Missing clientId/secret'; exit 2 }

$variants = @()
$variants += @{ desc='user wrapper owner=built-in name=fzcsps-app'; body = @{ user = @{ owner='built-in'; name='fzcsps-app' } }; form=$false }
$variants += @{ desc='owner=built-in name=fzcsps-app'; body = @{ owner='built-in'; name='fzcsps-app' }; form=$false }
$variants += @{ desc='form user wrapper owner=built-in name=fzcsps-app'; body = @{ user = @{ owner='built-in'; name='fzcsps-app' } }; form=$true }
$variants += @{ desc='owner=app name=app/fzcsps-app'; body = @{ owner='app'; name='app/fzcsps-app' }; form=$false }
$variants += @{ desc='owner=built-in name=app/fzcsps-app'; body = @{ owner='built-in'; name='app/fzcsps-app' }; form=$false }

foreach ($v in $variants) {
  Write-Output "\n=== Attempt: $($v.desc) ==="
  $uri = "$Endpoint/api/add-user?clientId=$([System.Net.WebUtility]::UrlEncode($ClientId))&clientSecret=$([System.Net.WebUtility]::UrlEncode($ClientSecret))"
  $json = ($v.body | ConvertTo-Json -Depth 6)
  Write-Output "POST $uri"
  Write-Output "Payload: $json"
  try {
    if ($v.form) {
      $form = "user=" + [System.Net.WebUtility]::UrlEncode($json)
      $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/x-www-form-urlencoded' -Body $form -ErrorAction Stop
    }
    else {
      $resp = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body $json -ErrorAction Stop
    }
    Write-Output "Response:`n$($resp | ConvertTo-Json -Depth 6)"
  }
  catch {
    Write-Output "Request failed: $_"
  }
}
