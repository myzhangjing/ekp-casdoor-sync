# 使用环境变量（若未设置则使用回退值）
$Endpoint = if ($env:CASDOOR_ENDPOINT) { $env:CASDOOR_ENDPOINT } else { 'http://sso.fzcsps.com' }
$ClientId = if ($env:CASDOOR_CLIENT_ID) { $env:CASDOOR_CLIENT_ID } else { 'cb838421e04ecd30f72b' }
$ClientSecret = if ($env:CASDOOR_CLIENT_SECRET) { $env:CASDOOR_CLIENT_SECRET } else { 'e54b3c2d06c864ac9243a03b8002b75167dce01d' }

$body = @{ owner='app'; name='app-built-in' } | ConvertTo-Json
$url = "$Endpoint/api/add-user?clientId=$ClientId&clientSecret=$ClientSecret"
Write-Output "POST $url`n$body"
try {
    $r = Invoke-RestMethod -Method Post -Uri $url -Body $body -ContentType 'application/json' -ErrorAction Stop
    $r | ConvertTo-Json -Depth 10
} catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $txt = $sr.ReadToEnd()
        Write-Output $txt
    }
}
