# 系统配置说明

## 配置文件位置

- **开发环境**: `appsettings.json`
- **生产环境**: `appsettings.Production.json`

## 核心配置项

### 1. 应用设置 (AppSettings)

```json
"AppSettings": {
  "Domain": "syn-ekp.fzcsps.com",      // 应用访问域名
  "Protocol": "http",                   // 协议: http 或 https
  "AdminPassword": "sosy3080@sohu.com"  // 特权管理员密码
}
```

**说明:**
- `Domain`: 系统的访问域名,用于构建OAuth回调URL
- `Protocol`: 访问协议,如果使用HTTPS请改为 "https"
- `AdminPassword`: 特权登录的密码,可修改为任意值

### 2. OAuth认证配置 (CasdoorAuth)

```json
"CasdoorAuth": {
  "Authority": "http://sso.fzcsps.com",
  "ClientId": "aecd00a352e5c560ffe6",
  "ClientSecret": "4402518b20dd191b8b48d6240bc786a4f847899a",
  "RedirectUri": "",                     // 留空将自动使用 {Protocol}://{Domain}/callback
  "Scope": "read",
  "AllowedOwner": "fzswjtOrganization",
  "AuthorizationEndpoint": "http://sso.fzcsps.com/login/oauth/authorize",
  "TokenEndpoint": "http://sso.fzcsps.com/api/login/oauth/access_token",
  "UserInfoEndpoint": "http://sso.fzcsps.com/api/userinfo"
}
```

**说明:**
- `RedirectUri`: 可以留空,系统会自动根据 `AppSettings` 中的 `Domain` 和 `Protocol` 构建
- 如果需要覆盖自动构建的值,可以手动设置完整的回调URL

### 3. 数据库连接 (EkpConnection)

```json
"EkpConnection": "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;Encrypt=False;"
```

### 4. 主机访问限制 (AllowedHosts)

```json
"AllowedHosts": "*"  // "*" 表示允许所有域名访问
```

## 登录方式

### 1. SSO登录
- **URL**: http://syn-ekp.fzcsps.com/login
- **方式**: 通过Casdoor SSO认证
- **用户**: 需要在Casdoor中注册

### 2. 特权管理员登录
- **URL**: http://syn-ekp.fzcsps.com/admin-login
- **账号**: 任意用户名 (例如: admin)
- **密码**: 配置文件中 `AppSettings.AdminPassword` 的值 (默认: sosy3080@sohu.com)
- **特点**: 绕过SSO认证,直接登录

## 修改域名

如需修改访问域名,只需修改配置文件中的 `AppSettings` 部分:

```json
"AppSettings": {
  "Domain": "your-new-domain.com",    // 改为新域名
  "Protocol": "http",                  // 如使用HTTPS改为 "https"
  "AdminPassword": "your-password"     // 可选:修改管理员密码
}
```

**注意事项:**
1. 修改后需要重启应用
2. 确保新域名正确解析到服务器IP
3. 需要在Casdoor应用配置中添加新的回调URL: `{Protocol}://{Domain}/callback`

## 部署环境变量

Docker部署时会自动使用 `appsettings.Production.json` 配置。

如需使用其他配置文件,可以设置环境变量:
```bash
ASPNETCORE_ENVIRONMENT=Development  # 使用 appsettings.Development.json
ASPNETCORE_ENVIRONMENT=Production   # 使用 appsettings.Production.json (默认)
```

## 安全建议

1. **生产环境务必修改管理员密码**
2. **使用HTTPS协议**
3. **限制 AllowedHosts 为特定域名**
4. **定期更换 ClientSecret**
