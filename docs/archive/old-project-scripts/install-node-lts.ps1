$ErrorActionPreference = 'Stop'
$msi = Join-Path $env:TEMP 'node-lts.msi'
Write-Host "Downloading Node LTS MSI to: $msi"
Invoke-WebRequest -Uri 'https://nodejs.org/dist/v22.21.0/node-v22.21.0-x64.msi' -OutFile $msi -UseBasicParsing
Write-Host "Running msiexec installer (silent) ..."
Start-Process -FilePath msiexec.exe -ArgumentList "/i","$msi","/qn","/norestart" -Wait -NoNewWindow
Write-Host "Installer finished. Installed MSI path: $msi"
