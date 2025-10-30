# GitHub 首次同步指南

## 准备工作清单

### ✅ 已完成
- [x] 项目结构整理（docs/、scripts/、归档历史文件）
- [x] 统一命名（SyncEkpToCasdoor.sln）
- [x] .gitignore 配置完善
- [x] .gitattributes 行尾规范化
- [x] README 文档完善
- [x] 构建验证通过

### 🔍 推送前检查

#### 1. 敏感信息检查
```powershell
# 搜索可能的敏感信息
Get-ChildItem -Recurse -File | Select-String -Pattern "(password|secret|token|api[_-]?key)" -CaseSensitive:$false | Where-Object { $_.Path -notmatch "(node_modules|\.git|bin|obj)" }
```

**已保护的配置**：
- ✅ `sync_config.json` 已在 .gitignore
- ✅ `test_config.json` 已在 .gitignore
- ✅ `sync_state.json` 已在 .gitignore
- ✅ 环境变量示例使用占位符

#### 2. 大文件检查
```powershell
# 查找大于 5MB 的文件
Get-ChildItem -Recurse -File | Where-Object { $_.Length -gt 5MB -and $_.FullName -notmatch "(bin|obj|node_modules|\.git)" } | Select-Object FullName, @{Name="Size(MB)";Expression={[math]::Round($_.Length/1MB,2)}}
```

#### 3. 临时文件检查
```powershell
# 确认临时文件已忽略
ls logs/, bin/, obj/ -ErrorAction SilentlyContinue
```

---

## 首次推送命令

### 方式一：从现有本地仓库推送

```powershell
# 1. 确认当前在正确的分支
git branch

# 2. 查看未跟踪文件
git status

# 3. 添加所有文件
git add .

# 4. 提交
git commit -m "Initial commit: SyncEkpToCasdoor v1.2

- 控制台同步工具（.NET 8）
- WPF 图形界面
- 统一文档结构（docs/）
- 集中脚本管理（scripts/）
- 历史文件归档（docs/archive/）
"

# 5. 推送到远程仓库
git push -u origin main
```

### 方式二：重新初始化干净仓库

如果当前仓库历史混乱，建议重新初始化：

```powershell
# 1. 备份当前 .git（可选）
Move-Item .git .git.backup -Force

# 2. 初始化新仓库
git init
git branch -M main

# 3. 添加远程仓库
git remote add origin https://github.com/myzhangjing/ekp-casdoor-sync.git

# 4. 添加所有文件
git add .

# 5. 首次提交
git commit -m "Initial commit: SyncEkpToCasdoor v1.2

项目结构：
- SyncEkpToCasdoor/          控制台同步程序（.NET 8）
- SyncEkpToCasdoor/SyncEkpToCasdoor.UI/  WPF 图形界面
- docs/                       统一文档目录
  - ui/                       UI 使用文档
  - archive/                  历史文档与脚本归档
- scripts/                    运行脚本
- logs/                       日志输出（已忽略）

核心功能：
- EKP → Casdoor 组织与用户同步
- 增量/全量同步模式
- SQL 视图优化工具
- 数据查看与比对
- 实时同步监控

技术栈：
- .NET 8.0
- WPF (MVVM)
- SQL Server
- Casdoor REST API
"

# 6. 推送到远程
git push -u origin main --force
```

---

## 后续维护

### 分支策略
```powershell
# 开发新功能
git checkout -b feature/new-feature
# 完成后合并到 main
git checkout main
git merge feature/new-feature
git push origin main
```

### 标签发布
```powershell
# 创建版本标签
git tag -a v1.2.0 -m "Release v1.2.0

新增功能：
- 数据查看与比对模块
- WPF 界面优化
- 文档结构重组
"
git push origin v1.2.0
```

### 忽略已提交的敏感文件
如果不小心提交了敏感文件：

```powershell
# 从 Git 移除但保留本地文件
git rm --cached sync_config.json

# 提交更改
git commit -m "Remove sensitive config file"
git push origin main
```

---

## 推荐 .gitconfig 配置

```ini
[user]
    name = Your Name
    email = your.email@example.com

[core]
    autocrlf = true
    editor = code --wait

[alias]
    st = status
    co = checkout
    br = branch
    ci = commit
    lg = log --oneline --graph --decorate

[pull]
    rebase = false
```

---

## 检查清单

推送前最后确认：

- [ ] 所有敏感配置已在 .gitignore
- [ ] 构建成功（`dotnet build -c Release`）
- [ ] README.md 描述清晰完整
- [ ] LICENSE 文件存在
- [ ] .gitignore 和 .gitattributes 配置正确
- [ ] 没有大文件（>5MB）
- [ ] 提交信息清晰准确

---

## 远程仓库地址

```
https://github.com/myzhangjing/ekp-casdoor-sync.git
```

---

**准备就绪后，运行上面的推送命令即可！** 🚀
