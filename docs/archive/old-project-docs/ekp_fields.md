# EKP 表字段说明

本文档从代码与视图（`vw_casdoor_users_sync`, `vw_org_structure_sync`）中提取 EKP 表/视图字段并解释含义，便于映射到 Casdoor。

## 用户视图：vw_casdoor_users_sync
- `id` (string) — EKP 用户唯一标识。用于作为 Casdoor user.id。
- `username` (string) — 登录名；同步时会做 slug 处理以生成 Casdoor user.name（小写、非字母数字替换为 `-`）。
- `display_name` (string) — 用户的显示名，对应 Casdoor displayName。
- `email` (string, nullable) — 邮箱，对应 Casdoor email。
- `phone` (string, nullable) — 电话，对应 Casdoor phone。
- `created_at` (datetime) — 创建时间（用于审计或全量/增量判断）。
- `updated_at` (datetime) — 更新时间（用于增量同步的 watermark）。
- `gender` (string, nullable) — 性别字段，映射到 Casdoor 的 gender（若有）。
- `language` (string, nullable) — 语言偏好，映射到 Casdoor language。
- `dept_id` (string, nullable) — 部门 id，可用于推断 membership（或作为 group.parent）。
- `company_name` (string, nullable) — 公司名，用作 owner 的后备值。
- `affiliation` / `department` (string, nullable) — 原始部门/隶属字段，映射到 Casdoor affiliation 字段。
- `owner` (string, nullable) — EKP 所属（直接映射到 Casdoor owner；若为空，程序使用 company_name 或全局默认 owner）。
- `groups` (string, nullable) — EKP 的群组/组织列表（视图字段名与格式依实现而异）。
- `type` (string, nullable) — 用户类型（例如 `normal-user`, `paid-user`），映射到 Casdoor type，用于权限或属性区分。

## 组织视图：vw_org_structure_sync
- `id` / `org_id` / `fd_id` (string) — 组织的唯一 id。代码以此作为 Casdoor group.name。
- `name` (string) — 组织原始名称（有时与 id 重复）。
- `display_name` (string) — 组织显示名，对应 Casdoor displayName。
- `parent_id` / `parent_dept_id` / `fd_parentid` (string, nullable) — 父组织 id，对应 Casdoor parentId；当缺失时同步时会置为 null。
- `type` / `org_type` (string) — 组织类型，映射到 group.type。
- `owner` / `company_name` / `org_owner` (string) — 组织所属者/公司名，对应 Casdoor owner。
- `created_time` / `create_time` (datetime) — 创建时间。
- `updated_time` / `update_time` (datetime) — 更新时间，用于增量同步。
- `dept_id` (string, nullable) — 备用父级标识（代码优先用此字段作为 parent reference）。
- `is_enabled` / `enabled` (bit/bool) — 是否启用，对应 Casdoor group.isEnabled（默认 true）。

## Membership 视图（可选）：EKP_USER_GROUP_VIEW
如果存在，自定义视图需包含列：`user_id`, `group_id`, `owner`, `group_name`。
程序优先使用此视图读取成员关系；若未提供，则基于用户的 `dept_id` 推断成员关系。

## 注意与边界情况
- 字段名在不同环境/版本可能不同：代码实现具有列探测逻辑（GetViewColumns + Pick），会尝试兼容常见列名。
- 时间字段可能为本地时间或 UTC，代码将尽量转换为 UTC 以进行比较。
- 敏感信息（如密码）请勿提交到代码仓库，始终通过环境变量或安全密钥库注入。

如需我为具体字段提供示例值，请允许我连接到数据库或提供一份脱敏的视图导出（CSV）。
