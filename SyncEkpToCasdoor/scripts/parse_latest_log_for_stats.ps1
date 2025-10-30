$logDir = Join-Path $PSScriptRoot '..\logs'
$latest = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { Write-Output 'NO_LOGS'; exit 0 }
$bytes = [System.IO.File]::ReadAllBytes($latest.FullName)
$text = $null
if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) { $text = [System.Text.Encoding]::Unicode.GetString($bytes) }
elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $text = [System.Text.Encoding]::UTF8.GetString($bytes) }
else { $text = [System.Text.Encoding]::UTF8.GetString($bytes) }

# Simple pattern matches
$stats = @{}
$patterns = @(
    @{name='GroupsUpsert'; rx='Groups.*upserted[: ]+([0-9]+)';},
    @{name='UsersUpsert'; rx='Users.*upserted[: ]+([0-9]+)';},
    @{name='MembershipEnsured'; rx='Membership.*ensured[: ]+([0-9]+)';},
    @{name='CheckpointSaved'; rx='Checkpoint saved';}
)
foreach ($p in $patterns) {
    if ($p.name -eq 'CheckpointSaved') {
        $stats[$p.name] = ([regex]::IsMatch($text,$p.rx))
    } else {
        $m = [regex]::Match($text,$p.rx)
        if ($m.Success) { $stats[$p.name] = $m.Groups[1].Value } else { $stats[$p.name] = $null }
    }
}

# Count error keywords
$errCount = 0
$errCount += ([regex]::Matches($text,'\bFatal\b','IgnoreCase')).Count
$errCount += ([regex]::Matches($text,'\bERROR\b','IgnoreCase')).Count
$errCount += ([regex]::Matches($text,'\bException\b','IgnoreCase')).Count

Write-Output "Latest log: $($latest.FullName)"
Write-Output "Log size: $(([System.IO.File]::GetLength($latest.FullName))) bytes"
Write-Output "Stats:"
$stats.GetEnumerator() | ForEach-Object { Write-Output "  $($_.Key): $($_.Value)" }
Write-Output "Error-like keywords occurrences: $errCount"

# If we couldn't find stats, show first 2000 decoded chars for inspection
if (-not $stats.GroupsUpsert -and -not $stats.UsersUpsert -and -not $stats.MembershipEnsured) {
    Write-Output "\n--- DECODED SNIPPET (first 2000 chars) ---"
    $snippet = $text.Substring(0,[Math]::Min(2000,$text.Length))
    Write-Output $snippet
}
