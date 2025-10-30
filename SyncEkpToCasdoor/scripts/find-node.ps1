Write-Host "Searching registry for Node.js uninstall entries..."
$roots = @('HKLM:\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall','HKLM:\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall')
foreach ($r in $roots) {
  if (Test-Path $r) {
    Get-ChildItem $r -ErrorAction SilentlyContinue | ForEach-Object {
      try {
        $props = Get-ItemProperty $_.PSPath -ErrorAction Stop
        if ($props.DisplayName -and $props.DisplayName -like '*Node.js*') {
          Write-Host "Registry: $($props.DisplayName) -> InstallLocation: $($props.InstallLocation)"
        }
      } catch { }
    }
  }
}

Write-Host "Checking common install locations..."
$common = @( 'C:\Program Files\nodejs', 'C:\Program Files (x86)\nodejs', "$env:LOCALAPPDATA\Programs\nodejs", 'C:\Program Files\nodejs\bin' )
foreach ($p in $common) { if (Test-Path $p) { Write-Host "Exists: $p" } }

Write-Host "Looking for node.exe in 'C:\Program Files' (first 20 results)..."
try { Get-ChildItem 'C:\Program Files' -Filter 'node.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName -First 20 | ForEach-Object { Write-Host $_.FullName } } catch { }

Write-Host "Looking for node.exe in LocalAppData (first 20 results)..."
try { Get-ChildItem $env:LOCALAPPDATA -Filter 'node.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName -First 20 | ForEach-Object { Write-Host $_.FullName } } catch { }

Write-Host "Done. If node.exe not found, try checking 'C:\Program Files\nodejs' or reinstall Node and restart shell." 
