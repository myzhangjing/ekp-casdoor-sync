# 同步状态管理 Bug 修复

## 问题描述

用户反馈："同步状态一直在运行中" - 点击同步按钮后，UI 显示"运行中"状态，但实际同步可能已完成或崩溃，状态无法恢复。

## 根本原因

1. **静态 bool 变量不适合多实例场景**
   - 原代码使用 `private static bool _isRunning = false`
   - Blazor Server 是多实例的，但静态变量在所有实例间共享
   - 异常发生时 `finally` 块可能未执行，导致 `_isRunning` 永久为 `true`

2. **缺少详细进度日志**
   - 同步 1187 个用户可能需要较长时间
   - 用户看不到进度，误以为卡死

## 修复方案

### 1. 使用 SemaphoreSlim 替代静态 bool

```csharp
// 修改前
private static bool _isRunning = false;

if (_isRunning) { /* 返回错误 */ }
_isRunning = true;
try { /* 同步逻辑 */ }
finally { _isRunning = false; }

// 修改后
private static readonly SemaphoreSlim _syncLock = new(1, 1);
private static DateTime? _syncStartTime = null;

if (!await _syncLock.WaitAsync(0, cancellationToken)) { /* 返回错误 */ }
_syncStartTime = DateTime.Now;
try { /* 同步逻辑 */ }
finally { 
    _syncStartTime = null;
    _syncLock.Release(); // 保证锁一定会释放
}
```

**优势:**
- `SemaphoreSlim` 是线程安全的同步原语
- `WaitAsync(0)` 非阻塞尝试获取锁，失败立即返回
- `Release()` 在 `finally` 中确保异常时也能释放锁
- 记录开始时间，可显示已运行时长

### 2. 添加进度日志

```csharp
// 组织同步进度 (每 10 个记录一次)
int orgIndex = 0;
foreach (var o in orgs) {
    orgIndex++;
    if (orgIndex % 10 == 0 || orgIndex == orgs.Count) {
        _logger.LogInformation("同步组织进度: {Current}/{Total} ({Percent}%)", 
            orgIndex, orgs.Count, (orgIndex * 100 / orgs.Count));
    }
    // 同步逻辑...
}

// 用户同步进度 (每 50 个记录一次)
while (await ur.ReadAsync(cancellationToken)) {
    if (usersSynced > 0 && usersSynced % 50 == 0) {
        _logger.LogInformation("同步用户进度: {Current}/{Total} ({Percent}%)", 
            usersSynced, totalUsersEstimate, (usersSynced * 100 / totalUsersEstimate));
    }
    // 同步逻辑...
}
```

**输出示例:**
```
[14:23:15 INF] 同步组织进度: 10/177 (5%)
[14:23:16 INF] 同步组织进度: 20/177 (11%)
...
[14:23:45 INF] 同步用户进度: 50/1187 (4%)
[14:24:12 INF] 同步用户进度: 100/1187 (8%)
...
[14:28:33 INF] 用户同步完成: 1187 个用户已处理
```

### 3. 改进运行状态检测

```csharp
// 修改前
return new SyncState { IsRunning = _isRunning };

// 修改后
return new SyncState { IsRunning = _syncLock.CurrentCount == 0 };
```

**原理:** 
- `SemaphoreSlim.CurrentCount` 返回当前可用的锁数量
- 初始化为 `new(1, 1)` 表示最多 1 个并发
- `CurrentCount == 0` 表示锁已被占用，即同步正在运行

## 测试验证

### 测试场景 1: 正常同步
1. 启动应用
2. 点击"全量同步"
3. **预期:** 
   - UI 显示"运行中"
   - 终端输出进度日志
   - 完成后 UI 显示"空闲"

### 测试场景 2: 并发请求
1. 点击"全量同步"
2. 立即再次点击"全量同步"
3. **预期:** 
   - 第二次请求返回 "同步任务正在运行中，请稍后再试 (已运行 X 秒)"
   - 第一次同步继续执行不受影响

### 测试场景 3: 异常恢复
1. 修改代码模拟异常：`throw new Exception("测试异常");`
2. 点击"全量同步"
3. **预期:** 
   - UI 显示同步失败
   - 再次点击"全量同步"时能正常启动(锁已释放)

## 其他改进建议

### 短期改进 (1-2 天)
1. ✅ **修复状态管理** (已完成)
2. ✅ **添加进度日志** (已完成)
3. **UI 显示进度百分比** - 需要 SignalR 实时推送
4. **添加取消按钮** - 使用 `CancellationTokenSource`

### 中期改进 (1 周)
5. **同步历史记录** - 保存每次同步的详细日志
6. **错误详情查看** - 点击展开查看失败的具体用户/组织
7. **部门成员查询** - 实现 `PeekMembershipAsync` 的 UI 入口
8. **数据导出功能** - 导出同步结果到 CSV/Excel

### 长期改进 (2-4 周)
9. **定时任务配置** - 内置 Hangfire/Quartz 定时器
10. **批量操作** - 删除孤立用户、重置密码等
11. **手动选择同步范围** - 按公司/部门筛选
12. **性能统计** - 显示同步速度、耗时分布

## 修改文件清单

- `SyncEkpToCasdoor.Web/Services/SyncService.cs` (核心修复)
  - 使用 `SemaphoreSlim` 替代静态 bool
  - 添加组织同步进度日志 (每 10 个)
  - 添加用户同步进度日志 (每 50 个)
  - 改进运行状态检测逻辑

## 部署步骤

```powershell
# 1. 停止现有服务
docker-compose down

# 2. 重新构建
docker-compose build

# 3. 启动服务
docker-compose up -d

# 4. 查看日志(验证进度输出)
docker-compose logs -f sync-web
```

## 回滚方案

如果修复后出现问题，使用 Git 回滚：

```bash
git log --oneline -5  # 查看最近提交
git revert <commit-hash>  # 回滚到修复前
docker-compose up -d --build
```

---

**修复日期:** 2025-10-31  
**影响范围:** 同步服务核心逻辑  
**测试状态:** ✅ 编译通过，待运行时测试  
**优先级:** 🔴 高 (影响核心功能)
