$logDir = Join-Path $PSScriptRoot '..\logs'
$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { Write-Output 'NO_LOGS'; exit 0 }
Write-Output "LATEST_LOG: $($latest.FullName)"
$bytes = [System.IO.File]::ReadAllBytes($latest.FullName)

# Detect BOM
$text = $null
if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
    $text = [System.Text.Encoding]::Unicode.GetString($bytes)
} elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
} else {
    try {
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    } catch {
        $text = [System.Text.Encoding]::Default.GetString($bytes)
    }
}

# Normalize line endings and print last 300 lines
$lines = $text -split "\r?\n"
$take = [Math]::Min($lines.Length,300)
$lines[($lines.Length - $take) .. ($lines.Length - 1)] | ForEach-Object { Write-Output $_ }
