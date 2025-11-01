# OAuth2 登录集成方案详解

## 📋 方案概述

本文档详细说明了 EKP-Casdoor 同步工具的 OAuth2 登录实现方案，提供了三种回调机制以适应不同的使用场景。

---

## 🎯 支持的三种回调方式

### 方式1：自定义 URI Scheme（推荐 ⭐⭐⭐⭐⭐）

**原理**：注册 `ekpsync://` 协议，让浏览器在授权后自动唤起应用

**优点**：
- ✅ 最佳用户体验，完全自动化
- ✅ 不需要HTTP服务器
- ✅ 不需要管理员权限（注册到 HKCU）
- ✅ 不受防火墙影响
- ✅ 符合OAuth2最佳实践

**工作流程**：
```
1. 应用启动时自动注册 ekpsync:// 协议
   ↓
2. 用户点击"使用Casdoor登录"
   ↓
3. 浏览器打开授权页面，redirect_uri=ekpsync://callback
   ↓
4. 用户在浏览器完成登录授权
   ↓
5. Casdoor 重定向到 ekpsync://callback?code=xxx&state=yyy
   ↓
6. Windows 自动启动应用（如果未运行）或激活现有实例
   ↓
7. 应用解析 URI 参数，提取授权码
   ↓
8. 自动完成令牌交换，用户无需任何手动操作
```

**Casdoor 配置**：
```
Application 设置:
- Redirect URIs: ekpsync://callback
```

**实现文件**：
- `Services/UriSchemeRegistrar.cs` - URI Scheme 注册与解析
- `App.xaml.cs` - 启动参数处理
- `ViewModels/LoginViewModel.cs` - OAuth2 流程

---

### 方式2：localhost + HttpListener（备用方案）

**原理**：在本地启动HTTP监听器，接收浏览器回调

**优点**：
- ✅ 适用于无法注册URI Scheme的情况
- ✅ 用户体验较好，自动完成

**限制**：
- ⚠️ 可能需要管理员权限（取决于端口和系统配置）
- ⚠️ 防火墙可能阻止
- ⚠️ 端口 9000 可能被占用

**工作流程**：
```
1. 应用启动 HttpListener 监听 http://localhost:9000/callback
   ↓
2. 浏览器授权后重定向到 localhost:9000/callback?code=xxx
   ↓
3. HttpListener 接收请求，解析授权码
   ↓
4. 返回成功页面给浏览器
   ↓
5. 应用完成令牌交换
```

**Casdoor 配置**：
```
Application 设置:
- Redirect URIs: http://localhost:9000/callback
```

---

### 方式3：手动输入授权码（兜底方案）

**原理**：用户手动复制粘贴授权码

**优点**：
- ✅ 100% 兼容性
- ✅ 不依赖任何系统功能
- ✅ 适合受限环境

**工作流程**：
```
1. 用户点击"手动输入授权码"
   ↓
2. 应用显示授权URL
   ↓
3. 用户手动访问该URL，完成授权
   ↓
4. 复制回调URL中的 code 参数值
   ↓
5. 粘贴到应用的"授权码"输入框
   ↓
6. 点击"使用授权码登录"
```

---

## 🔧 技术实现细节

### URI Scheme 注册

**注册位置**：`HKEY_CURRENT_USER\SOFTWARE\Classes\ekpsync`

**注册内容**：
```registry
[HKEY_CURRENT_USER\SOFTWARE\Classes\ekpsync]
@="URL:EKP-Casdoor Sync Tool"
"URL Protocol"=""

[HKEY_CURRENT_USER\SOFTWARE\Classes\ekpsync\DefaultIcon]
@="C:\Path\To\SyncEkpToCasdoor.UI.exe,0"

[HKEY_CURRENT_USER\SOFTWARE\Classes\ekpsync\shell\open\command]
@="\"C:\Path\To\SyncEkpToCasdoor.UI.exe\" \"%1\""
```

**代码实现**：
```csharp
// 注册 URI Scheme
UriSchemeRegistrar.RegisterUriScheme();

// 检查是否已注册
bool isRegistered = UriSchemeRegistrar.IsUriSchemeRegistered();

// 解析回调 URI
if (UriSchemeRegistrar.TryParseCallbackUri(uri, out var code, out var state, out var error))
{
    // 处理授权码
}
```

### 启动参数处理

**App.xaml.cs**：
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // 检查是否通过 URI Scheme 启动
    if (e.Args.Length > 0 && e.Args[0].StartsWith("ekpsync://"))
    {
        HandleUriSchemeCallback(e.Args[0]);
        return;
    }
    
    // 正常启动流程
    ShowLoginWindow();
}
```

### OAuth2 授权流程

**LoginViewModel.cs**：
```csharp
// 1. 构造授权 URL
var authUrl = $"{endpoint}/login/oauth/authorize"
    + $"?client_id={clientId}"
    + $"&response_type=code"
    + $"&redirect_uri={redirectUri}"  // ekpsync://callback 或 http://localhost:9000/callback
    + $"&scope=read"
    + $"&state=casdoor";

// 2. 打开浏览器
Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

// 3. 接收授权码（三种方式之一）
// 4. 交换访问令牌
var tokenUrl = $"{endpoint}/api/login/oauth/access_token";
var requestData = new
{
    grant_type = "authorization_code",
    client_id = clientId,
    client_secret = clientSecret,
    code = authorizationCode,
    redirect_uri = redirectUri
};
```

---

## 📝 Casdoor 配置指南

### 步骤1：创建或编辑 Application

登录 Casdoor 管理后台，进入 Applications 页面。

### 步骤2：配置 Redirect URIs

在 Application 配置中添加以下重定向地址（支持多个）：

```
ekpsync://callback
http://localhost:9000/callback
```

**截图示例**：
```
Redirect URIs:
┌──────────────────────────────────────┐
│ ekpsync://callback                   │ ← 优先使用
│ http://localhost:9000/callback       │ ← 备用
└──────────────────────────────────────┘
```

### 步骤3：启用 Authorization Code Grant

确保在 Grant Types 中勾选 **Authorization Code**：

```
Grant Types:
☑ Authorization Code  ← 必须启用
☐ Implicit
☐ Password
☐ Client Credentials
```

### 步骤4：配置 Scopes

根据需要配置权限范围：
```
Available Scopes:
☑ read
☐ profile
☐ email
```

### 步骤5：记录配置信息

保存以下信息到环境变量或配置文件：
```
CASDOOR_ENDPOINT=http://sso.fzcsps.com
CASDOOR_CLIENT_ID=aecd00a352e5c560ffe6
CASDOOR_CLIENT_SECRET=<your-secret>
```

---

## 🚀 使用指南

### 首次运行

1. **启动应用**：
   ```powershell
   .\SyncEkpToCasdoor.UI.exe
   ```

2. **自动注册 URI Scheme**：
   - 应用首次启动时自动注册 `ekpsync://` 协议
   - 无需管理员权限
   - 注册到用户级别注册表

3. **显示登录窗口**：
   - 点击"使用 Casdoor 登录"按钮
   - 浏览器自动打开授权页面

4. **完成授权**：
   - 在浏览器中输入用户名密码
   - 点击"授权"按钮
   - **自动返回应用**（无需任何手动操作）

5. **登录成功**：
   - 应用自动关闭登录窗口
   - 进入主界面

### 方式选择逻辑

应用会自动按以下优先级选择回调方式：

```
1. 检查 URI Scheme 是否已注册
   ├─ 是 → 使用 ekpsync://callback
   └─ 否 → 尝试注册
       ├─ 成功 → 使用 ekpsync://callback
       └─ 失败 → 继续下一步

2. 检查是否提供了 redirectUri 参数
   ├─ 是 → 使用提供的地址
   └─ 否 → 使用默认 http://localhost:9000/callback

3. 如果使用 localhost，尝试启动 HttpListener
   ├─ 成功 → 自动接收回调
   └─ 失败 → 显示"手动输入授权码"选项
```

### 故障排查

#### 问题1：浏览器授权后没有自动返回应用

**可能原因**：
- URI Scheme 注册失败
- 浏览器安全设置阻止了协议调用

**解决方案**：
1. 点击"手动输入授权码"
2. 复制浏览器地址栏中的 `code` 参数
3. 粘贴到应用并点击登录

#### 问题2：HttpListener 启动失败

**可能原因**：
- 端口 9000 被占用
- 防火墙阻止
- 需要管理员权限

**解决方案**：
- 使用自定义 URI Scheme（推荐）
- 或使用手动输入方式

#### 问题3：Casdoor 返回 "redirect_uri_mismatch" 错误

**原因**：回调地址未在 Casdoor 中配置

**解决方案**：
在 Casdoor Application 配置中添加对应的 Redirect URI

---

## 🔒 安全考虑

### 1. Client Secret 保护

```csharp
// ❌ 不要硬编码
var clientSecret = "abc123secret";

// ✅ 使用环境变量
var clientSecret = Environment.GetEnvironmentVariable("CASDOOR_CLIENT_SECRET");

// ✅ 或使用配置文件（加密存储）
var clientSecret = Configuration["Casdoor:ClientSecret"];
```

### 2. State 参数验证

```csharp
// 生成随机 state
var state = Guid.NewGuid().ToString();

// 验证返回的 state
if (returnedState != expectedState)
{
    throw new SecurityException("State mismatch");
}
```

### 3. PKCE 支持（可选增强）

**Casdoor 支持 PKCE**，可进一步提升安全性：

```csharp
// 生成 code_verifier
var codeVerifier = GenerateCodeVerifier();

// 生成 code_challenge
var codeChallenge = GenerateCodeChallenge(codeVerifier);

// 授权请求添加 PKCE 参数
var authUrl = $"{endpoint}/login/oauth/authorize"
    + $"&code_challenge={codeChallenge}"
    + $"&code_challenge_method=S256";

// 令牌请求附加 code_verifier
var tokenRequest = new
{
    code_verifier = codeVerifier,
    // ... 其他参数
};
```

---

## 📊 方案对比表

| 特性 | URI Scheme | HttpListener | 手动输入 |
|------|------------|--------------|---------|
| **用户体验** | ⭐⭐⭐⭐⭐ 完全自动 | ⭐⭐⭐⭐ 较好 | ⭐⭐ 需要手动 |
| **兼容性** | ⭐⭐⭐⭐ 高 | ⭐⭐⭐ 中等 | ⭐⭐⭐⭐⭐ 100% |
| **安全性** | ⭐⭐⭐⭐⭐ 最佳 | ⭐⭐⭐⭐ 良好 | ⭐⭐⭐ 一般 |
| **权限要求** | ⭐⭐⭐⭐⭐ 无 | ⭐⭐ 可能需要 | ⭐⭐⭐⭐⭐ 无 |
| **防火墙影响** | ⭐⭐⭐⭐⭐ 无 | ⭐⭐ 可能受影响 | ⭐⭐⭐⭐⭐ 无 |
| **实现复杂度** | ⭐⭐⭐ 中等 | ⭐⭐⭐⭐ 较高 | ⭐⭐⭐⭐⭐ 简单 |
| **推荐指数** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |

---

## 🔗 参考资料

- [Casdoor OAuth2 文档](https://casdoor.org/docs/how-to-connect/oauth)
- [OAuth 2.0 RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749)
- [PKCE RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)
- [Microsoft - 注册 URI Scheme](https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/aa767914(v=vs.85))

---

## 📞 获取帮助

如果遇到问题：
1. 查看控制台输出（Debug模式）
2. 检查 `logs/error_YYYYMMDD.log` 日志文件
3. 提交 [GitHub Issue](https://github.com/myzhangjing/ekp-casdoor-sync/issues)

---

**更新日期**：2025-10-31  
**版本**：v1.3.0
