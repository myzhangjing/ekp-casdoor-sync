# 变更日志

## v1.0.0 - 2025-10-30

基线版本：EKP-Casdoor Sync（ekp-casdoor-sync）

- 新增：基于 SQL Server 视图的高性能同步（移除递归 CTE，120s → 0.36s）
- 修复：部门 dept_id 仅指向部门（org_type=2），不再错误指向公司
- 兼容：Casdoor v2.109.0，使用 parentId 持久化组织父子关系
- 行为：当父组织为空时，默认写入 parentId=owner，确保组织树可渲染
- 稳定：update-group 使用 URL 查询参数 id=owner/name，以避免 HTML 错误页
- 脚本：提供 CSV 驱动与 API 扫描两种 parentId 兜底修复脚本
- 安全：移除脚本中的硬编码密钥，改为从环境变量读取
