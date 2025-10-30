# EKP -> Casdoor 字段映射说明

本文档列出 EKP 视图字段到 Casdoor 用户/组织/策略（Casbin）的具体映射规则与 API 端点参考，便于验证与调整。

## 用户映射（EkpUser -> Casdoor user）

基本映射（在 `CasdoorSdkRepository.UpsertUser` 中实现）：
- owner: 优先使用 `EkpUser.Owner`，若为空使用 `EkpUser.CompanyName`，仍为空使用全局 `DefaultOwner`。
- id: `EkpUser.Id` -> Casdoor user.id
- name: slug(EkpUser.Name) -> Casdoor user.name（slug 规则见代码：小写、空格与特殊字符转 `-`，连写 `--` 合并为 `-`）
- displayName: `EkpUser.DisplayName`
- email: `EkpUser.Email`
- phone: `EkpUser.Phone`
- gender: `EkpUser.Gender`
- language: `EkpUser.Language`
- affiliation: `EkpUser.Department`（或 `affiliation` 字段）
- type: `EkpUser.Type`

API 流程：
- GET `/api/get-user?id={owner}/{name}` 检查是否存在
- 若不存在：POST `/api/add-user` (payload: `{ user = { owner, name } }`) 或退回至 `{ owner, name }` 简化 payload
- 若存在或创建后：POST `/api/update-user` (payload: `{ user = { ... } }`)。在 `--minimal` 模式下会跳过复杂属性更新

缓存/解析：
- 程序内部会缓存 EKP user.id -> Casdoor(owner,name) 映射，用于 membership 解析（ResolveUserKey）

## 组织/群组映射（EkpGroup -> Casdoor group）

基本映射（在 `CasdoorSdkRepository.UpsertGroup` 中实现）：
- owner: `EkpGroup.Owner` 或全局默认 owner
- name: `EkpGroup.Id`（代码以 id 为主键）
- displayName: `EkpGroup.DisplayName`
- type: `EkpGroup.Type`
- parentId: 优先使用 `EkpGroup.DeptId`，否则使用 `EkpGroup.ParentId`（若父级缺失则设置为 null）
- isEnabled: `EkpGroup.IsEnabled`（若读取不到则默认 true）

API 流程：
- POST `/api/update-group` 尝试更新全部字段
- 若 update 失败，回退到 POST `/api/add-group` 创建（先以最小 payload `{ owner, name }`）

## 成员关系（Membership -> Casbin 策略）

来源：
- 优先使用外部视图 `EKP_USER_GROUP_VIEW`（需包含 `user_id, group_id, owner, group_name`）；
- 否则基于用户 `dept_id` 推断成员关系（用户 -> 部门 id 作为 group id）。

映射：
- 在 Casdoor 中的 policy 格式：`ptype='g'`，`v0=userKey`，`v1=group:owner/groupName`。
- Add 操作：优先调用 `/api/add-grouping-policy`（新接口），失败则回退到 `/api/add-policy`（兼容接口，payload 例如 `{ policy = { ptype='g', v0, v1 } }`）。
- 批量更新：在收集完单个成员后，调用 `/api/update-group` 填写 `group.users` 列表以确保一致性。

## 增量/检查点
- 程序使用视图中的时间列（`updated_time`, `updated_at`）作为增量判断列，并把上次成功同步时间写入 `sync_state.json`（`SyncStateStore`）。

## 边界情况与建议
- 若字段在视图中缺失，代码会以 NULL/空值替代并继续；对于父级缺失的 group，会在同步时把 parent 置空再创建。
- 推荐在测试环境先用 `--dry-run` 或设置 `CASDOOR_MINIMAL_MODE=true` 来避免误改生产数据。
- 强烈建议 Casdoor 的 client secret 不要写入任何代码或提交，使用环境变量或操作系统密钥库注入。
