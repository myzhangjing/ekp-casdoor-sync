# NOTE: 此脚本已迁移至仓库统一目录：scripts\run-sync-full.ps1
# 为避免破坏现有引用，这里保留一个跳板调用。
# 建议更新任务计划、CI/CD 或手动调用至新路径。

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot   = Resolve-Path (Join-Path $projectDir "..")
$target     = Join-Path $repoRoot "scripts\run-sync-full.ps1"

if (-not (Test-Path $target)) {
  Write-Error "未找到目标脚本: $target。请确保已迁移到 scripts/ 目录。"
  exit 1
}

& $target @args
