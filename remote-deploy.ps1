# ==========================================
# 远程服务器部署脚本 (SSH)
# Remote Server Deployment via SSH
# ==========================================

param(
    [string]$ServerIP = "172.16.10.110",
    [string]$Username = "root",
    [string]$Password = "fzwater@163.com",
    [switch]$Deploy,      # 完整部署
    [switch]$Update,      # 仅更新代码
    [switch]$Restart,     # 重启服务
    [switch]$Status,      # 查看状态
    [switch]$Logs         # 查看日志
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  远程服务器部署工具" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "目标服务器: $ServerIP" -ForegroundColor Yellow
Write-Host "用户名: $Username" -ForegroundColor Yellow
Write-Host ""

# 检查plink是否存在
$plinkPath = "C:\Program Files\PuTTY\plink.exe"
$pscpPath = "C:\Program Files\PuTTY\pscp.exe"

if (-not (Test-Path $plinkPath)) {
    Write-Host "❌ 未找到PuTTY工具" -ForegroundColor Red
    Write-Host ""
    Write-Host "请选择SSH工具:" -ForegroundColor Yellow
    Write-Host "1. 使用Windows内置SSH (推荐)" -ForegroundColor Green
    Write-Host "2. 安装PuTTY" -ForegroundColor Cyan
    Write-Host "   下载地址: https://www.putty.org/" -ForegroundColor Gray
    Write-Host ""
    $choice = Read-Host "选择 (1/2)"
    
    if ($choice -eq "1") {
        $useWindowsSSH = $true
    } else {
        Write-Host "请安装PuTTY后重试" -ForegroundColor Red
        exit 1
    }
} else {
    $useWindowsSSH = $false
}

# 执行SSH命令函数 (使用Windows SSH)
function Invoke-SSHCommand {
    param([string]$Command)
    
    Write-Host "执行: $Command" -ForegroundColor Gray
    
    # 使用sshpass或直接SSH
    $sshCmd = "ssh -o StrictHostKeyChecking=no ${Username}@${ServerIP} `"$Command`""
    
    # 创建临时密码文件
    $tempPass = [System.IO.Path]::GetTempFileName()
    $Password | Out-File -FilePath $tempPass -Encoding ASCII
    
    try {
        # 尝试使用plink (如果有PuTTY)
        if (Test-Path $plinkPath) {
            & $plinkPath -ssh ${Username}@${ServerIP} -pw $Password -batch $Command
        } else {
            # 使用Windows SSH (需要手动输入密码)
            Write-Host "提示: 需要输入密码 $Password" -ForegroundColor Yellow
            ssh -o StrictHostKeyChecking=no ${Username}@${ServerIP} $Command
        }
    } finally {
        if (Test-Path $tempPass) {
            Remove-Item $tempPass -Force
        }
    }
}

# 上传文件函数
function Copy-ToServer {
    param(
        [string]$LocalPath,
        [string]$RemotePath
    )
    
    Write-Host "上传: $LocalPath -> ${ServerIP}:${RemotePath}" -ForegroundColor Cyan
    
    if (Test-Path $pscpPath) {
        & $pscpPath -pw $Password -r $LocalPath ${Username}@${ServerIP}:${RemotePath}
    } else {
        Write-Host "提示: 需要输入密码 $Password" -ForegroundColor Yellow
        scp -r $LocalPath ${Username}@${ServerIP}:${RemotePath}
    }
}

# 查看状态
if ($Status) {
    Write-Host "`n[查看服务状态]" -ForegroundColor Yellow
    Invoke-SSHCommand "systemctl status ekp-casdoor-sync --no-pager"
    exit 0
}

# 查看日志
if ($Logs) {
    Write-Host "`n[查看日志]" -ForegroundColor Yellow
    Invoke-SSHCommand "journalctl -u ekp-casdoor-sync -n 50 --no-pager"
    exit 0
}

# 重启服务
if ($Restart) {
    Write-Host "`n[重启服务]" -ForegroundColor Yellow
    Invoke-SSHCommand "systemctl restart ekp-casdoor-sync"
    Start-Sleep -Seconds 3
    Invoke-SSHCommand "systemctl status ekp-casdoor-sync --no-pager"
    exit 0
}

# 更新代码
if ($Update) {
    Write-Host "`n[更新代码]" -ForegroundColor Yellow
    
    Write-Host "1. 备份配置..." -ForegroundColor Cyan
    Invoke-SSHCommand "cp ~/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/appsettings.json /tmp/appsettings.json.backup"
    
    Write-Host "2. 拉取最新代码..." -ForegroundColor Cyan
    Invoke-SSHCommand "cd ~/ekp-casdoor-sync && git pull origin web-docker"
    
    Write-Host "3. 恢复配置..." -ForegroundColor Cyan
    Invoke-SSHCommand "cp /tmp/appsettings.json.backup ~/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/appsettings.json"
    
    Write-Host "4. 重新编译..." -ForegroundColor Cyan
    Invoke-SSHCommand "cd ~/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web && dotnet publish -c Release -o bin/Release/net8.0/publish"
    
    Write-Host "5. 重启服务..." -ForegroundColor Cyan
    Invoke-SSHCommand "systemctl restart ekp-casdoor-sync"
    
    Write-Host "`n✅ 更新完成!" -ForegroundColor Green
    exit 0
}

# 完整部署
if ($Deploy) {
    Write-Host "`n[开始完整部署]" -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "===== 使用一键部署脚本 =====" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "步骤1: 下载部署脚本到服务器" -ForegroundColor Yellow
    $deployScript = @'
#!/bin/bash
# 自动部署脚本
set -e

echo "========================================"
echo "  EKP-Casdoor-Sync 自动部署"
echo "========================================"
echo ""

# 检查Git
if ! command -v git &> /dev/null; then
    echo "安装Git..."
    apt-get update
    apt-get install -y git
fi

# 检查.NET
if ! command -v dotnet &> /dev/null; then
    echo "安装.NET 8.0 SDK..."
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update
    apt-get install -y dotnet-sdk-8.0
fi

# 克隆或更新代码
if [ -d "$HOME/ekp-casdoor-sync" ]; then
    echo "更新代码..."
    cd $HOME/ekp-casdoor-sync
    git pull origin web-docker
else
    echo "克隆仓库..."
    git clone -b web-docker https://github.com/myzhangjing/ekp-casdoor-sync.git $HOME/ekp-casdoor-sync
    cd $HOME/ekp-casdoor-sync
fi

# 编译发布
echo "编译应用..."
cd SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web
dotnet publish -c Release -o bin/Release/net8.0/publish

# 创建systemd服务
echo "配置系统服务..."
cat > /etc/systemd/system/ekp-casdoor-sync.service << 'EOF'
[Unit]
Description=EKP to Casdoor Sync Web Application
After=network.target

[Service]
Type=notify
WorkingDirectory=/root/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/bin/Release/net8.0/publish
ExecStart=/usr/bin/dotnet /root/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/bin/Release/net8.0/publish/SyncEkpToCasdoor.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=ekp-casdoor-sync
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://0.0.0.0:5233

[Install]
WantedBy=multi-user.target
EOF

# 重新加载并启动
systemctl daemon-reload
systemctl enable ekp-casdoor-sync
systemctl restart ekp-casdoor-sync

echo ""
echo "========================================"
echo "  部署完成!"
echo "========================================"
echo ""
echo "访问地址: http://172.16.10.110:5233/login"
echo ""
echo "管理命令:"
echo "  systemctl status ekp-casdoor-sync"
echo "  systemctl restart ekp-casdoor-sync"
echo "  journalctl -u ekp-casdoor-sync -f"
echo ""
'@
    
    # 保存部署脚本到临时文件
    $tempScript = [System.IO.Path]::GetTempFileName() + ".sh"
    $deployScript | Out-File -FilePath $tempScript -Encoding UTF8
    
    Write-Host "步骤2: 上传部署脚本" -ForegroundColor Yellow
    Copy-ToServer -LocalPath $tempScript -RemotePath "/tmp/deploy.sh"
    
    Write-Host "步骤3: 执行部署" -ForegroundColor Yellow
    Invoke-SSHCommand "chmod +x /tmp/deploy.sh && /tmp/deploy.sh"
    
    Write-Host "`n✅ 部署完成!" -ForegroundColor Green
    Write-Host ""
    Write-Host "访问地址: http://172.16.10.110:5233/login" -ForegroundColor Cyan
    
    # 清理临时文件
    Remove-Item $tempScript -Force
    
    exit 0
}

# 默认显示帮助
Write-Host "使用方法:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  .\remote-deploy.ps1 -Deploy     # 完整部署" -ForegroundColor White
Write-Host "  .\remote-deploy.ps1 -Update     # 更新代码" -ForegroundColor White
Write-Host "  .\remote-deploy.ps1 -Restart    # 重启服务" -ForegroundColor White
Write-Host "  .\remote-deploy.ps1 -Status     # 查看状态" -ForegroundColor White
Write-Host "  .\remote-deploy.ps1 -Logs       # 查看日志" -ForegroundColor White
Write-Host ""
Write-Host "服务器信息:" -ForegroundColor Yellow
Write-Host "  IP: $ServerIP" -ForegroundColor Gray
Write-Host "  用户: $Username" -ForegroundColor Gray
Write-Host ""
