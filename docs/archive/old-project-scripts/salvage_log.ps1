Param(
    [string]$LogFile = "..\logs\sync_20251029_134246.log"
)

# Read raw bytes
$bytes = [System.IO.File]::ReadAllBytes($LogFile)

# Find BOM positions for UTF-16 LE (FF FE) and UTF-8 (EF BB BF)
$bomUtf16 = @(0xFF,0xFE)
$bomUtf8 = @(0xEF,0xBB,0xBF)

$positions = @()
for ($i = 0; $i -lt $bytes.Length; $i++) {
    if ($i -le $bytes.Length - 2 -and $bytes[$i] -eq $bomUtf16[0] -and $bytes[$i+1] -eq $bomUtf16[1]) {
        $positions += @{pos=$i; enc='Unicode'}
    } elseif ($i -le $bytes.Length - 3 -and $bytes[$i] -eq $bomUtf8[0] -and $bytes[$i+1] -eq $bomUtf8[1] -and $bytes[$i+2] -eq $bomUtf8[2]) {
        $positions += @{pos=$i; enc='UTF8'}
    }
}

# Ensure there's at least one segment
if ($positions.Count -eq 0) {
    # Try decoding entire file as UTF8, then as Unicode
    try {
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    } catch {
        $text = [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    $lines = $text -split "\r?\n"
} else {
    # Build segments between BOM markers
    $segments = @()
    $positions = $positions | Sort-Object pos
    for ($i = 0; $i -lt $positions.Count; $i++) {
        $start = $positions[$i].pos
        $enc = $positions[$i].enc
        $end = if ($i -lt $positions.Count - 1) { $positions[$i+1].pos - 1 } else { $bytes.Length - 1 }
        $len = $end - $start + 1
        $segBytes = $bytes[$start..$end]
        if ($enc -eq 'UTF8') {
            $segText = [System.Text.Encoding]::UTF8.GetString($segBytes)
        } else {
            $segText = [System.Text.Encoding]::Unicode.GetString($segBytes)
        }
        $segments += $segText
    }
    $lines = ($segments -join "`n") -split "\r?\n"
}

# Filter lines containing useful keywords
$keywords = @('组织同步完成','用户同步完成','Membership','[Groups]','[Users]','[Membership]','upserted','ensured','Checkpoint saved','Sync finished','Starting EKP')

$found = @()
foreach ($k in $keywords) {
    $matches = $lines | Where-Object { $_ -and $_ -match [regex]::Escape($k) }
    if ($matches) {
        $found += $matches
    }
}

if ($found.Count -gt 0) {
    Write-Output "-- Salvaged lines matching keywords --"
    $found | Select-Object -Unique
} else {
    Write-Output "No keyword lines found; printing last 200 readable lines as fallback."
    $readable = $lines | Where-Object { $_ -match '[\w\p{IsCJKUnifiedIdeographs}]' }
    $readable[-200..-1] 2>$null
}
