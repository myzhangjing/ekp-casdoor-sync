# SyncEkpToCasdoor.Test - 诊断功能测试

## 测试目标

验证新增的诊断命令（`--peek-user` 和 `--peek-membership`）能够正确:
1. 从视图中查询用户信息
2. 识别并提示用户缺失的原因（登录名、部门、公司归属等）
3. 查询用户的组织成员关系
4. 在无成员关系时给出正确的回退提示

## 测试环境

- 使用 SQLite 内存数据库模拟 EKP 视图
- 无需真实的 EKP SQL Server 连接
- 测试数据包含:
  - ✓ 正常用户（张璟）：有登录名、部门、成员关系
  - ✓ 正常用户（王五）：完整信息
  - ✗ 各种异常场景的隐式验证（查询不存在的用户）

## 运行测试

```powershell
cd SyncEkpToCasdoor.Test
dotnet run
```

## 测试结果

✅ **所有场景通过**

1. **场景 1: 查询正常用户（张璟）**
   - ✓ 成功找到用户信息
   - ✓ 显示完整字段: id, username, display_name, dept_id, company_name, affiliation, owner, updated_at

2. **场景 2: 查询缺失登录名的用户**
   - ✓ 正确识别用户不存在
   - ✓ 给出准确的排查提示（登录名缺失、部门关联、公司归属等）

3. **场景 3: 查询不在目标公司的用户**
   - ✓ 正确识别用户不存在
   - ✓ 给出排查提示

4. **场景 4: 查询正常用户的成员关系（zhangjing）**
   - ✓ 成功找到 2 条成员关系记录
   - ✓ 显示 username -> dept_id 映射

5. **场景 5: 查询无成员关系的用户（nobody）**
   - ✓ 正确识别无成员关系
   - ✓ 提示将回退使用 dept_id
   - ✓ 给出详细的核对建议

## 结论

诊断命令功能完整、逻辑正确，能够有效帮助排查"用户未同步"或"用户无组织"的问题。

## 实际使用示例

在生产环境中使用（需要设置 EKP 连接字符串）:

```powershell
# 设置连接
$env:EKP_SQLSERVER_CONN = "Server=...;Database=ekp;..."

# 查询用户
.\SyncEkpToCasdoor.exe --peek-user 张璟

# 查询成员关系
.\SyncEkpToCasdoor.exe --peek-membership zhangjing
```
