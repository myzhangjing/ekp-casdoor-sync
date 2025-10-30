# 贡献指南

感谢你愿意为 EKP-Casdoor Sync 做出贡献！

## 代码规范
- C# 目标框架：.NET 8
- 保持注释为中文，便于本地团队协作
- 提交前请确保可以构建并通过基本自查

## 分支与提交
- 使用 `main` 作为稳定分支
- 功能开发：`feat/<topic>`，修复：`fix/<topic>`
- 提交信息：`type(scope): summary`，例如 `feat(sync): support parentId fallback`

## 构建与运行
- 安装 .NET 8 SDK
- 构建：`dotnet build -c Release`
- 运行：参考 `SyncEkpToCasdoor/README.md` 的环境变量说明

## 安全与隐私
- 不要在仓库提交任何密钥（client secret、数据库密码等）
- 所有敏感信息请通过环境变量或 CI/CD Secret 管理

## 问题反馈
- 提交 issue 时请附：版本、复现步骤、预期行为与实际行为、相关日志片段（脱敏）
