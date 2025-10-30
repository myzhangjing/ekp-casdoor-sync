# EKP-Casdoor Sync（ekp-casdoor-sync）

将 EKP 的组织与用户从 SQL Server 视图高效同步到 Casdoor（兼容 v2.109.0），并正确维护组织树（使用 parentId）。

## 主要特性
- 高性能视图：移除递归 CTE（120s → 0.36s）
- 正确的层级关系：使用 `parentId` 持久化父子关系；当父组织为空时，默认写入 `owner`
- 可靠的更新：`update-group` 使用 URL 查询参数 `id=owner/name`，避免返回 HTML 错误页
- 幂等同步：新增/更新均可重复执行
- 安全实践：不在代码与脚本中硬编码任何密钥，全部来自环境变量

## 环境变量
在运行前设置以下变量（不要把真实值写入仓库）：

- EKP_SQLSERVER_CONN：SQL Server 连接串
- CASDOOR_ENDPOINT：Casdoor 接口地址，例如 `http://172.16.10.110:8000`
- CASDOOR_CLIENT_ID：Casdoor 应用的 ClientId
- CASDOOR_CLIENT_SECRET：ClientSecret
- CASDOOR_DEFAULT_OWNER：Casdoor 的组织 owner，例如 `fzswjtOrganization`
- EKP_USER_GROUP_VIEW（可选）：用户-组织视图名，默认已内置
- SYNC_SINCE_UTC（可选）：ISO 时间；设置时强制以该时间作为增量窗口起点

## 构建
- 需要 .NET 8 SDK
- 构建命令：`dotnet build -c Release`

## 运行
- 推荐使用脚本：`run-sync.ps1`（已移除密钥，可通过环境变量注入）
- 修复父子关系辅助脚本：
  - `update-parentid-from-csv.ps1`：基于导出的 CSV 按行设置 `parentId`，空父级回退为 `owner`
  - `fix-parentid-missing.ps1`：通过 API 扫描 `parentId` 为空的组织并回填为 `owner`

## 字段映射（简版）
- Group
  - name = EKP 组织 id
  - displayName = 组织中文名
  - parentId = 父组织 id；为空时写 `owner`
  - owner = 公司级 owner
  - key = EKP 的 dept_id（用于外部溯源）
- User
  - name = Slug 化后的用户名
  - externalId = EKP 用户 id
  - groups = ["owner/name"]（在同步完成组织后加载映射加入）

## 已知兼容性
- Casdoor v2.109.0：`parentId` 为权威字段；`parentName` 可为空且不会持久化

## 变更记录
见 `CHANGELOG.md`（当前基线 v1.0.0）。

## 贡献
见 `CONTRIBUTING.md`。

## 许可证
MIT（见 `LICENSE`）。
