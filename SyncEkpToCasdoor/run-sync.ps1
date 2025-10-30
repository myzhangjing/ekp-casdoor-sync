$ErrorActionPreference = "Stop"

# Resolve paths relative to this script
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dll = Join-Path $root "bin\Release\net8.0\SyncEkpToCasdoor.dll"
$logDir = Join-Path $root "logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$logFile = Join-Path $logDir ("sync_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")

# Environment: 请在执行前通过系统环境变量或 CI/CD 注入，避免把密钥写入脚本或仓库
# 示例（请勿提交到仓库）：
# $env:EKP_SQLSERVER_CONN = "Server=<server>,<port>;Database=<db>;User Id=<user>;Password=<password>;TrustServerCertificate=True;"
# $env:CASDOOR_ENDPOINT = "http://172.16.10.110:8000"
# $env:CASDOOR_CLIENT_ID = "<client-id>"
# $env:CASDOOR_CLIENT_SECRET = "<client-secret>"
# $env:CASDOOR_DEFAULT_OWNER = "fzswjtOrganization"
# $env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"
# 可选：一次性全量（需要恢复增量时，将此行注释或移除）
# $env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"

# 基本校验（缺少关键变量则终止）
$required = @('EKP_SQLSERVER_CONN','CASDOOR_ENDPOINT','CASDOOR_CLIENT_ID','CASDOOR_CLIENT_SECRET','CASDOOR_DEFAULT_OWNER')
$missing = @()
foreach($k in $required){ if([string]::IsNullOrWhiteSpace((Get-Item Env:$k).Value)){ $missing += $k } }
if($missing.Count -gt 0){ throw "缺少必要的环境变量: $($missing -join ', ')" }

Write-Output ("[{0}] Starting EKP -> Casdoor sync" -f (Get-Date)) | Out-File -FilePath $logFile -Encoding utf8 -Append

# Ensure the release build exists (optional)
if (-not (Test-Path $dll)) {
  & $dotnet build "$root\SyncEkpToCasdoor.csproj" -c Release | Out-File -FilePath $logFile -Encoding utf8 -Append
}

# Run sync using ProcessStartInfo to redirect output (works better for native process streams)
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $dotnet
$startInfo.Arguments = "`"$dll`""
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true

$process = [System.Diagnostics.Process]::Start($startInfo)

# Read async to avoid deadlocks
$stdOutTask = $process.StandardOutput.ReadToEndAsync()
$stdErrTask = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()

# Append outputs to log
$stdOutTask.Result | Out-File -FilePath $logFile -Append -Encoding utf8
$stdErrTask.Result | Out-File -FilePath $logFile -Append -Encoding utf8

Write-Output ("[{0}] Sync finished" -f (Get-Date)) | Out-File -FilePath $logFile -Encoding utf8 -Append
