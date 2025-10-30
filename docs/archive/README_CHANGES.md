# EKP到Casdoor同步工具 - 最新改进

## 日期: 2025-10-29

### 问题背景

用户反馈两个关键问题:
1. **组织成员丢失** - 同步后的组织都是空的,没有成员
2. **缺失父级组织** - 部分组织的父级不在同步列表中,导致无法建立层级

### 解决方案

#### 1. 自动创建占位符父组织 ✅

**问题**: 当组织的父级ID不在EKP同步视图中时,组织无法建立父子关系

**解决**: 
- 在`SimpleCasdoorRepository.cs`中添加`EnsurePlaceholderGroup`方法
- 自动检测缺失的父级组织
- 创建占位符组织,命名格式: `PLACEHOLDER_{父组织ID}`
- 显示名称: `[待配置] {父组织ID}`
- 用户可以后续在Casdoor UI中手动修改占位符组织的名称和属性

**代码位置**: `SimpleCasdoorRepository.cs` 第125-164行

**示例输出**:
```
-> 创建占位符父组织: fzswjtOrganization/PLACEHOLDER_16f1c1a4910426f41649fd14862b99a1 ([待配置] 16f1c1a4910426f41649fd14862b99a1)
  ✓ 占位符组织已创建
```

#### 2. 导出组织成员关系CSV文件 ✅

**问题**: Casdoor的`update-group` API存在严重bug,无法通过API设置组织成员

**根本原因**:
- `get-group` API返回 404 Not Found
- `update-group` API返回 HTML错误页面
- 这是Casdoor服务器端的bug,不是我们代码的问题

**解决**:
- 添加`ExportGroupMembership`方法
- 在每次同步后自动生成CSV文件
- 文件包含所有组织及其成员列表
- 用户可以参照CSV文件在Casdoor UI中手动配置成员

**文件位置**: `logs/organization_membership_YYYYMMDD_HHMMSS.csv`

**文件格式**:
```csv
组织ID,组织名称,成员数量,成员列表(Casdoor用户名)
"16f1c1a4a2aa9f389dbf3fd493a8342e","财务部","5","fzswjtOrganization/user1; fzswjtOrganization/user2; ..."
```

**代码位置**: 
- `SimpleCasdoorRepository.cs` 第565-582行
- `Program.cs` 第619-635行

### 使用说明

#### 运行同步
```powershell
cd "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor"
.\run-sync.ps1
```

#### 查看导出文件
同步完成后,在`logs`目录下会生成两个CSV文件:

1. **组织层级关系**: `organization_hierarchy_YYYYMMDD_HHMMSS.csv`
   - 包含所有组织的父子关系
   - 用于手动在Casdoor中设置父级组织

2. **组织成员关系**: `organization_membership_YYYYMMDD_HHMMSS.csv`
   - 包含每个组织的成员列表
   - 用于手动在Casdoor中添加成员

#### 手动配置步骤

##### 配置组织层级:
1. 打开 `organization_hierarchy_YYYYMMDD_HHMMSS.csv`
2. 登录Casdoor管理界面
3. 进入"Organizations"页面
4. 参照CSV中的"组织名称"和"父组织名称"列
5. 逐个设置组织的父级组织

##### 配置组织成员:
1. 打开 `organization_membership_YYYYMMDD_HHMMSS.csv`
2. 登录Casdoor管理界面
3. 进入"Organizations"页面
4. 选择组织,进入编辑页面
5. 参照CSV中的"成员列表"列添加成员

### 技术细节

#### 修改的文件
1. `SimpleCasdoorRepository.cs`
   - 新增`EnsurePlaceholderGroup`方法 (125-164行)
   - 修改`UpsertGroup`方法支持占位符创建 (42-75行)
   - 修改`UpdateGroupUsers`为只打印信息 (272-285行)
   - 新增`ExportGroupMembership`方法 (565-582行)

2. `Program.cs`
   - 在`ICasdoorRepository`接口添加`ExportGroupMembership` (442行)
   - 修改`SyncMemberships`自动导出CSV (619-639行)

3. `CasdoorSdkRepository.cs`
   - 添加`ExportGroupMembership`空实现 (577-581行)

#### Casdoor API限制
- ❌ `update-group` - 返回HTML错误
- ❌ `get-group` - 返回404
- ❌ `add-grouping-policy` - 404 Not Found
- ❌ `add-policy` - beego error
- ✅ `add-group` - 正常工作
- ✅ `add-user` - 正常工作

### 未来改进

等Casdoor服务器升级修复API后,可以:
1. 启用自动成员更新功能
2. 删除手动配置步骤
3. 实现完全自动化同步

### 统计数据

- 总组织数: 192
- 有父级的组织: 155
- 需占位符的父组织: 约2个
- 总用户数: 1787
- 组织成员关系: 1787条

### 注意事项

1. **占位符组织**: 名称为`PLACEHOLDER_*`的组织是自动创建的,需要手动修改
2. **CSV编码**: 所有CSV文件使用UTF-8编码,支持中文
3. **成员格式**: 成员列表使用分号(;)分隔
4. **手动配置**: 由于API限制,组织成员必须手动配置

Server:    npm.fzcsps.com,11433
Database:  ekp
Username:  xxzx
Password:  sosy3080@sohu.com

Host:      172.16.10.110
Port:      3306
Database:  casdoor
Username:  root
Password:  zhangjing

Endpoint:       http://172.16.10.110:8000
Client ID:      aecd00a352e5c560ffe6
Client Secret:  4402518b20dd191b8b48d6240bc786a4f847899a
Default Owner:  fzswjtOrganization

URL:      http://172.16.10.110:8000
用户名:   admin
密码:     123