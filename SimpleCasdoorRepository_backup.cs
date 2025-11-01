using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SyncEkpToCasdoor;

/// <summary>
/// 简化版 Casdoor 仓储 - 直接映射 EKP 数据到 Casdoor API，无复杂回退逻辑
/// </summary>
internal sealed class SimpleCasdoorRepository : ICasdoorRepository
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _defaultOwner;
    private readonly Dictionary<string, Dictionary<string, (string Owner, string Name)>> _userIdCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _syncedGroupIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string DisplayName, string? ParentId, string? ParentDisplayName)> _groupHierarchy = new(StringComparer.OrdinalIgnoreCase);
    
    // 从Casdoor加载的组织映射: EkpGroupId -> Casdoor完整组织名称 (owner/name)
    private Dictionary<string, string>? _casdoorGroupMapping = null;

    public SimpleCasdoorRepository(AppConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.CasdoorEndpoint) ||
            string.IsNullOrWhiteSpace(cfg.CasdoorClientId) ||
            string.IsNullOrWhiteSpace(cfg.CasdoorClientSecret))
        {
            throw new InvalidOperationException("缺少 Casdoor 接口配置：CASDOOR_ENDPOINT、CASDOOR_CLIENT_ID 或 CASDOOR_CLIENT_SECRET。");
        }

        _endpoint = cfg.CasdoorEndpoint.TrimEnd('/');
        _clientId = cfg.CasdoorClientId!;
        _clientSecret = cfg.CasdoorClientSecret!;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _defaultOwner = cfg.DefaultOwner;
    }

    /// <summary>
    /// 同步组织 - 仅创建模式(绕过 Casdoor update-group API bug)
    /// </summary>
    public void UpsertGroup(EkpGroup g)
    {
        var owner = string.IsNullOrWhiteSpace(g.Owner) ? _defaultOwner : g.Owner.Trim();
        // 修复: Casdoor 的分级依赖 parentId 递归，应当使用父级的 name 作为 parentId
        // 根节点的 parentId 设为 owner，确保顶层节点可见
        string parentIdForApi = string.IsNullOrWhiteSpace(g.ParentId) ? owner : g.ParentId; // Casdoor 接口期望 parentId=父级的 name，根节点为 owner
        
        // 先记录层级关系(用于导出),必须在检查之前就记录
        _groupHierarchy[g.Id] = (g.DisplayName, g.ParentId, null);
        
        // 处理父组织ID - 使用视图中的parent_id字段
        string? parentName = parentIdForApi; // 与 parentId 一致，便于排查
        if (!string.IsNullOrWhiteSpace(g.ParentId) && _groupHierarchy.TryGetValue(g.ParentId!, out var parentInfo))
        {
            _groupHierarchy[g.Id] = (g.DisplayName, g.ParentId, parentInfo.DisplayName);
        }
        
        var parentDisplayInfo = _groupHierarchy[g.Id].ParentDisplayName ?? g.ParentId ?? "无";
        Console.WriteLine($"  -> 同步组织: {owner}/{g.Id} ({g.DisplayName}) parent={parentDisplayInfo}");

        // 构建完整的组织数据（Casdoor v2.x：建议使用 parentId 持久化父子关系）
        var createData = new 
        { 
            owner, 
            name = g.Id,                    // 使用 EKP ID 作为 Casdoor name
            displayName = g.DisplayName,    // 显示名称
            parentName,                     // 可选：显示用途（owner/name）
            parentId = parentIdForApi,      // 关键：父级用父组织的 name（不带 owner）
            type = g.Type,                  // 组织类型
            isEnabled = g.IsEnabled,        // 是否启用
            key = g.DeptId                  // EKP 部门ID存到 key 字段
        };
        
        var createResp = PostAsync("/api/add-group", createData).GetAwaiter().GetResult();
        
        if (IsOk(createResp))
        {
            Console.WriteLine($"      ✓ 组织已创建: {owner}/{g.Id} - {g.DisplayName}{(parentName != null ? $" (父级: {parentName})" : "")}");
            _syncedGroupIds.Add(g.Id);
            return;
        }

        var errMsg = GetErrorMsg(createResp);
        
        // 如果是 Duplicate 或 already exists 错误,说明组织已存在
        if (errMsg.Contains("Duplicate") || errMsg.Contains("duplicate") || 
            errMsg.Contains("已存在") || errMsg.Contains("PRIMARY") ||
            errMsg.Contains("already exists"))
        {
                Console.WriteLine($"      ⚠ 组织已存在,尝试更新: {owner}/{g.Id} - {g.DisplayName}");
            
                // 尝试更新现有组织 - 使用正确的id格式 "owner/name"
                var updateData = new 
                { 
                    id = $"{owner}/{g.Id}",         // 重要: id格式必须是 "owner/name"
                    owner, 
                    name = g.Id,
                    displayName = g.DisplayName,
                    parentName,
                    parentId = parentIdForApi,
                    type = g.Type,
                    isEnabled = g.IsEnabled,
                    key = g.DeptId
                };
            
                // 注意: Casdoor 要求在 URL 查询参数中提供 id=owner/name
                var updatePath = $"/api/update-group?id={Uri.EscapeDataString(owner + "/" + g.Id)}";
                var updateResp = PostAsync(updatePath, updateData).GetAwaiter().GetResult();
            
                if (IsOk(updateResp))
                {
                    Console.WriteLine($"      ✓ 组织已更新: {owner}/{g.Id} - parentName={parentName ?? "null"}");
                    _syncedGroupIds.Add(g.Id);
                    return;
                }
            
                var updateErr = GetErrorMsg(updateResp);
                Console.WriteLine($"      ✗ 更新失败: {updateErr}");
            
                // 即使更新失败,也认为组织已同步(至少存在于Casdoor中)
            _syncedGroupIds.Add(g.Id);
            return;
        }

        // 其他错误才抛出异常
        throw new InvalidOperationException($"创建组织 {owner}/{g.Id} 失败: {errMsg}");
    }

    /// <summary>
    /// 从Casdoor加载所有组织并建立EKP ID到Casdoor完整名称的映射
    /// 必须在所有组织创建完成后调用
    /// </summary>
    public void LoadCasdoorGroupMapping()
    {
        Console.WriteLine("\n正在从Casdoor加载所有组织映射...");
        
        _casdoorGroupMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var resp = GetAsync($"/api/get-groups?owner={Uri.EscapeDataString(_defaultOwner)}").GetAwaiter().GetResult();
        
        if (!resp.HasValue || !resp.Value.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("  ⚠ 无法从Casdoor获取组织列表");
            return;
        }

        int count = 0;
        foreach (var group in data.EnumerateArray())
        {
            if (group.TryGetProperty("owner", out var ownerProp) &&
                group.TryGetProperty("name", out var nameProp))
            {
                var groupOwner = ownerProp.GetString();
                var groupName = nameProp.GetString();
                
                if (!string.IsNullOrWhiteSpace(groupOwner) && !string.IsNullOrWhiteSpace(groupName))
                {
                    // 使用name作为key(这是EKP的组织ID),value是完整的"owner/name"格式
                    _casdoorGroupMapping[groupName] = $"{groupOwner}/{groupName}";
                    count++;
                }
            }
        }
        
        Console.WriteLine($"  ✓ 已加载 {count} 个组织映射");
    }

    /// <summary>
    /// 同步用户 - 在创建时直接包含groups信息
    /// </summary>
    public void UpsertUser(EkpUser u, string owner, bool forceOwner, List<string>? groupIds)
    {
        var targetOwner = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();
        var userName = Slug.Name(u.Name);

        // 使用映射将EKP组织ID转换为Casdoor完整组织名称 ("owner/name")
        string[]? casdoorGroups = null;
        if (groupIds != null && groupIds.Count > 0 && _casdoorGroupMapping != null)
        {
            var mappedGroups = groupIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && _casdoorGroupMapping.ContainsKey(id))
                .Select(id => _casdoorGroupMapping[id])
                .Distinct() // Ensure no duplicate groups
                .ToList();
            
            if (groupIds.Count > mappedGroups.Count)
            {
                var unmapped = groupIds.Where(id => !_casdoorGroupMapping.ContainsKey(id));
                Console.WriteLine($"      警告: 用户 {u.Id} 的部分组织ID在Casdoor中未找到或重复: {string.Join(", ", unmapped)}");
            }
            
            casdoorGroups = mappedGroups.Count > 0 ? mappedGroups.ToArray() : null;
        }

        var groupsInfo = casdoorGroups != null && casdoorGroups.Length > 0 
            ? $" 组织: [{string.Join(", ", casdoorGroups)}]" 
            : "";
        
        Console.WriteLine($"  -> 同步用户: {targetOwner}/{userName} ({u.DisplayName}){groupsInfo}");

        // 构建用户数据,直接包含groups和password
        var userData = new 
        { 
            owner = targetOwner, 
            name = userName,
            displayName = u.DisplayName,
            externalId = u.Id,
            email = u.Email,
            phone = u.Phone,
            gender = u.Gender,
            language = u.Language,
            type = u.Type,
            tag = u.Department,
            affiliation = u.CompanyName,
            groups = casdoorGroups ?? Array.Empty<string>(),
            password = u.PasswordMd5  // MD5密码哈希
        };
        
        var createResp = PostAsync("/api/add-user", userData).GetAwaiter().GetResult();
        
        if (IsOk(createResp))
        {
            Console.WriteLine($"      ✓ 用户已创建(含{casdoorGroups?.Length ?? 0}个组织): {targetOwner}/{userName}");
            CacheUser(targetOwner, u.Id, userName);
            return;
        }

        var errMsg = GetErrorMsg(createResp);
        
        if (errMsg.Contains("Duplicate") || errMsg.Contains("duplicate") || 
            errMsg.Contains("已存在") || errMsg.Contains("PRIMARY") ||
            errMsg.Contains("Username already exists") || errMsg.Contains("already exists"))
        {
            Console.WriteLine($"      ✓ 用户已存在, 尝试更新...");
            // 用户已存在, 尝试更新。Casdoor的 `update-user` 会覆盖所有字段。
            var updateResp = PostAsync("/api/update-user", userData).GetAwaiter().GetResult();
            if (IsOk(updateResp))
            {
                Console.WriteLine($"      ✓ 用户已更新(含{casdoorGroups?.Length ?? 0}个组织): {targetOwner}/{userName}");
            }
            else
            {
                Console.WriteLine($"      ⚠ 更新用户失败: {GetErrorMsg(updateResp)}");
            }
            CacheUser(targetOwner, u.Id, userName);
            return;
        }

        // 其他错误才抛出异常
        throw new InvalidOperationException($"创建用户 {targetOwner}/{userName} 失败: {errMsg}");
    }

    public void PurgeExceptOwner(string owner)
    {
        var keep = (owner ?? string.Empty).Trim();
        var orgsResp = GetAsync("/api/get-organizations").GetAwaiter().GetResult();
        var orgs = new List<string>();

        if (orgsResp.HasValue && orgsResp.Value.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in data.EnumerateArray())
            {
                var name = o.TryGetProperty("name", out var on) ? (on.GetString() ?? string.Empty) : string.Empty;
                if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, keep, StringComparison.OrdinalIgnoreCase))
                {
                    orgs.Add(name);
                }
            }
        }

        foreach (var org in orgs)
        {
            // 删除组织下的所有用户
            try
            {
                var users = GetAsync($"/api/get-users?owner={Uri.EscapeDataString(org)}").GetAwaiter().GetResult();
                if (users.HasValue && users.Value.TryGetProperty("data", out var udata) && udata.ValueKind == JsonValueKind.Array)
                {
                    foreach (var u in udata.EnumerateArray())
                    {
                        var name = u.TryGetProperty("name", out var np) ? (np.GetString() ?? string.Empty) : string.Empty;
                        if (!string.IsNullOrEmpty(name))
                        {
                            PostAsync("/api/delete-user", new { owner = org, name }).GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    警告: 清理组织 {org} 的用户时出错: {ex.Message}");
            }

            // 删除组织下的所有群组
            try
            {
                var groups = GetAsync($"/api/get-groups?owner={Uri.EscapeDataString(org)}").GetAwaiter().GetResult();
                if (groups.HasValue && groups.Value.TryGetProperty("data", out var gdata) && gdata.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in gdata.EnumerateArray())
                    {
                        var name = g.TryGetProperty("name", out var np) ? (np.GetString() ?? string.Empty) : string.Empty;
                        if (!string.IsNullOrEmpty(name))
                        {
                            PostAsync("/api/delete-group", new { owner = org, name }).GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    警告: 清理组织 {org} 的群组时出错: {ex.Message}");
            }
        }

        _userIdCache.Clear();
    }

    public (string Owner, string Name)? ResolveUserKey(string owner, string userId)
    {
        var normalizedOwner = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();
        var map = LoadUsersForOwner(normalizedOwner, false);

        if (map.TryGetValue(userId, out var info))
        {
            return info;
        }

        // 强制重新加载
        map = LoadUsersForOwner(normalizedOwner, true);
        if (map.TryGetValue(userId, out info))
        {
            return info;
        }

        // 在其他 owner 中查找
        foreach (var kv in _userIdCache)
        {
            if (kv.Value.TryGetValue(userId, out info))
            {
                return info;
            }
        }

        return null;
    }

    public void RefreshEnforcer(string owner, string name)
    {
        var normalizedOwner = string.IsNullOrWhiteSpace(owner) ? "built-in" : owner.Trim();
        var payload = new { owner = normalizedOwner, name = name.Trim() };
        var resp = PostAsync("/api/update-enforcer", payload).GetAwaiter().GetResult();
        if (!IsOk(resp))
        {
            Console.WriteLine($"    警告: 刷新执行器 {normalizedOwner}/{name} 失败: {GetErrorMsg(resp)}");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    // ==================== 私有辅助方法 ====================

    private async Task<JsonElement?> GetAsync(string path)
    {
        var url = $"{_endpoint}{path}{(path.Contains('?') ? "&" : "?")}clientId={Uri.EscapeDataString(_clientId)}&clientSecret={Uri.EscapeDataString(_clientSecret)}";
        Console.WriteLine($"  GET {MaskSecret(path)}");

        try
        {
            using var resp = await _http.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"    HTTP {(int)resp.StatusCode} - 响应长度: {json.Length} 字节");

            if (string.IsNullOrWhiteSpace(json))
            {
                return resp.IsSuccessStatusCode ? null : CreateErrorElement($"HTTP {(int)resp.StatusCode}");
            }

            // 检查响应是否为 HTML
            if (json.TrimStart().StartsWith("<"))
            {
                Console.WriteLine($"    警告: 服务器返回 HTML 而非 JSON (前100字符): {json.Substring(0, Math.Min(100, json.Length))}");
                return CreateErrorElement("服务器返回HTML,非预期的JSON响应");
            }

            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    错误: GET请求失败 - {ex.Message}");
            return CreateErrorElement(ex.Message);
        }
    }

    private async Task<JsonElement?> PostAsync(string path, object payload)
    {
        var url = $"{_endpoint}{path}?clientId={Uri.EscapeDataString(_clientId)}&clientSecret={Uri.EscapeDataString(_clientSecret)}";
        
        // 使用显式的 UTF-8 编码选项序列化 JSON
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var body = JsonSerializer.Serialize(payload, options);
        Console.WriteLine($"  POST {MaskSecret(path)}");

        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"    HTTP {(int)resp.StatusCode} - 响应长度: {json.Length} 字节");

            if (string.IsNullOrWhiteSpace(json))
            {
                return resp.IsSuccessStatusCode ? CreateOkElement() : CreateErrorElement($"HTTP {(int)resp.StatusCode}");
            }

            // 检查响应是否为 HTML
            if (json.TrimStart().StartsWith("<"))
            {
                Console.WriteLine($"    警告: 服务器返回 HTML 而非 JSON (前200字符): {json.Substring(0, Math.Min(200, json.Length))}");
                return CreateErrorElement("服务器返回HTML,非预期的JSON响应");
            }

            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    错误: POST请求失败 - {ex.Message}");
            return CreateErrorElement(ex.Message);
        }
    }

    private static bool IsOk(JsonElement? elem)
    {
        if (elem is null) return false;
        try
        {
            return elem.Value.TryGetProperty("status", out var s) && s.GetString() == "ok";
        }
        catch
        {
            return false;
        }
    }

    private static string GetErrorMsg(JsonElement? elem)
    {
        if (elem is null) return "无响应";
        try
        {
            if (elem.Value.TryGetProperty("msg", out var m))
            {
                return m.GetString() ?? "未知错误";
            }
            return elem.Value.ToString();
        }
        catch
        {
            return "解析错误信息失败";
        }
    }

    private static JsonElement CreateErrorElement(string msg)
    {
        var json = JsonSerializer.Serialize(new { status = "error", msg });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static JsonElement CreateOkElement()
    {
        var json = JsonSerializer.Serialize(new { status = "ok" });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static string MaskSecret(string path)
    {
        return path.Contains("clientSecret") ? path.Replace("clientSecret", "***") : path;
    }

    private void CacheUser(string owner, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return;

        var normalizedOwner = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();
        if (!_userIdCache.TryGetValue(normalizedOwner, out var map))
        {
            map = new Dictionary<string, (string Owner, string Name)>(StringComparer.OrdinalIgnoreCase);
            _userIdCache[normalizedOwner] = map;
        }
        map[id] = (normalizedOwner, name);
    }

    private Dictionary<string, (string Owner, string Name)> LoadUsersForOwner(string owner, bool forceReload)
    {
        var normalized = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();

        if (!forceReload && _userIdCache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var map = new Dictionary<string, (string Owner, string Name)>(StringComparer.OrdinalIgnoreCase);
        var resp = GetAsync($"/api/get-users?owner={Uri.EscapeDataString(normalized)}").GetAwaiter().GetResult();

        if (resp is { } root && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var ownerValue = item.TryGetProperty("owner", out var ownerProp) ? ownerProp.GetString() : normalized;

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                {
                    map[id!] = (ownerValue ?? normalized, name!);
                }
            }
        }

        _userIdCache[normalized] = map;
        return map;
    }

    /// <summary>
    /// 导出组织层级关系到CSV文件,方便手动在Casdoor中设置父子关系
    /// </summary>
    public void ExportGroupHierarchy(string filePath)
    {
        using var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("组织ID,组织名称,父组织ID,父组织名称,Casdoor组织名称,Casdoor父组织名称");
        
        foreach (var (id, info) in _groupHierarchy.OrderBy(x => x.Value.DisplayName))
        {
            var parentId = info.ParentId ?? "";
            var parentDisplayName = info.ParentDisplayName ?? "";
            var casdoorName = $"{_defaultOwner}/{id}";
            var casdoorParentName = string.IsNullOrWhiteSpace(parentId) ? "" : $"{_defaultOwner}/{parentId}";
            
            writer.WriteLine($"\"{id}\",\"{info.DisplayName}\",\"{parentId}\",\"{parentDisplayName}\",\"{casdoorName}\",\"{casdoorParentName}\"");
        }
        
        Console.WriteLine($"\n✓ 组织层级关系已导出到: {filePath}");
        Console.WriteLine($"  共 {_groupHierarchy.Count} 个组织");
        Console.WriteLine($"  其中 {_groupHierarchy.Count(x => !string.IsNullOrWhiteSpace(x.Value.ParentId))} 个有上级组织");
    }

    /// <summary>
    /// 导出组织成员关系到CSV文件,方便手动在Casdoor中配置成员
    /// </summary>
    public void ExportGroupMembership(string filePath, Dictionary<string, HashSet<string>> groupMembers)
    {
        // This method is no longer called by SyncService but is kept for potential manual export needs.
        using var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("组织ID,组织名称,成员数量,成员列表(Casdoor用户名)");
        
        var totalMembers = 0;
        foreach (var (groupId, members) in groupMembers.OrderBy(x => x.Key))
        {
            var displayName = _groupHierarchy.TryGetValue(groupId, out var info) ? info.DisplayName : groupId;
            var memberList = string.Join("; ", members.OrderBy(x => x));
            
            writer.WriteLine($"\"{groupId}\",\"{displayName}\",\"{members.Count}\",\"{memberList}\"");
            totalMembers += members.Count;
        }
        
        Console.WriteLine($"\n✓ 组织成员关系已导出到: {filePath}");
        Console.WriteLine($"  共 {groupMembers.Count} 个组织有成员");
        Console.WriteLine($"  总计 {totalMembers} 个成员关系");
    }
}
