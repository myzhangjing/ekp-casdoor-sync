using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using SyncEkpToCasdoor.UI.Models;

namespace SyncEkpToCasdoor.UI.Services;

/// <summary>
/// 数据查看服务
/// </summary>
public class DataViewService
{
    private readonly HttpClient _httpClient;

    public DataViewService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    #region EKP 数据查询

    /// <summary>
    /// 获取 EKP 组织列表
    /// </summary>
    public async Task<List<OrganizationInfo>> GetEkpOrganizationsAsync(SyncConfiguration config)
    {
        var organizations = new List<OrganizationInfo>();

        try
        {
            using var connection = new SqlConnection(config.EkpConnectionString);
            await connection.OpenAsync();

            // 使用视图查询组织数据
            var query = @"
                SELECT 
                    id as Id,
                    name as Name,
                    display_name as DisplayName,
                    parent_id as ParentId,
                    type as Type,
                    is_enabled as IsEnabled
                FROM vw_org_structure_sync
                WHERE is_enabled = 1
                ORDER BY display_name";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                organizations.Add(new OrganizationInfo
                {
                    Id = reader["Id"].ToString() ?? "",
                    Name = reader["Name"].ToString() ?? "",
                    DisplayName = reader["DisplayName"] == DBNull.Value ? reader["Name"].ToString() ?? "" : reader["DisplayName"].ToString() ?? "",
                    Type = reader["Type"] == DBNull.Value ? "" : reader["Type"].ToString() ?? "",
                    ParentId = reader["ParentId"] == DBNull.Value ? null : reader["ParentId"].ToString(),
                    Order = 0, // 视图中没有 depth 或 order 字段,使用默认值
                    IsEnabled = reader["IsEnabled"] == DBNull.Value ? false : Convert.ToBoolean(reader["IsEnabled"]),
                    Source = "EKP",
                    SyncStatus = "EKP 数据"
                });
            }

            // 获取每个组织的成员数量
            foreach (var org in organizations)
            {
                org.MemberCount = await GetEkpOrganizationMemberCountAsync(connection, org.Id);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取 EKP 组织列表失败: {ex.Message}", ex);
        }

        return organizations;
    }

    /// <summary>
    /// 获取 EKP 用户列表
    /// </summary>
    public async Task<List<UserInfo>> GetEkpUsersAsync(SyncConfiguration config, string? organizationId = null)
    {
        var users = new List<UserInfo>();

        try
        {
            using var connection = new SqlConnection(config.EkpConnectionString);
            await connection.OpenAsync();

            // 使用视图查询用户数据
            var query = @"
                SELECT 
                    id as Id,
                    username as LoginName,
                    display_name as DisplayName,
                    email as Email,
                    phone as Phone,
                    dept_id as OrganizationId,
                    company_name as OrganizationName,
                    owner as Owner
                FROM vw_casdoor_users_sync";

            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                query += " WHERE dept_id = @OrgId";
            }

            query += " ORDER BY display_name";

            using var command = new SqlCommand(query, connection);
            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                command.Parameters.AddWithValue("@OrgId", organizationId);
            }

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new UserInfo
                {
                    Id = reader["Id"].ToString() ?? "",
                    Name = reader["LoginName"] == DBNull.Value ? "" : reader["LoginName"].ToString() ?? "",
                    DisplayName = reader["DisplayName"] == DBNull.Value ? reader["LoginName"].ToString() ?? "" : reader["DisplayName"].ToString() ?? "",
                    Email = reader["Email"] == DBNull.Value ? null : reader["Email"].ToString(),
                    Phone = reader["Phone"] == DBNull.Value ? null : reader["Phone"].ToString(),
                    OrganizationId = reader["OrganizationId"] == DBNull.Value ? null : reader["OrganizationId"].ToString(),
                    OrganizationName = reader["OrganizationName"] == DBNull.Value ? null : reader["OrganizationName"].ToString(),
                    IsEnabled = true, // 视图中没有 is_enabled 字段,默认为 true
                    Source = "EKP",
                    SyncStatus = "EKP 数据"
                });
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取 EKP 用户列表失败: {ex.Message}", ex);
        }

        return users;
    }

    private async Task<int> GetEkpOrganizationMemberCountAsync(SqlConnection connection, string organizationId)
    {
        try
        {
            // 使用视图查询组织成员数量
            var query = @"
                SELECT COUNT(*) 
                FROM vw_casdoor_users_sync
                WHERE dept_id = @OrgId";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@OrgId", organizationId);
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region Casdoor 数据查询

    /// <summary>
    /// 获取 Casdoor 组织列表
    /// </summary>
    public async Task<List<OrganizationInfo>> GetCasdoorOrganizationsAsync(SyncConfiguration config)
    {
        var organizations = new List<OrganizationInfo>();

        try
        {
            // Casdoor API: 获取群组列表 (Groups,不是 Organizations)
            // 群组才是对应 EKP 部门的概念
            var url = $"{config.CasdoorEndpoint}/api/get-groups?owner={config.CasdoorOwner}&clientId={config.CasdoorClientId}&clientSecret={config.CasdoorClientSecret}";
            
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 请求 URL: {url}");
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 响应状态: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 响应内容: {content.Substring(0, Math.Min(500, content.Length))}...");
            
            response.EnsureSuccessStatusCode();

            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                // 检查 data 是否为 null 或空数组
                if (dataElement.ValueKind != JsonValueKind.Null && dataElement.ValueKind == JsonValueKind.Array)
                {
                    var arrayLength = dataElement.GetArrayLength();
                    System.Diagnostics.Debug.WriteLine($"[Casdoor API] 组织数量: {arrayLength}");
                    
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        var org = new OrganizationInfo
                        {
                            Source = "Casdoor",
                            SyncStatus = "Casdoor 数据"
                        };

                        if (item.TryGetProperty("name", out var name))
                            org.Id = org.Name = name.GetString() ?? "";

                        if (item.TryGetProperty("displayName", out var displayName))
                            org.DisplayName = displayName.GetString() ?? org.Name;

                        // 群组可能没有 createdTime,尝试获取但不强制要求
                        if (item.TryGetProperty("createdTime", out var createdTime))
                        {
                            var timeStr = createdTime.GetString();
                            if (!string.IsNullOrEmpty(timeStr))
                            {
                                if (DateTime.TryParse(timeStr, out var dt))
                                    org.CreatedTime = dt;
                            }
                        }
                        
                        // 群组特有字段: parentId (可用于树形结构)
                        if (item.TryGetProperty("parentId", out var parentId))
                        {
                            var parentIdStr = parentId.GetString();
                            if (!string.IsNullOrEmpty(parentIdStr))
                                org.ParentId = parentIdStr;
                        }
                        
                        // isEnabled 字段
                        if (item.TryGetProperty("isEnabled", out var isEnabled))
                        {
                            org.IsEnabled = isEnabled.GetBoolean();
                        }

                        organizations.Add(org);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Casdoor API] data 字段为空或不是数组: {dataElement.ValueKind}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Casdoor API] 响应中没有 data 字段");
            }

            // 获取每个组织的成员数量
            foreach (var org in organizations)
            {
                org.MemberCount = await GetCasdoorOrganizationMemberCountAsync(config, org.Name);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"获取 Casdoor 组织列表失败: {ex.Message}\n\n详细信息:\n{ex}";
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 错误: {errorMsg}");
            throw new Exception(errorMsg, ex);
        }

        return organizations;
    }

    /// <summary>
    /// 获取 Casdoor 用户列表
    /// </summary>
    public async Task<List<UserInfo>> GetCasdoorUsersAsync(SyncConfiguration config, string? organizationName = null)
    {
        var users = new List<UserInfo>();

        try
        {
            // Casdoor API: 获取用户列表 (使用 clientId 和 clientSecret 认证)
            var owner = organizationName ?? config.CasdoorOwner;
            var url = $"{config.CasdoorEndpoint}/api/get-users?owner={owner}&clientId={config.CasdoorClientId}&clientSecret={config.CasdoorClientSecret}";
            
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 请求 URL: {url}");
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 响应状态: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 响应内容: {content.Substring(0, Math.Min(500, content.Length))}...");
            
            response.EnsureSuccessStatusCode();

            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                // 检查 data 是否为 null 或空数组
                if (dataElement.ValueKind != JsonValueKind.Null && dataElement.ValueKind == JsonValueKind.Array)
                {
                    var arrayLength = dataElement.GetArrayLength();
                    System.Diagnostics.Debug.WriteLine($"[Casdoor API] 用户数量: {arrayLength}");
                    
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        var user = new UserInfo
                        {
                            Source = "Casdoor",
                            SyncStatus = "Casdoor 数据"
                        };

                        if (item.TryGetProperty("name", out var name))
                            user.Name = name.GetString() ?? "";

                        if (item.TryGetProperty("id", out var id))
                            user.Id = id.GetString() ?? user.Name;

                        if (item.TryGetProperty("displayName", out var displayName))
                            user.DisplayName = displayName.GetString() ?? user.Name;

                        if (item.TryGetProperty("email", out var email))
                            user.Email = email.GetString();

                        if (item.TryGetProperty("phone", out var phone))
                            user.Phone = phone.GetString();

                        if (item.TryGetProperty("owner", out var ownerProp))
                            user.OrganizationName = ownerProp.GetString();

                        if (item.TryGetProperty("isAdmin", out var isAdmin))
                            user.IsAdmin = isAdmin.GetBoolean();

                        if (item.TryGetProperty("createdTime", out var createdTime))
                        {
                            var timeStr = createdTime.GetString();
                            if (!string.IsNullOrEmpty(timeStr))
                                user.CreatedTime = DateTime.Parse(timeStr);
                        }

                        if (item.TryGetProperty("avatar", out var avatar))
                            user.Avatar = avatar.GetString();

                        users.Add(user);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Casdoor API] data 字段为空或不是数组: {dataElement.ValueKind}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Casdoor API] 响应中没有 data 字段");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"获取 Casdoor 用户列表失败: {ex.Message}\n\n详细信息:\n{ex}";
            System.Diagnostics.Debug.WriteLine($"[Casdoor API] 错误: {errorMsg}");
            throw new Exception(errorMsg, ex);
        }

        return users;
    }

    private async Task<int> GetCasdoorOrganizationMemberCountAsync(SyncConfiguration config, string organizationName)
    {
        try
        {
            var users = await GetCasdoorUsersAsync(config, organizationName);
            return users.Count;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<string> GetCasdoorAccessTokenAsync(SyncConfiguration config)
    {
        // 简化版：实际应该实现完整的 OAuth2 流程
        // 这里假设使用 Client Credentials 流程
        try
        {
            var url = $"{config.CasdoorEndpoint}/api/login/oauth/access_token";
            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config.CasdoorClientId,
                ["client_secret"] = config.CasdoorClientSecret,
                ["scope"] = "read"
            };

            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(requestData));
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("access_token", out var token))
            {
                return token.GetString() ?? "";
            }

            // 如果 API 不需要 token，返回空字符串
            return "";
        }
        catch
        {
            // 某些 Casdoor API 可能不需要认证，返回空字符串
            return "";
        }
    }

    #endregion

    #region Casdoor 数据清空

    /// <summary>
    /// 清空 Casdoor 所有组织和用户 (仅用于测试环境)
    /// </summary>
    public async Task<(int DeletedUsers, int DeletedOrgs)> ClearCasdoorDataAsync(SyncConfiguration config)
    {
        int deletedUsers = 0;
        int deletedOrgs = 0;

        try
        {
            // 1. 删除所有用户 (除了 admin)
            var users = await GetCasdoorUsersAsync(config);
            Console.WriteLine($"[清空] 找到 {users.Count} 个用户");
            
            foreach (var user in users.Where(u => u.Name != "admin" && u.Name != "built-in/admin"))
            {
                try
                {
                    Console.WriteLine($"[清空] 正在删除用户: {user.Name} ({user.DisplayName})");
                    await DeleteCasdoorUserAsync(config, user.Name);
                    deletedUsers++;
                    Console.WriteLine($"[清空] ✓ 已删除用户: {user.Name}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"删除用户 {user.Name} 失败: {ex.Message}";
                    Console.WriteLine($"[清空] ✗ {errorMsg}");
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    // 继续删除其他用户,不中断
                }
            }

            // 2. 删除所有群组 (除了根组织)
            var orgs = await GetCasdoorOrganizationsAsync(config);
            Console.WriteLine($"[清空] 找到 {orgs.Count} 个群组");
            
            foreach (var org in orgs.Where(o => !o.Name.Equals(config.CasdoorOwner, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    Console.WriteLine($"[清空] 正在删除群组: {org.Name} ({org.DisplayName})");
                    await DeleteCasdoorGroupAsync(config, org.Name);
                    deletedOrgs++;
                    Console.WriteLine($"[清空] ✓ 已删除群组: {org.Name}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"删除群组 {org.Name} 失败: {ex.Message}";
                    Console.WriteLine($"[清空] ✗ {errorMsg}");
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    // 继续删除其他群组,不中断
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"清空 Casdoor 数据失败: {ex.Message}", ex);
        }

        return (deletedUsers, deletedOrgs);
    }

    private async Task DeleteCasdoorUserAsync(SyncConfiguration config, string username)
    {
        // 使用 URL 参数认证 (clientId 和 clientSecret)
        var url = $"{config.CasdoorEndpoint}/api/delete-user?clientId={config.CasdoorClientId}&clientSecret={config.CasdoorClientSecret}";
        
        var requestData = new
        {
            owner = config.CasdoorOwner,
            name = username
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestData),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteCasdoorGroupAsync(SyncConfiguration config, string groupName)
    {
        // 使用 URL 参数认证删除群组 (clientId 和 clientSecret)
        var url = $"{config.CasdoorEndpoint}/api/delete-group?clientId={config.CasdoorClientId}&clientSecret={config.CasdoorClientSecret}";
        
        var requestData = new
        {
            owner = config.CasdoorOwner,
            name = groupName
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestData),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[删除群组] API 响应: {content}");
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region 数据比对

    /// <summary>
    /// 比对 EKP 和 Casdoor 的组织数据
    /// </summary>
    public async Task<(List<OrganizationInfo> EkpOnly, List<OrganizationInfo> CasdoorOnly, List<OrganizationInfo> Both)> 
        CompareOrganizationsAsync(SyncConfiguration config)
    {
        var ekpOrgs = await GetEkpOrganizationsAsync(config);
        var casdoorOrgs = await GetCasdoorOrganizationsAsync(config);

        // 使用 GroupBy 处理重复键
        var ekpDict = ekpOrgs.GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
        var casdoorDict = casdoorOrgs.GroupBy(o => o.Name).ToDictionary(g => g.Key, g => g.First());

        var ekpOnly = new List<OrganizationInfo>();
        var casdoorOnly = new List<OrganizationInfo>();
        var both = new List<OrganizationInfo>();

        // EKP 中的组织
        foreach (var ekpOrg in ekpOrgs)
        {
            if (casdoorDict.ContainsKey(ekpOrg.Id))
            {
                ekpOrg.SyncStatus = "已同步";
                both.Add(ekpOrg);
            }
            else
            {
                ekpOrg.SyncStatus = "仅在 EKP";
                ekpOnly.Add(ekpOrg);
            }
        }

        // Casdoor 中的组织
        foreach (var casdoorOrg in casdoorOrgs)
        {
            if (!ekpDict.ContainsKey(casdoorOrg.Name))
            {
                casdoorOrg.SyncStatus = "仅在 Casdoor";
                casdoorOnly.Add(casdoorOrg);
            }
        }

        return (ekpOnly, casdoorOnly, both);
    }

    /// <summary>
    /// 比对 EKP 和 Casdoor 的用户数据
    /// </summary>
    public async Task<(List<UserInfo> EkpOnly, List<UserInfo> CasdoorOnly, List<UserInfo> Both)> 
        CompareUsersAsync(SyncConfiguration config, string? organizationId = null)
    {
        var ekpUsers = await GetEkpUsersAsync(config, organizationId);
        var casdoorUsers = await GetCasdoorUsersAsync(config, organizationId);

        // 使用 GroupBy 处理重复的 Name,只保留第一个
        var ekpDict = ekpUsers.GroupBy(u => u.Name).ToDictionary(g => g.Key, g => g.First());
        var casdoorDict = casdoorUsers.GroupBy(u => u.Name).ToDictionary(g => g.Key, g => g.First());

        var ekpOnly = new List<UserInfo>();
        var casdoorOnly = new List<UserInfo>();
        var both = new List<UserInfo>();

        // EKP 中的用户
        foreach (var ekpUser in ekpUsers)
        {
            if (casdoorDict.ContainsKey(ekpUser.Name))
            {
                ekpUser.SyncStatus = "已同步";
                both.Add(ekpUser);
            }
            else
            {
                ekpUser.SyncStatus = "仅在 EKP";
                ekpOnly.Add(ekpUser);
            }
        }

        // Casdoor 中的用户
        foreach (var casdoorUser in casdoorUsers)
        {
            if (!ekpDict.ContainsKey(casdoorUser.Name))
            {
                casdoorUser.SyncStatus = "仅在 Casdoor";
                casdoorOnly.Add(casdoorUser);
            }
        }

        return (ekpOnly, casdoorOnly, both);
    }

    #endregion
}
