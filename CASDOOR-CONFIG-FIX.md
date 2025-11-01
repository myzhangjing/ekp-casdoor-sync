# ===============================================
# Casdoor 配置修复指南
# ===============================================

## 问题: Redirect URL 未配置

当前错误:
```
ERROR: You must specify at least one Redirect URL in 'Redirect URLs'
```

## 解决方案:

### 1. 登录 Casdoor 管理界面
访问: http://sso.fzcsps.com
或: http://172.16.10.110:8000

使用管理员账号登录 (admin)

### 2. 配置应用 Redirect URL

1. 进入 **Applications** (应用管理)
2. 找到 **SyncEkpToCasdoor** 应用 (Client ID: d339c32ea95dbf0f61cb)
3. 在 **Redirect URLs** 字段中添加:
   ```
   http://syn-ekp.fzcsps.com:9000/callback
   http://172.16.10.110:9000/callback
   http://localhost:9000/callback
   ```
4. 点击 **Save** 保存

### 3. 确认 Organization 配置

同时确认 admin 用户的 Organization 是 `built-in`:

1. 进入 **Users** (用户管理)
2. 找到 **admin** 用户
3. 确认 **Organization** 字段为: `built-in`
4. 如果不是,修改并保存

### 4. 测试登录

访问: http://172.16.10.110:9000/login

应该可以正常跳转到 Casdoor 登录页面

## 当前 Client ID 说明

- **旧的 Client ID**: aecd00a352e5c560ffe6 (之前配置的)
- **新的 Client ID**: d339c32ea95dbf0f61cb (当前使用的)

需要确认代码中使用的 Client ID 与 Casdoor 中配置的一致!

