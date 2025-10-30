# Playwright 自动化：在本地使用 admin UI 创建 Casdoor 用户

目的：使用 Playwright 在本地自动登录 Casdoor 管理界面并创建缺失的 app/* 用户（例如 `app-built-in`）。

重要说明：
- 脚本在你的本机上运行，管理员凭据不会被发送给第三方服务。
- 脚本采用启发式选择器来定位表单元素；不同 Casdoor 版本或主题可能需调整选择器。

先决条件：
- Node.js >= 16 安装
- 在 PowerShell 中运行以下命令安装依赖并安装浏览器二进制：

```powershell
cd .\playwright
npm install
npx playwright install
```

如何运行：

1. 在环境变量中设置目标 URL 与管理员凭据（建议使用环境变量，不要在脚本中硬编码）：

```powershell
#$env:CASDOOR_URL = "https://sso.fzcsps.com"
#$env:ADMIN_USER = "admin"
#$env:ADMIN_PASS = "123"
# 运行脚本
node create_app_users.js
```

2. 脚本会在当前目录生成以下产物：
- `playwright-after-login.png`：登录后截图
- `playwright-created-<name>.png`：每个创建尝试的截图
- `trace.zip`：Playwright tracing（包含快照和网络活动）

常见问题与调整：
- 如果登录失败：打开 `playwright-login-failed.png`，查看页面中的输入字段名称，然后修改 `create_app_users.js` 中的 `userSelectors` / `passSelectors`。
- 如果找不到“Users”或“Add User”，请在浏览器中手动定位到创建用户的页面，并把对应的路径或选择器写入脚本（文件头部有注释）。

安全提示：
- 运行完成后请删除或安全保存生成的截图和 trace 文件，避免凭据信息泄露（trace 可能包含页面快照）。

后续：在本地执行完自动化并确认 app/* 用户创建成功后，请运行现有的同步脚本（`run-sync.ps1`）并使用 `scripts\parse_latest_log.ps1` 检查同步是否成功。
