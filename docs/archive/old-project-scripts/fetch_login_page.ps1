$url = 'http://sso.fzcsps.com/login'
Write-Output "GET $url"
$r = Invoke-WebRequest -Uri $url -UseBasicParsing -SessionVariable s -ErrorAction Stop
$outFile = 'C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\logs\casdoor_login_page.html'
$r.Content | Out-File -FilePath $outFile -Encoding utf8
Write-Output "Saved to $outFile"
