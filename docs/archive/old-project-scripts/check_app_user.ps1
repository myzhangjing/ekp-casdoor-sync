Param(
    [string]$ClientId = 'aecd00a352e5c560ffe6',
    [string]$ClientSecret = '4402518b20dd191b8b48d6240bc786a4f847899a',
    [string]$Owner = 'app',
    [string]$Name = 'fzcsps-app',
    [string]$Endpoint = 'http://sso.fzcsps.com'
)

$ErrorActionPreference = 'Stop'

$uri = "$Endpoint/api/get-user?id=$($Owner)/$($Name)&clientId=$ClientId&clientSecret=$ClientSecret"
Write-Output "Checking: $uri"
try {
    $resp = Invoke-RestMethod -Uri $uri -Method GET -TimeoutSec 15
    Write-Output "-- API response --"
    $resp | ConvertTo-Json -Depth 5
} catch {
    Write-Output "Request failed: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        try { $_.Exception.Response.GetResponseStream() | %{ new-object System.IO.StreamReader($_) } | %{ $_.ReadToEnd() } } catch {}
    }
}
