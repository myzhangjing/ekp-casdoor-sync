# =====================================
# 完全自动化测试脚本
# =====================================

$baseUrl = "http://localhost:5233"
$apiUrl = "$baseUrl/api/sync"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor 同步系统自动化测试" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 测试计数器
$testsPassed = 0
$testsFailed = 0
$testsTotal = 6

function Write-TestHeader {
    param([string]$testName, [int]$testNumber)
    Write-Host ""
    Write-Host "[$testNumber/$testsTotal] $testName" -ForegroundColor Yellow
    Write-Host "-------------------------------------" -ForegroundColor Gray
}

function Write-Success {
    param([string]$message)
    Write-Host "  ✓ $message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$message)
    Write-Host "  ✗ $message" -ForegroundColor Red
}

function Write-Info {
    param([string]$message)
    Write-Host "  ℹ $message" -ForegroundColor Cyan
}

function Wait-WithProgress {
    param([int]$seconds, [string]$message)
    Write-Info $message
    for ($i = $seconds; $i -gt 0; $i--) {
        Write-Host "`r  等待: $i 秒..." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
    Write-Host "`r  等待: 完成    " -ForegroundColor Green
}

# =====================================
# 测试 1: 检查应用是否运行
# =====================================
Write-TestHeader "检查应用运行状态" 1
try {
    $response = Invoke-WebRequest -Uri $baseUrl -TimeoutSec 5 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Success "应用运行正常 (http://localhost:5233)"
        $testsPassed++
    }
} catch {
    Write-Failure "应用未运行或无法访问"
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor DarkRed
    $testsFailed++
    Write-Host ""
    Write-Host "请先启动应用:" -ForegroundColor Yellow
    Write-Host "  cd SyncEkpToCasdoor.Web" -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    exit 1
}

# =====================================
# 测试 2: 创建 API 端点
# =====================================
Write-TestHeader "检查 API 端点" 2
Write-Info "为了自动化测试，需要添加 API 控制器..."

# 检查 Controllers 目录是否存在
$controllersPath = "C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\Controllers"
if (-not (Test-Path $controllersPath)) {
    New-Item -ItemType Directory -Path $controllersPath -Force | Out-Null
    Write-Success "创建 Controllers 目录"
}

# 创建 API 控制器
$apiControllerContent = @'
using Microsoft.AspNetCore.Mvc;
using SyncEkpToCasdoor.Web.Services;

namespace SyncEkpToCasdoor.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [HttpGet("test-connections")]
    public async Task<IActionResult> TestConnections()
    {
        try
        {
            var result = await _syncService.TestConnectionsAsync();
            return Ok(new 
            { 
                success = result.Success,
                message = result.Message,
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewSync()
    {
        try
        {
            var result = await _syncService.PreviewSyncAsync();
            return Ok(new 
            { 
                success = result.Success,
                message = result.Message,
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览同步失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("full-sync")]
    public async Task<IActionResult> FullSync()
    {
        try
        {
            var result = await _syncService.FullSyncAsync(incremental: false);
            return Ok(new 
            { 
                success = result.Success,
                message = result.Message,
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完整同步失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = _syncService.GetSyncStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取状态失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int count = 50)
    {
        try
        {
            var logs = await _syncService.GetSyncLogsAsync(count);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
'@

$apiControllerPath = Join-Path $controllersPath "SyncController.cs"
Set-Content -Path $apiControllerPath -Value $apiControllerContent -Encoding UTF8
Write-Success "创建 API 控制器: Controllers/SyncController.cs"

Write-Info "重新编译并启动应用..."
Write-Host ""
Write-Host "  请按以下步骤操作:" -ForegroundColor Yellow
Write-Host "  1. 停止当前运行的应用 (Ctrl+C)" -ForegroundColor White
Write-Host "  2. 重新编译: dotnet build" -ForegroundColor White
Write-Host "  3. 重新运行: dotnet run" -ForegroundColor White
Write-Host "  4. 重新运行此测试脚本" -ForegroundColor White
Write-Host ""
Write-Host "或者运行以下命令自动重启:" -ForegroundColor Yellow
Write-Host "  .\restart-and-test.ps1" -ForegroundColor White
Write-Host ""

# 创建自动重启脚本
$restartScript = @'
# 停止所有 SyncEkpToCasdoor.Web 进程
Get-Process | Where-Object { $_.ProcessName -like "*SyncEkpToCasdoor.Web*" } | Stop-Process -Force

# 等待进程停止
Start-Sleep -Seconds 2

# 重新编译
Write-Host "正在编译..." -ForegroundColor Cyan
cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "编译成功!" -ForegroundColor Green
    
    # 在新窗口启动应用
    Write-Host "启动应用..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web ; dotnet run"
    
    # 等待应用启动
    Write-Host "等待应用启动..." -ForegroundColor Cyan
    Start-Sleep -Seconds 8
    
    # 运行测试
    Write-Host "运行自动化测试..." -ForegroundColor Cyan
    cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker
    .\auto-test.ps1
} else {
    Write-Host "编译失败!" -ForegroundColor Red
}
'@

$restartScriptPath = "C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\restart-and-test.ps1"
Set-Content -Path $restartScriptPath -Value $restartScript -Encoding UTF8
Write-Success "创建自动重启脚本: restart-and-test.ps1"

exit 0
