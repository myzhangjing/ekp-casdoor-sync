# 变更日志

## v1.3.0 - 2025-10-31

### 🐛 问题修复

- **修复**：清空Casdoor需要点击两次的问题
  - 在删除用户和组织后添加等待机制（各2秒），确保Casdoor索引更新完成
  - 文件：`SyncEkpToCasdoor.UI/Services/DataViewService.cs`

- **修复**：全量同步统计数量不准确的问题
  - 使用正则表达式精确解析控制台输出的最终统计结果
  - 解析格式："组织同步完成，共处理 X 条记录"和"用户同步完成，共处理 Y 条记录"
  - 文件：`SyncEkpToCasdoor.UI/Services/SyncEngineService.cs`

### ✨ 新增功能

- **新增**：OAuth2 登录页面
  - 基于Casdoor的OAuth2授权码流程
  - 支持浏览器自动回调（HttpListener）
  - 支持手动输入授权码（备用方案）
  - 新文件：
    - `Views/LoginWindow.xaml` - 登录窗口界面
    - `Views/LoginWindow.xaml.cs` - 窗口代码后台
    - `ViewModels/LoginViewModel.cs` - 登录业务逻辑
  - 修改文件：
    - `App.xaml.cs` - 添加启动时登录检查

### 📝 文档

- 新增：`docs/功能更新说明_v1.3.0.md` - 详细更新说明
- 新增：`docs/monitoring/` - 定时同步监控文档
  - `如何检查定时同步是否执行.md` - 快速参考
  - `定时同步监控指南.md` - 完整方案
  - `修复说明-两次操作问题.md` - 历史问题记录

### 🛠️ 工具脚本

- 新增：`scripts/check-sync-status.ps1` - 自动化监控脚本
  - 检查同步状态文件时间戳
  - 分析日志文件
  - 验证Windows定时任务
  - 检查环境变量配置

---

## v1.2.0 - 2025-10-30

### ✨ 新增功能

- 数据查看与比对模块
- WPF 图形界面
- 组织和用户数据可视化
- EKP与Casdoor数据三分类比对

---

## v1.1.0 - 2025-10-30

### ✨ 新增功能

- 同步执行界面
- 增量/全量同步支持
- 实时进度显示

---

## v1.0.0 - 2025-10-30

基线版本：EKP-Casdoor Sync（ekp-casdoor-sync）

- 新增：基于 SQL Server 视图的高性能同步（移除递归 CTE，120s → 0.36s）
- 修复：部门 dept_id 仅指向部门（org_type=2），不再错误指向公司
- 兼容：Casdoor v2.109.0，使用 parentId 持久化组织父子关系
- 行为：当父组织为空时，默认写入 parentId=owner，确保组织树可渲染
- 稳定：update-group 使用 URL 查询参数 id=owner/name，以避免 HTML 错误页
- 脚本：提供 CSV 驱动与 API 扫描两种 parentId 兜底修复脚本
- 安全：移除脚本中的硬编码密钥，改为从环境变量读取
