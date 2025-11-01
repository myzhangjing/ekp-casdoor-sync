# ekp-casdoor-sync 文档总览

本仓库包含 EKP → Casdoor 同步工具与 WPF 图形界面。为便于维护与查阅，文档与脚本已归档至统一目录：

- docs/
  - 本文件：文档导航与结构说明
  - ui/：WPF 界面相关文档（用户指南、模块总结、更新日志、测试计划等）
  - monitoring/：定时同步与监控相关文档（配置、检查、故障排查等）
- scripts/
  - 存放用于构建、运行、同步的脚本（PowerShell 等）
  - check-sync-status.ps1：监控同步状态脚本
- SyncEkpToCasdoor/
  - 控制台同步程序（.NET 8）
- SyncEkpToCasdoor/SyncEkpToCasdoor.UI/
  - WPF 图形界面（.NET 8, Windows）

## 快速导航

### WPF UI 文档（docs/ui/）
- README_WPF_UI.md：界面概览与快速开始
- 执行同步使用说明.md：执行同步功能详解
- 数据查看使用说明.md：数据查看与比对模块说明
- 数据查看模块总结.md：技术实现与设计说明
- 更新日志_v1.1.md：功能更新说明
- 测试计划.md：测试清单与记录模板
- BUGFIX.md：问题修复记录

### 定时同步与监控（docs/monitoring/）
- **如何检查定时同步是否执行.md**：⭐ 快速参考指南（推荐日常使用）
- **定时同步监控指南.md**：完整监控方案与故障排查
- **修复说明-两次操作问题.md**：历史问题修复说明

## 约定与规范

- 代码留在各自项目目录内（控制台/界面）；文档放在 docs/，脚本放在 scripts/
- 命名采用一致的大小写与连字符风格；中文文档可保留中文文件名
- 解决方案统一包含 Console 与 UI 两个项目，便于一键构建

## 构建与运行（摘要）

- 控制台：在解决方案目录运行 dotnet build；可设置环境变量后直接运行 exe
- WPF UI：在解决方案目录运行 dotnet build；或在 IDE 中设为启动项目

更多详细用法请查看各子目录 README 或 UI 文档。
