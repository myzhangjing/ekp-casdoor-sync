# MD5 密码同步功能更新日志

## 版本 1.1.0 - 2025-01-XX

### 新增功能

#### 1. MD5 密码字段同步

**问题背景：**
- EKP 系统中用户密码以 MD5 格式存储在 `sys_org_person.fd_password` 字段
- 原同步程序未包含密码字段，导致 Casdoor 中用户无法使用原始密码登录

**解决方案：**

1. **数据模型更新** (`Program.cs`)
   ```csharp
   internal record EkpUser(
       // ... 其他字段
       string? PasswordMd5  // 新增：MD5密码哈希
   );
   ```

2. **EKP 视图优化** (`vw_casdoor_users_sync`)
   - 在 `person` CTE 中添加 `p.fd_password AS PasswordMd5`
   - 在最终 SELECT 中添加 `p.PasswordMd5 AS password_md5` 字段
   - 使用命令更新视图：`.\SyncEkpToCasdoor.exe --update-user-view`

3. **数据读取更新** (`Program.cs` - `GetUsers` 方法)
   ```sql
   SELECT id, username, display_name, email, phone, created_at, updated_at, 
          gender, language, dept_id, company_name, affiliation, owner, type, 
          password_md5  -- 新增字段
   FROM vw_casdoor_users_sync
   ```

4. **API 同步更新** (`SimpleCasdoorRepository.cs` - `UpsertUser` 方法)
   ```csharp
   var userData = new 
   { 
       // ... 其他字段
       password = u.PasswordMd5  // 发送 MD5 密码到 Casdoor
   };
   ```

### 技术细节

**MD5 密码格式：**
- 长度：32 字符
- 格式：十六进制小写字符串
- 示例：`eb5ea7f5b326609a4b0f8556b6548e8d`

**视图更新命令：**
```powershell
# 更新视图以包含密码字段
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --update-user-view

# 然后执行正常同步
.\bin\Release\net8.0\SyncEkpToCasdoor.exe
```

**影响范围：**
- ✅ 新用户创建时会包含 MD5 密码
- ✅ 现有用户更新时会覆盖密码字段
- ✅ 用户可以使用 EKP 原始密码登录 Casdoor

### 代码变更文件

```
SyncEkpToCasdoor/
├── Program.cs                          # 新增 PasswordMd5 字段到 EkpUser 记录
│   ├── UpdateUserViewWithPassword()    # 新增：视图更新方法
│   └── GetUsers()                      # 更新：添加 password_md5 字段读取
│
├── SimpleCasdoorRepository.cs          # 更新：UpsertUser 方法添加 password 字段
│
└── sql/
    └── ALTER_VIEW_ADD_PASSWORD.sql     # 新增：视图更新 SQL 脚本（备份）
```

### 使用说明

**首次使用：**
1. 编译项目：`dotnet build -c Release`
2. 更新视图：`.\bin\Release\net8.0\SyncEkpToCasdoor.exe --update-user-view`
3. 执行同步：`.\bin\Release\net8.0\SyncEkpToCasdoor.exe`

**后续同步：**
- 直接运行：`.\bin\Release\net8.0\SyncEkpToCasdoor.exe`
- 视图已更新，无需重复执行 `--update-user-view`

### 验证方法

1. **检查日志输出：**
   ```powershell
   Get-Content .\logs\sync_*.log -Tail 100 | Select-String "用户已创建|用户已更新"
   ```

2. **Casdoor 管理界面验证：**
   - 登录 Casdoor：http://172.16.10.110:8000
   - 进入"用户管理"
   - 随机选择一个同步的用户，检查是否有密码值

3. **数据库验证（可选）：**
   ```sql
   -- 检查视图中密码字段
   SELECT TOP 5 id, username, password_md5, LEN(password_md5) AS pwd_length
   FROM vw_casdoor_users_sync
   WHERE password_md5 IS NOT NULL;
   ```

### 注意事项

⚠️ **安全提示：**
- MD5 是弱哈希算法，不建议用于生产环境密码存储
- 建议 Casdoor 配置强制首次登录修改密码策略
- 考虑后续升级到 bcrypt/argon2 等更安全的哈希算法

⚠️ **兼容性：**
- Casdoor API 版本：v2.109.0 及以上
- EKP 数据库：SQL Server（已测试 npm.fzcsps.com,11433）

### 后续优化建议

1. **密码加密传输：**
   - 当前 password 字段明文传输（HTTPS 保护）
   - 可考虑二次加密或使用安全传输协议

2. **密码策略配置：**
   - 在 Casdoor 中配置密码复杂度要求
   - 启用多因素认证 (MFA)

3. **同步状态监控：**
   - 添加密码字段同步成功/失败统计
   - 记录密码字段为空的用户数量

### 相关文档

- [EKP 数据库字段分析](./docs/EKP_DATABASE_SCHEMA.md)
- [功能改进计划](./docs/IMPROVEMENT_PLAN.md)
- [Casdoor API 文档](https://casdoor.org/docs/basic/server-installation/)

---

**更新日期：** 2025-01-XX  
**作者：** GitHub Copilot  
**审核：** 待审核
