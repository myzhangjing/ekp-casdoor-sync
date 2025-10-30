$ErrorActionPreference = 'Stop'
$outDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) '..\SyncEkpToCasdoor\logs'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$base = 'http://sso.fzcsps.com'
$paths = @('/swagger','/swagger/index.html','/swagger/v1/swagger.json','/swagger.json')
foreach ($p in $paths) {
    $url = $base + $p
    try {
        $fileName = 'casdoor_swagger' + ($p -replace '/','_') + '.txt'
        $outPath = Join-Path $outDir $fileName
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15 -OutFile $outPath -ErrorAction Stop
        Write-Output "Saved $url -> $outPath"
    } catch {
        Write-Output "Failed to fetch $url : $($_.Exception.Message)"
    }
}
