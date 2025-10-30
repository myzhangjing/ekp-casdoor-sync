param(
  [string]$Version = '22.21.0'
)

$ErrorActionPreference = 'Stop'
$zip = Join-Path $PSScriptRoot "..\SyncEkpToCasdoor\playwright\node-win.zip" | Resolve-Path -ErrorAction SilentlyContinue | ForEach-Object { $_.ProviderPath }
if (-not $zip) { $zip = Join-Path $PSScriptRoot "..\SyncEkpToCasdoor\playwright\node-win.zip" }
$dest = Join-Path $PSScriptRoot "..\SyncEkpToCasdoor\playwright\node"

Write-Host "Downloading Node $Version zip to: $zip"
$uri = "https://nodejs.org/dist/v$Version/node-v$Version-win-x64.zip"
Invoke-WebRequest -Uri $uri -OutFile $zip -UseBasicParsing

if (Test-Path $dest) { Write-Host "Removing existing $dest"; Remove-Item $dest -Recurse -Force }
Write-Host "Extracting $zip -> $dest"
Expand-Archive -Path $zip -DestinationPath $dest -Force

Write-Host "Files extracted (first 40):"
Get-ChildItem $dest -Recurse -File | Select-Object FullName -First 40 | ForEach-Object { Write-Host $_.FullName }

Write-Host "Done. You can locate node.exe under the extracted folder and run it directly."
