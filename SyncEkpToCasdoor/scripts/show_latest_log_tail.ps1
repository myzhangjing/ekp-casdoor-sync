$dir = 'c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\logs'
$log = Get-ChildItem -Path $dir -Filter 'sync_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $log) { Write-Output 'No sync logs found'; exit 0 }
Write-Output "Latest log: $($log.FullName)"
Get-Content -Path $log.FullName -Encoding Unicode -Tail 200 | ForEach-Object { Write-Output $_ }
