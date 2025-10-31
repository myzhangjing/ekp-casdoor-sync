# 自动化测试文档

## 测试脚本说明

已创建全自动化测试脚本,用于测试系统的各项功能:

### 测试项目

1. **基础连接测试**
   - 应用启动检查
   - Blazor 框架加载
   - 静态资源访问

2. **页面响应时间测试**
   - 登录页加载速度
   - 首页访问控制
   - OAuth Challenge 端点
   - 性能基准测试

3. **配置检查**
   - OAuth 配置验证
   - ClientId: cb838421e04ecd30f72b
   - AllowedOwner: built-in
   - Scope: read

4. **定时任务测试**
   - 配置文件检查
   - 定时同步服务状态
   - 任务执行日志

5. **数据库连接测试**
   - EKP 数据库服务器连通性
   - 连接字符串配置验证

6. **安全性检查**
   - 未授权访问控制
   - 敏感配置检查

7. **组件完整性检查**
   - 关键文件存在性验证
   - 服务注册检查

## 使用方法

### 1. 启动应用

```powershell
cd c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
dotnet run
```

### 2. 运行测试(在新终端)

```powershell
cd c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1
```

### 3. 测试定时任务

#### 启用定时任务
编辑 `appsettings.json`:

```json
"ScheduledSync": {
  "Enabled": true,
  "IntervalSeconds": 10
}
```

重启应用后,应该看到日志输出:
```
info: SyncEkpToCasdoor.Web.Services.ScheduledSyncService[0]
      定时同步服务启动
info: SyncEkpToCasdoor.Web.Services.ScheduledSyncService[0]
      定时同步已启用，间隔: 10 秒
```

每 10 秒会看到:
```
info: SyncEkpToCasdoor.Web.Services.ScheduledSyncService[0]
      定时同步任务开始执行 - 2025/10/31 ...
info: SyncEkpToCasdoor.Web.Services.ScheduledSyncService[0]
      准备同步 2 个公司
```

## 测试结果

测试脚本会生成报告文件: `test-report-yyyyMMdd-HHmmss.txt`

### 成功标准

- **成功率 ≥ 80%**: 系统运行良好 ✓
- **成功率 60-80%**: 需要优化 ⚠
- **成功率 < 60%**: 存在严重问题 ✗

### 性能标准

- **响应时间 < 500ms**: 优秀 ✓
- **响应时间 500-1000ms**: 良好 ○
- **响应时间 > 1000ms**: 需要优化 ⚠

## 已实现的功能

### ✅ 认证系统
- Casdoor OAuth 2.0 集成
- 组织验证 (仅 built-in)
- Cookie 会话管理
- 登录/登出功能

### ✅ 定时同步服务
- 后台服务自动启动
- 可配置同步间隔
- 按公司批量同步
- 详细日志记录

### ✅ 数据同步
- EKP 到 Casdoor 用户同步
- 组织结构同步
- 增量/全量同步支持
- 同步状态跟踪

### ✅ 日志系统
- 文件日志记录
- 内存日志收集
- 分级日志输出

## 性能优化建议

如测试发现性能问题,可采用以下优化:

1. **启用响应缓存**
   ```csharp
   app.UseResponseCaching();
   ```

2. **优化数据库查询**
   - 添加索引
   - 使用查询优化
   - 批量处理

3. **CDN 加速**
   - 静态资源使用 CDN
   - 启用文件压缩

4. **Blazor 优化**
   - 启用预渲染
   - 组件缓存
   - 延迟加载

## 故障排除

### 问题1: 应用无法启动

**检查**:
- 端口 5233 是否被占用
- 配置文件是否正确
- 依赖项是否完整

**解决**: 
```powershell
netstat -ano | findstr :5233
dotnet restore
dotnet build
```

### 问题2: 登录失败

**检查**:
- Casdoor 服务是否可访问
- ClientId/ClientSecret 是否正确
- 用户是否属于 built-in 组织

**查看日志**:
```powershell
Get-Content .\logs\*.log | Select-Object -Last 50
```

### 问题3: 定时任务不执行

**检查**:
- appsettings.json 中 `ScheduledSync.Enabled` 是否为 true
- TargetCompanyIds 是否配置
- 查看应用启动日志

### 问题4: 数据库连接失败

**检查**:
- 数据库服务器是否可访问
- 连接字符串是否正确
- 防火墙设置

**测试连接**:
```powershell
Test-NetConnection -ComputerName npm.fzcsps.com -Port 11433
```

## 持续集成

建议设置定期自动化测试:

```powershell
# 创建 Windows 计划任务,每小时运行测试
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-ExecutionPolicy Bypass -File C:\path\to\run-tests.ps1"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName "EKP-Casdoor-Test" -Action $action -Trigger $trigger
```

## 联系支持

如遇到问题:
1. 查看日志文件: `logs/*.log`
2. 运行测试脚本: `.\run-tests.ps1`
3. 查看测试报告: `test-report-*.txt`
