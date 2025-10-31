# OAuth登录验证测试文档

## 配置信息

### Casdoor OAuth配置
- **授权服务器**: http://sso.fzcsps.com
- **Client ID**: aecd00a352e5c560ffe6
- **Redirect URI**: http://syn-ekp.fzcsps.com:9000/callback
- **Scope**: read
- **限制**: 只允许 owner=built-in 的用户登录

### 应用地址
- **应用URL**: http://syn-ekp.fzcsps.com:9000

## 测试步骤

### 1. 访问应用
```
http://syn-ekp.fzcsps.com:9000
```
应该自动重定向到登录页面 `/login`

### 2. 点击登录按钮
点击"通过 Casdoor 登录"按钮，将重定向到：
```
http://sso.fzcsps.com/login/oauth/authorize?client_id=aecd00a352e5c560ffe6&response_type=code&redirect_uri=http://syn-ekp.fzcsps.com:9000/callback&scope=read&state=casdoor
```

### 3. 在Casdoor登录
使用 owner=built-in 的用户账号登录

### 4. 授权后重定向
Casdoor将重定向回：
```
http://syn-ekp.fzcsps.com:9000/callback?code=xxx&state=casdoor
```

### 5. 验证登录状态
- 应该看到用户名显示在顶部导航栏
- 有"登出"按钮
- 可以访问所有功能页面

### 6. 测试权限控制
使用非 built-in 组织的用户登录，应该被拒绝访问，显示"访问被拒绝"页面

## 已实现的功能

### ✅ OAuth 认证
- Casdoor OAuth 2.0 集成
- 授权码流程
- Token 管理

### ✅ 用户权限验证
- Owner 验证（只允许 built-in）
- 自动拒绝非授权用户
- 访问被拒绝页面

### ✅ 会话管理
- Cookie 认证
- 8小时会话过期
- 滑动过期（SlidingExpiration）

### ✅ 页面保护
- 所有页面需要认证
- 未认证自动重定向到登录页
- 主布局显示用户信息

### ✅ 登录/登出
- 登录页面 (`/login`)
- 登出功能 (`/logout`)
- OAuth 回调处理 (`/callback`)
- 访问拒绝页面 (`/access-denied`)

## 测试checklist

- [ ] 访问首页自动跳转到登录页
- [ ] 点击登录按钮跳转到 Casdoor
- [ ] 使用 built-in 用户登录成功
- [ ] 显示用户名和登出按钮
- [ ] 可以访问所有功能页面
- [ ] 点击登出清除会话
- [ ] 使用非 built-in 用户登录被拒绝
- [ ] 会话过期后重新登录

## 注意事项

1. **DNS配置**: 确保 `syn-ekp.fzcsps.com` 和 `sso.fzcsps.com` 可以正确解析
2. **Client Secret**: 已配置在 appsettings.json 中
3. **HTTPS**: 生产环境建议使用 HTTPS
4. **端口**: 应用运行在 9000 端口

## 配置文件

### appsettings.json
```json
"CasdoorAuth": {
  "Authority": "http://sso.fzcsps.com",
  "ClientId": "aecd00a352e5c560ffe6",
  "ClientSecret": "4402518b20dd191b8b48d6240bc786a4f847899a",
  "RedirectUri": "http://syn-ekp.fzcsps.com:9000/callback",
  "Scope": "read",
  "AllowedOwner": "built-in",
  "TokenEndpoint": "http://sso.fzcsps.com/api/login/oauth/access_token",
  "UserInfoEndpoint": "http://sso.fzcsps.com/api/userinfo"
}
```

## 故障排查

### 问题1: 重定向URI不匹配
检查 Casdoor 中配置的 redirect_uri 是否为 `http://syn-ekp.fzcsps.com:9000/callback`

### 问题2: Client ID/Secret 错误
验证 appsettings.json 中的配置是否正确

### 问题3: DNS解析失败
检查hosts文件或DNS配置

### 问题4: 用户被拒绝
确认用户的 owner 字段是否为 "built-in"
