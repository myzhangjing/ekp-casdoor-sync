$dir = 'c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\logs'
$files = Get-ChildItem -Path $dir -Filter 'sync_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 5
if ($files.Count -eq 0) { Write-Output 'No sync logs found'; exit 0 }

foreach ($f in $files) {
    Write-Output "\n=== $($f.FullName) ({0}) ===" -f $f.LastWriteTime
    Write-Output '--- Try UTF8 ---'
    try { Get-Content -Path $f.FullName -Encoding UTF8 -Tail 50 | ForEach-Object { Write-Output $_ } } catch { Write-Output "UTF8 read failed: $_" }
    Write-Output '--- Try Unicode (UTF-16 LE) ---'
    try { Get-Content -Path $f.FullName -Encoding Unicode -Tail 50 | ForEach-Object { Write-Output $_ } } catch { Write-Output "Unicode read failed: $_" }
    Write-Output '--- Try Default encoding ---'
    try { Get-Content -Path $f.FullName -Tail 50 | ForEach-Object { Write-Output $_ } } catch { Write-Output "Default read failed: $_" }
}
