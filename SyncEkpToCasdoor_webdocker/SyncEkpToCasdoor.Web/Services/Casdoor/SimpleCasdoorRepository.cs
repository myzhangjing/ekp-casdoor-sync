using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SyncEkpToCasdoor.Web.Models;

namespace SyncEkpToCasdoor.Web.Services;

/// <summary>
/// 轻量 Casdoor 调用层：直接面向 Casdoor REST API（不依赖 SDK），带基本健壮性处理
/// </summary>
internal sealed class SimpleCasdoorRepository : ICasdoorRepository
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _defaultOwner;

    // owner => (ekpUserId -> (Owner, Name))
    private readonly Dictionary<string, Dictionary<string, (string Owner, string Name)>> _userIdCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _syncedGroupIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string DisplayName, string? ParentId, string? ParentDisplayName)> _groupHierarchy = new(StringComparer.OrdinalIgnoreCase);

    // 从 Casdoor 加载的 “EKP GroupId -> owner/name” 映射
    private Dictionary<string, string>? _casdoorGroupMapping = null;

    public SimpleCasdoorRepository(AppConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.CasdoorEndpoint) ||
            string.IsNullOrWhiteSpace(cfg.ClientId) ||
            string.IsNullOrWhiteSpace(cfg.ClientSecret))
        {
            throw new InvalidOperationException("缺少 Casdoor 接口配置: CASDOOR_ENDPOINT / CASDOOR_CLIENT_ID / CASDOOR_CLIENT_SECRET");
        }

        _endpoint = cfg.CasdoorEndpoint.TrimEnd('/');
        _clientId = cfg.ClientId!;
        _clientSecret = cfg.ClientSecret!;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _defaultOwner = string.IsNullOrWhiteSpace(cfg.DefaultOwner) ? "built-in" : cfg.DefaultOwner;
    }

    public void UpsertGroup(EkpGroup g)
    {
        var owner = string.IsNullOrWhiteSpace(g.Owner) ? _defaultOwner : g.Owner.Trim();

        // Casdoor 期望 parentId = 父级的 name（根节点用 owner）
        string parentIdForApi = string.IsNullOrWhiteSpace(g.ParentId) ? owner : g.ParentId!;

        // 记录层级信息（导出用）
        _groupHierarchy[g.Id] = (g.DisplayName, g.ParentId, null);
        if (!string.IsNullOrWhiteSpace(g.ParentId) && _groupHierarchy.TryGetValue(g.ParentId!, out var parentInfo))
        {
            _groupHierarchy[g.Id] = (g.DisplayName, g.ParentId, parentInfo.DisplayName);
        }

        var createData = new
        {
            owner,
            name = g.Id,                     // 使用 EKP 部门ID 作为 Casdoor name
            displayName = g.DisplayName,
            parentName = parentIdForApi,     // 可选
            parentId = parentIdForApi,       // 关键：父级 name
            type = g.Type,                   // company | department
            isEnabled = g.IsEnabled,
            key = string.IsNullOrWhiteSpace(g.DeptId) ? g.Id : g.DeptId
        };

        var createResp = PostAsync("/api/add-group", createData).GetAwaiter().GetResult();
        if (IsOk(createResp))
        {
            _syncedGroupIds.Add(g.Id);
            return;
        }

        var errMsg = GetErrorMsg(createResp);
        if (IsAlreadyExists(errMsg))
        {
            var updateData = new
            {
                id = $"{owner}/{g.Id}",     // Casdoor 要求 id=owner/name
                owner,
                name = g.Id,
                displayName = g.DisplayName,
                parentName = parentIdForApi,
                parentId = parentIdForApi,
                type = g.Type,
                isEnabled = g.IsEnabled,
                key = string.IsNullOrWhiteSpace(g.DeptId) ? g.Id : g.DeptId
            };
            var updatePath = $"/api/update-group?id={Uri.EscapeDataString(owner + "/" + g.Id)}";
            var updateResp = PostAsync(updatePath, updateData).GetAwaiter().GetResult();
            if (!IsOk(updateResp))
            {
                // 记录但不阻断流程
                Console.WriteLine($"[WARN] 更新组失败: {GetErrorMsg(updateResp)}");
            }
            _syncedGroupIds.Add(g.Id);
            return;
        }

        throw new InvalidOperationException($"创建组 {owner}/{g.Id} 失败: {errMsg}");
    }

    public void LoadCasdoorGroupMapping()
    {
        _casdoorGroupMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resp = GetAsync($"/api/get-groups?owner={Uri.EscapeDataString(_defaultOwner)}").GetAwaiter().GetResult();
        if (resp.HasValue && resp.Value.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var owner = item.TryGetProperty("owner", out var op) ? op.GetString() : _defaultOwner;
                var name = item.TryGetProperty("name", out var np) ? np.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _casdoorGroupMapping[name!] = $"{owner}/{name}";
                }
            }
        }
    }

    public void UpsertUser(EkpUser u, string owner, bool forceOwner, List<string>? groupIds)
    {
        var targetOwner = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();

        // group 映射到 Casdoor 完整名 owner/name
        string[]? casdoorGroups = null;
        if (groupIds is { Count: > 0 } && _casdoorGroupMapping != null)
        {
            casdoorGroups = groupIds.Where(id => !string.IsNullOrWhiteSpace(id) && _casdoorGroupMapping.ContainsKey(id))
                                     .Select(id => _casdoorGroupMapping![id])
                                     .Distinct()
                                     .ToArray();
        }

        var userData = new
        {
            owner = targetOwner,
            name = Slug(u.Id),
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
            password = string.IsNullOrWhiteSpace(u.PasswordMd5) ? null : u.PasswordMd5
        };

        var createResp = PostAsync("/api/add-user", userData).GetAwaiter().GetResult();
        if (IsOk(createResp))
        {
            CacheUser(targetOwner, u.Id, Slug(u.Id));
            return;
        }

        var errMsg = GetErrorMsg(createResp);
        if (IsAlreadyExists(errMsg))
        {
            var updateResp = PostAsync("/api/update-user", userData).GetAwaiter().GetResult();
            if (!IsOk(updateResp))
            {
                Console.WriteLine($"[WARN] 更新用户失败: {GetErrorMsg(updateResp)}");
            }
            CacheUser(targetOwner, u.Id, Slug(u.Id));
            return;
        }

        throw new InvalidOperationException($"创建用户 {targetOwner}/{Slug(u.Id)} 失败: {errMsg}");
    }

    public (string Owner, string Name)? ResolveUserKey(string owner, string userId)
    {
        var normalizedOwner = string.IsNullOrWhiteSpace(owner) ? _defaultOwner : owner.Trim();
        var map = LoadUsersForOwner(normalizedOwner, false);
        if (map.TryGetValue(userId, out var info)) return info;
        map = LoadUsersForOwner(normalizedOwner, true);
        if (map.TryGetValue(userId, out info)) return info;
        foreach (var kv in _userIdCache)
        {
            if (kv.Value.TryGetValue(userId, out info)) return info;
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
            Console.WriteLine($"[WARN] 刷新执行器失败: {GetErrorMsg(resp)}");
        }
    }

    public void PurgeExceptOwner(string owner)
    {
        var keep = (owner ?? string.Empty).Trim();
        try
        {
            var users = GetAsync($"/api/get-users?owner={Uri.EscapeDataString(keep)}").GetAwaiter().GetResult();
            // 可按需扩展清理逻辑
        }
        catch { /* 忽略清理错误 */ }
    }

    public void ExportGroupHierarchy(string filePath)
    {
        using var writer = new System.IO.StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("组ID,组名称,父组ID,父组名称,Casdoor组,父Casdoor组");
        foreach (var (id, info) in _groupHierarchy.OrderBy(x => x.Value.DisplayName))
        {
            var casdoorName = $"{_defaultOwner}/{id}";
            var casdoorParentName = string.IsNullOrWhiteSpace(info.ParentId) ? "" : $"{_defaultOwner}/{info.ParentId}";
            writer.WriteLine($"\"{id}\",\"{info.DisplayName}\",\"{info.ParentId ?? ""}\",\"{info.ParentDisplayName ?? ""}\",\"{casdoorName}\",\"{casdoorParentName}\"");
        }
    }

    public void Dispose() => _http.Dispose();

    // ============== helpers ==============
    private async Task<JsonElement?> GetAsync(string path)
    {
        var url = $"{_endpoint}{path}{(path.Contains('?') ? "&" : "?")}clientId={Uri.EscapeDataString(_clientId)}&clientSecret={Uri.EscapeDataString(_clientSecret)}";
        try
        {
            using var resp = await _http.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return resp.IsSuccessStatusCode ? null : CreateErrorElement($"HTTP {(int)resp.StatusCode}");
            if (json.TrimStart().StartsWith("<"))
                return CreateErrorElement("服务端返回 HTML，非 JSON");
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            return CreateErrorElement(ex.Message);
        }
    }

    private async Task<JsonElement?> PostAsync(string path, object payload)
    {
        var url = $"{_endpoint}{path}?clientId={Uri.EscapeDataString(_clientId)}&clientSecret={Uri.EscapeDataString(_clientSecret)}";
        var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var body = JsonSerializer.Serialize(payload, options);
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return resp.IsSuccessStatusCode ? CreateOkElement() : CreateErrorElement($"HTTP {(int)resp.StatusCode}");
            if (json.TrimStart().StartsWith("<"))
                return CreateErrorElement("服务端返回 HTML，非 JSON");
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            return CreateErrorElement(ex.Message);
        }
    }

    private static bool IsOk(JsonElement? elem)
    {
        if (elem is null) return false;
        try { return elem.Value.TryGetProperty("status", out var s) && s.GetString() == "ok"; }
        catch { return false; }
    }

    private static bool IsAlreadyExists(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        msg = msg.ToLowerInvariant();
        return msg.Contains("duplicate") || msg.Contains("already exists") || msg.Contains("primary") || msg.Contains("已存在");
    }

    private static string GetErrorMsg(JsonElement? elem)
    {
        if (elem is null) return "无响应";
        try
        {
            if (elem.Value.TryGetProperty("msg", out var m)) return m.GetString() ?? "未知错误";
            return elem.Value.ToString();
        }
        catch { return "解析错误"; }
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

    private static string Slug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = input.Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.') sb.Append(ch);
            else sb.Append('-');
        }
        return sb.ToString();
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
        if (!forceReload && _userIdCache.TryGetValue(normalized, out var cached)) return cached;

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
}
