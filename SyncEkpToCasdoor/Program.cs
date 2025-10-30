using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SyncEkpToCasdoor;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("EKP -> Casdoor 同步程序启动。");

        try
        {
            var config = AppConfig.LoadFromEnvironment();
            var stateStore = new SyncStateStore(config.StateFilePath);
            var state = stateStore.Load();

            var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase) ||
                         string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "true", StringComparison.OrdinalIgnoreCase);

            if (dryRun)
            {
                Console.WriteLine("当前运行在演练模式，仅打印配置信息：");
                Console.WriteLine($"  状态文件：{config.StateFilePath}");
                Console.WriteLine($"  增量时间：{config.SinceUtc?.ToString("o") ?? "全量"}");
                Console.WriteLine($"  组织同步视图：{config.MembershipViewName ?? "自动推导"}");
                return 0;
            }

            using var ekp = new EkpRepository(config.EkpConnectionString);
            using var casdoor = new SimpleCasdoorRepository(config);  // 使用简化版仓储
            var service = new SyncService(ekp, casdoor, config, state);

            var purgeFlag = args.Contains("--purge-except-built-in", StringComparer.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_EXCEPT_BUILTIN"), "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_EXCEPT_BUILTIN"), "true", StringComparison.OrdinalIgnoreCase);
            var purgeOnly = args.Contains("--purge-only", StringComparer.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_ONLY"), "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

            if (purgeFlag)
            {
                Console.WriteLine($"开始执行清理：仅保留 owner = {config.DefaultOwner} 的用户与组织。");
                service.PurgeExceptOwner(config.DefaultOwner);
                Console.WriteLine("清理完成。");
                if (purgeOnly)
                {
                    Console.WriteLine("根据参数设置，仅执行清理步骤，程序结束。");
                    state.LastRunUtc = DateTime.UtcNow;
                    stateStore.Save(state);
                    return 0;
                }
            }

            service.SyncGroups();
            service.SyncUsers();
            service.SyncMemberships();

            foreach (var enforcer in config.EnforcerNames)
            {
                casdoor.RefreshEnforcer(enforcer.owner, enforcer.name);
            }

            state.LastRunUtc = DateTime.UtcNow;
            stateStore.Save(state);
            Console.WriteLine($"同步流程结束，检查点已写入：{config.StateFilePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"同步失败：{ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class AppConfig
{
    public string EkpConnectionString { get; init; } = string.Empty;
    public string CasdoorEndpoint { get; init; } = string.Empty;
    public string CasdoorClientId { get; init; } = string.Empty;
    public string CasdoorClientSecret { get; init; } = string.Empty;
    public string? CasdoorOrganization { get; init; }
    public string? CasdoorApplication { get; init; }
    public string DefaultOwner { get; init; } = "built-in";
    public bool ForceOwnerRefresh { get; init; }
    public bool MinimalMode { get; init; }
    public string? MembershipViewName { get; init; }
    public DateTime? SinceUtc { get; init; }
    public string StateFilePath { get; init; } = "sync_state.json";
    public IReadOnlyList<(string owner, string name)> EnforcerNames { get; init; } = Array.Empty<(string owner, string name)>();

    public static AppConfig LoadFromEnvironment()
    {
        string Require(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"缺少必要环境变量：{key}");
            return value;
        }

        string? Optional(string key) => Environment.GetEnvironmentVariable(key);

        var defaultOwner = Optional("CASDOOR_DEFAULT_OWNER") ?? "built-in";
        DateTime? since = null;
        var sinceRaw = Optional("SYNC_SINCE_UTC");
        if (!string.IsNullOrWhiteSpace(sinceRaw) && DateTime.TryParse(sinceRaw, null,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            since = parsed.ToUniversalTime();
        }

        var enforcerRaw = Optional("CASDOOR_ENFORCERS");
        var enforcers = new List<(string owner, string name)>();
        if (!string.IsNullOrWhiteSpace(enforcerRaw))
        {
            foreach (var part in enforcerRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                var slash = trimmed.IndexOf('/');
                if (slash > 0)
                {
                    enforcers.Add((trimmed[..slash], trimmed[(slash + 1)..]));
                }
            }
        }
        if (enforcers.Count == 0)
        {
            enforcers.Add(("built-in", "user-enforcer-built-in"));
        }

        return new AppConfig
        {
            EkpConnectionString = Require("EKP_SQLSERVER_CONN"),
            CasdoorEndpoint = Require("CASDOOR_ENDPOINT"),
            CasdoorClientId = Require("CASDOOR_CLIENT_ID"),
            CasdoorClientSecret = Require("CASDOOR_CLIENT_SECRET"),
            CasdoorOrganization = Optional("CASDOOR_ORGANIZATION"),
            CasdoorApplication = Optional("CASDOOR_APPLICATION"),
            DefaultOwner = defaultOwner,
            ForceOwnerRefresh = string.Equals(Optional("FORCE_OWNER_REFRESH"), "1", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Optional("FORCE_OWNER_REFRESH"), "true", StringComparison.OrdinalIgnoreCase),
            MinimalMode = string.Equals(Optional("CASDOOR_MINIMAL_MODE"), "1", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(Optional("CASDOOR_MINIMAL_MODE"), "true", StringComparison.OrdinalIgnoreCase),
            MembershipViewName = Optional("EKP_USER_GROUP_VIEW"),
            SinceUtc = since,
            StateFilePath = Optional("SYNC_STATE_FILE") ?? "sync_state.json",
            EnforcerNames = enforcers
        };
    }
}

internal sealed class SyncState
{
    public DateTime? LastGroupSyncUtc { get; set; }
    public DateTime? LastUserSyncUtc { get; set; }
    public DateTime? LastMembershipSyncUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
}

internal sealed class SyncStateStore
{
    private readonly string _path;

    public SyncStateStore(string path) => _path = path;

    public SyncState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new SyncState();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<SyncState>(json) ?? new SyncState();
        }
        catch
        {
            return new SyncState();
        }
    }

    public void Save(SyncState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}

internal static class Slug
{
    public static string Name(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var chars = input.Trim()
            .ToLowerInvariant()
            .Select(c =>
                char.IsLetterOrDigit(c) ? c :
                (char.IsWhiteSpace(c) || c == '-' || c == '_') ? '-' : '\0')
            .Where(c => c != '\0')
            .ToArray();

        var result = new string(chars);
        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }
        return result.Trim('-');
    }
}

internal record EkpUser(
    string Id,
    string Name,
    string DisplayName,
    string? Email,
    string? Phone,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? Gender,
    string? Language,
    string? DeptId,
    string? CompanyName,
    string? Department,
    string? Owner,
    // string? Groups, // This property is obsolete.
    string? Type
);

internal record EkpGroup(
    string Id,
    string Name,
    string DisplayName,
    string? ParentId,
    string Type,
    string Owner,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string? DeptId,
    bool IsEnabled
);

internal sealed class EkpRepository : IDisposable
{
    private readonly SqlConnection _connection;

    public EkpRepository(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        _connection.Open();
    }

    public IEnumerable<EkpGroup> GetGroups(DateTime? sinceUtc)
    {
        var columns = GetViewColumns("vw_org_structure_sync");

        string Pick(params string[] options) =>
            options.FirstOrDefault(o => columns.Contains(o, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;

        string SelectOrNull(string column, string alias, string sqlType) =>
            string.IsNullOrEmpty(column) ? $"CAST(NULL AS {sqlType}) AS {alias}" : $"[{column}] AS {alias}";

        var idCol = Pick("id", "fd_id", "org_id");
        var nameCol = Pick("name", "fd_id");
        var displayCol = Pick("display_name", "fd_name");
        var parentCol = Pick("parent_id", "parent_dept_id", "fd_parentid", "fd_parentorgid");
        var typeCol = Pick("type", "org_type");
        var ownerCol = Pick("owner", "company_name", "org_owner");
        var createdCol = Pick("created_time", "create_time", "fd_create_time");
        var updatedCol = Pick("updated_time", "update_time", "fd_alter_time");
        var deptCol = Pick("dept_id", "parent_dept_id");
        var enabledCol = Pick("is_enabled", "enabled", "fd_is_available");

        var sql = $@"
SELECT
    {SelectOrNull(idCol, "id", "NVARCHAR(255)")},
    {SelectOrNull(nameCol, "name", "NVARCHAR(255)")},
    {SelectOrNull(displayCol, "display_name", "NVARCHAR(255)")},
    {SelectOrNull(parentCol, "parent_id", "NVARCHAR(255)")},
    {SelectOrNull(typeCol, "type", "NVARCHAR(255)")},
    {SelectOrNull(ownerCol, "owner", "NVARCHAR(255)")},
    {SelectOrNull(createdCol, "created_time", "DATETIME")},
    {SelectOrNull(updatedCol, "updated_time", "DATETIME")},
    {SelectOrNull(deptCol, "dept_id", "NVARCHAR(255)")},
    {SelectOrNull(enabledCol, "is_enabled", "BIT")}
FROM vw_org_structure_sync";

        if (sinceUtc.HasValue && !string.IsNullOrEmpty(updatedCol))
        {
            sql += $" WHERE [{updatedCol}] > @since";
        }

        using var cmd = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 120
        };
        if (sinceUtc.HasValue && !string.IsNullOrEmpty(updatedCol))
        {
            cmd.Parameters.AddWithValue("@since", sinceUtc.Value);
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var createdObj = reader.GetValue(6);
            var updatedObj = reader.GetValue(7);
            var deptObj = reader.GetValue(8);
            var enabledObj = reader.GetValue(9);

            yield return new EkpGroup(
                Id: reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Name: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                DisplayName: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ParentId: reader.IsDBNull(3) ? null : reader.GetString(3),
                Type: reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Owner: reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                CreatedUtc: createdObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(createdObj).ToUniversalTime(),
                UpdatedUtc: updatedObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(updatedObj).ToUniversalTime(),
                DeptId: deptObj is DBNull ? null : Convert.ToString(deptObj),
                IsEnabled: enabledObj is DBNull ? true : Convert.ToBoolean(enabledObj)
            );
        }
    }

    public IEnumerable<EkpUser> GetUsers(DateTime? sinceUtc)
    {
        var sql = @"
SELECT id, username, display_name, email, phone, created_at, updated_at, gender, language,
       dept_id, company_name, affiliation, owner, type
FROM vw_casdoor_users_sync";

        if (sinceUtc.HasValue)
        {
            sql += " WHERE updated_at > @since";
        }

        using var cmd = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 120
        };
        if (sinceUtc.HasValue)
        {
            cmd.Parameters.AddWithValue("@since", sinceUtc.Value);
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var createdObj = reader.GetValue(5);
            var updatedObj = reader.GetValue(6);

            yield return new EkpUser(
                Id: reader.GetString(0),
                Name: reader.GetString(1),
                DisplayName: reader.GetString(2),
                Email: reader.IsDBNull(3) ? null : reader.GetString(3),
                Phone: reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAtUtc: createdObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(createdObj).ToUniversalTime(),
                UpdatedAtUtc: updatedObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(updatedObj).ToUniversalTime(),
                Gender: reader.IsDBNull(7) ? null : reader.GetString(7),
                Language: reader.IsDBNull(8) ? null : reader.GetString(8),
                DeptId: reader.IsDBNull(9) ? null : reader.GetString(9),
                CompanyName: reader.IsDBNull(10) ? null : reader.GetString(10),
                Department: reader.IsDBNull(11) ? null : reader.GetString(11),
                Owner: reader.IsDBNull(12) ? null : reader.GetString(12),
                Type: reader.IsDBNull(13) ? null : reader.GetString(13)
            );
        }
    }

    public Dictionary<string, List<string>> GetUserGroupMemberships(string? viewName)
    {
        var memberships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(viewName))
        {
            Console.WriteLine("警告: 未配置 EKP_USER_GROUP_VIEW, 用户将不会被自动添加到任何组织。");
            return memberships;
        }

        Console.WriteLine($"从视图 {viewName} 读取用户组织关系...");
        var sql = $"SELECT user_id, group_id FROM {viewName}";

        using var cmd = new SqlCommand(sql, _connection);
        
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                
                var userId = reader.GetString(0);
                var groupId = reader.GetString(1);

                if (!memberships.TryGetValue(userId, out var groups))
                {
                    groups = new List<string>();
                    memberships[userId] = groups;
                }
                groups.Add(groupId);
            }
        }
        Console.WriteLine($"成功从视图 {viewName} 加载 {memberships.Count} 个用户的 {memberships.Values.Sum(g => g.Count)} 条组织关系。");
        return memberships;
    }

    private HashSet<string> GetViewColumns(string viewName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new SqlCommand("SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@name)", _connection);
        cmd.Parameters.AddWithValue("@name", viewName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public void Dispose() => _connection.Dispose();
}

internal interface ICasdoorRepository : IDisposable
{
    void UpsertGroup(EkpGroup group);
    void UpsertUser(EkpUser user, string owner, bool forceOwner, List<string>? groupIds);
    void PurgeExceptOwner(string owner);
    (string Owner, string Name)? ResolveUserKey(string owner, string userId);
    void RefreshEnforcer(string owner, string name);
    void ExportGroupHierarchy(string filePath);
    void LoadCasdoorGroupMapping();
}

internal sealed class SyncService
{
    private readonly EkpRepository _ekp;
    private readonly ICasdoorRepository _casdoor;
    private readonly AppConfig _config;
    private readonly SyncState _state;
    private readonly Dictionary<string, string> _userNameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _userOwnerCache = new(StringComparer.OrdinalIgnoreCase);

    public SyncService(EkpRepository ekp, ICasdoorRepository casdoor, AppConfig config, SyncState state)
    {
        _ekp = ekp;
        _casdoor = casdoor;
        _config = config;
        _state = state;
    }

    public void PurgeExceptOwner(string owner)
    {
        _casdoor.PurgeExceptOwner(owner);
        _state.LastGroupSyncUtc = null;
        _state.LastUserSyncUtc = null;
        _state.LastMembershipSyncUtc = null;
    }

    public void SyncGroups()
    {
        var since = _config.SinceUtc ?? _state.LastGroupSyncUtc;
        Console.WriteLine($"开始同步组织结构，时间范围：{since?.ToString("o") ?? "全量"}");
        var groups = _ekp.GetGroups(since).ToList();
        var byId = new Dictionary<string, EkpGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            if (!string.IsNullOrWhiteSpace(g.Id))
            {
                byId[g.Id] = g;
            }
        }
        groups = byId.Values.ToList();

        var depthCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int ComputeDepth(EkpGroup group, HashSet<string>? stack = null)
        {
            if (string.IsNullOrWhiteSpace(group.Id)) return 0;
            if (depthCache.TryGetValue(group.Id, out var depth)) return depth;

            stack ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!stack.Add(group.Id))
            {
                depthCache[group.Id] = 0;
                return 0;
            }

            var parentKey = !string.IsNullOrWhiteSpace(group.DeptId) ? group.DeptId : group.ParentId;
            if (string.IsNullOrWhiteSpace(parentKey) || !byId.TryGetValue(parentKey, out var parent))
            {
                depth = 0;
            }
            else
            {
                depth = 1 + ComputeDepth(parent, stack);
            }

            stack.Remove(group.Id);
            depthCache[group.Id] = depth;
            return depth;
        }

        var ordered = groups
            .OrderBy(g => ComputeDepth(g))
            .ThenBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var count = 0L;
        foreach (var group in ordered)
        {
            var parentKey = !string.IsNullOrWhiteSpace(group.DeptId) ? group.DeptId : group.ParentId;
            var depth = ComputeDepth(group);
            string parentInfo;
            var hasParent = true;
            if (string.IsNullOrWhiteSpace(parentKey))
            {
                parentInfo = "<无>";
                hasParent = false;
            }
            else
            {
                hasParent = byId.ContainsKey(parentKey);
                parentInfo = hasParent ? $"存在({parentKey})" : $"缺失({parentKey})";
            }
            Console.WriteLine($"  -> 同步群组：{group.Id}（名称：{group.DisplayName}），层级：{depth}，父级：{parentInfo}");
            var groupForSync = hasParent ? group : group with { ParentId = null, DeptId = null };
            _casdoor.UpsertGroup(groupForSync);
            count++;
        }
        Console.WriteLine($"组织同步完成，共处理 {count} 条记录。");
        
        // 导出组织层级关系到CSV文件
        var hierarchyFile = Path.Combine("logs", $"organization_hierarchy_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        Directory.CreateDirectory("logs");
        _casdoor.ExportGroupHierarchy(hierarchyFile);
        
        _state.LastGroupSyncUtc = DateTime.UtcNow;
    }

    public void SyncUsers()
    {
        // *** 关键步骤: 在同步用户之前,先从Casdoor加载所有组织映射 ***
        _casdoor.LoadCasdoorGroupMapping();
        
        var since = _config.SinceUtc ?? _state.LastUserSyncUtc;
        Console.WriteLine($"开始同步用户信息，时间范围：{since?.ToString("o") ?? "全量"}");

        // 预加载所有用户-组织关系
        var userGroupMemberships = _ekp.GetUserGroupMemberships(_config.MembershipViewName);

        var count = 0L;
        foreach (var user in _ekp.GetUsers(since))
        {
            var owner = string.IsNullOrWhiteSpace(user.Owner)
                ? (string.IsNullOrWhiteSpace(user.CompanyName) ? _config.DefaultOwner : user.CompanyName!.Trim())
                : user.Owner.Trim();
            
            userGroupMemberships.TryGetValue(user.Id, out var groupIds);

            // Fallback logic: if no groups from the dedicated view, use the user's dept_id
            if ((groupIds is null || groupIds.Count == 0) && !string.IsNullOrWhiteSpace(user.DeptId))
            {
                Console.WriteLine($"  -> 用户 {user.Id} 未在成员关系视图中找到, 回退使用其部门ID: {user.DeptId}");
                groupIds = new List<string> { user.DeptId };
            }
            
            _casdoor.UpsertUser(user, owner, _config.ForceOwnerRefresh, groupIds);
            
            // Cache user info after upsert
            var resolvedKey = ResolveUserKey(user.Id, owner);
            if (resolvedKey is not null)
            {
                 _userNameCache[user.Id] = resolvedKey.Value.Name;
                 _userOwnerCache[user.Id] = resolvedKey.Value.Owner;
            }
            count++;
        }
        Console.WriteLine($"用户同步完成，共处理 {count} 条记录。");
        _state.LastUserSyncUtc = DateTime.UtcNow;
    }

    public void SyncMemberships()
    {
        // This method is now obsolete. The logic is merged into SyncUsers.
        Console.WriteLine("成员关系同步步骤已合并到用户同步中，此步骤将被跳过。");
        _state.LastMembershipSyncUtc = DateTime.UtcNow;
    }

    private (string Owner, string Name)? ResolveUserKey(string userId, string preferredOwner)
    {
        if (_userNameCache.TryGetValue(userId, out var name) &&
            _userOwnerCache.TryGetValue(userId, out var owner))
        {
            return (owner, name);
        }

        var info = _casdoor.ResolveUserKey(preferredOwner, userId);
        if (info is null)
        {
            return null;
        }

        _userNameCache[userId] = info.Value.Name;
        _userOwnerCache[userId] = info.Value.Owner;
        return info;
    }
}
