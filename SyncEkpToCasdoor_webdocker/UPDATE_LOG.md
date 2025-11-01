# 系统更新日志 - 2025年10月31日

## 更新内容

### 1. ✅ 修改默认域名
- **新域名**: `syncas.fzcsps.com` (之前是 syn-ekp.fzcsps.com)
- **自动回调URL**: `http://syncas.fzcsps.com/callback`
- **配置位置**: `appsettings.Production.json` → `AppSettings.Domain`

### 2. ✅ 特权管理配置页面
创建了专门的特权配置界面: `/admin/settings`

**功能包括:**
- 🔐 修改特权管理员密码
- 🌐 配置系统域名和协议
- 👥 配置允许访问的用户列表
- 💾 所有配置统一保存

**访问权限**: 仅限特权登录的管理员（Administrator角色）

### 3. ✅ 特权密码修改功能
- 在 `/admin/settings` 页面可修改特权密码
- 需要输入当前密码验证
- 修改后自动保存到配置文件
- 下次登录使用新密码

### 4. ✅ 域名策略配置
在特权模式下可配置:
- **Domain**: 系统访问域名
- **Protocol**: http 或 https
- **自动生成**: RedirectUri = `{Protocol}://{Domain}/callback`

**重要提示**: 修改域名后需要在Casdoor应用配置中添加新的回调URL

### 5. ✅ Casdoor登出修复
- 修改了 `/logout` 逻辑
- 登出时同时清除本地会话和Casdoor会话
- 使用Casdoor标准登出端点: `{authority}/logout?redirect_uri={returnUrl}`

### 6. ✅ 用户访问控制
- 在 `/admin/settings` 配置允许访问的用户
- 支持用户名、邮箱、userId等多种匹配方式
- **留空表示允许所有用户**
- **特权登录不受此限制**

示例配置:
```
admin@example.com
user1
user2@company.com
```

### 7. ✅ 导航菜单更新
- 特权登录后，左侧导航栏显示"特权配置"入口
- 红色图标标识，醒目提示
- 仅管理员可见

## 当前登录方式

### SSO登录
- **URL**: http://syncas.fzcsps.com/login
- **认证**: 通过Casdoor SSO
- **限制**: 受用户访问控制配置影响

### 特权登录
- **URL**: http://syncas.fzcsps.com/admin-login
- **账号**: 任意用户名（建议使用 admin）
- **密码**: 默认 `sosy3080@sohu.com` (可在特权配置中修改)
- **特点**: 
  - 不受用户访问控制限制
  - 自动获得Administrator角色
  - 可访问特权配置页面

## 配置文件结构

```json
{
  "AppSettings": {
    "Domain": "syncas.fzcsps.com",
    "Protocol": "http",
    "AdminPassword": "sosy3080@sohu.com",
    "AllowedUsers": ""
  }
}
```

## 使用流程

### 首次配置
1. 使用特权登录: http://syncas.fzcsps.com/admin-login
   - 账号: admin
   - 密码: sosy3080@sohu.com

2. 进入特权配置: 左侧菜单 → "特权配置"

3. 配置系统:
   - 修改特权密码（可选）
   - 配置允许访问的用户列表
   - 确认域名设置正确

4. 保存配置

### 日常使用
- **管理员**: 使用特权登录，拥有完整权限
- **普通用户**: 使用SSO登录，需要在允许列表中

## 安全建议

1. ✅ **立即修改特权密码** - 在 `/admin/settings` 中修改
2. ⚠️ **配置HTTPS** - 生产环境应使用 HTTPS 协议
3. ✅ **限制访问用户** - 在特权配置中添加用户白名单
4. ⚠️ **定期更换密码** - 建议定期更换特权密码

## 技术实现

### 新增文件
- `Services/IConfigurationService.cs` - 配置服务接口
- `Services/ConfigurationService.cs` - 配置服务实现
- `Components/Pages/AdminSettings.razor` - 特权配置页面

### 修改文件
- `Program.cs` - 添加用户访问控制逻辑
- `Controllers/AuthController.cs` - 修复Casdoor登出
- `Components/Pages/AdminLogin.razor` - 增强错误提示
- `Components/Layout/NavMenu.razor` - 添加特权配置入口
- `appsettings.Production.json` - 更新配置结构

## 常见问题

### Q: 特权登录失败？
A: 检查错误提示，系统会显示当前配置的密码。默认密码是 `sosy3080@sohu.com`

### Q: 如何修改域名？
A: 
1. 特权登录
2. 进入 `/admin/settings`
3. 修改"系统域名"和"访问协议"
4. 保存配置
5. **重要**: 在Casdoor应用配置中添加新的回调URL

### Q: 如何限制用户访问？
A:
1. 特权登录
2. 进入 `/admin/settings`
3. 在"允许访问的用户列表"中添加用户（每行一个）
4. 保存配置
5. 未在列表中的用户无法通过SSO登录

### Q: 如何允许所有用户？
A: 在"允许访问的用户列表"中留空即可

## 部署信息

- **服务器**: 172.16.10.110
- **端口**: 9000
- **域名**: syncas.fzcsps.com (需要DNS解析)
- **容器**: syncekp-web
- **状态**: ✅ 运行中

## 下一步

建议后续优化:
1. 配置HTTPS证书
2. 添加审计日志
3. 实现配置备份/恢复
4. 添加多因素认证
