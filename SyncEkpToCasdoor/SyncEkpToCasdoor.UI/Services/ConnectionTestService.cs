using System;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using SyncEkpToCasdoor.UI.Models;

namespace SyncEkpToCasdoor.UI.Services;

/// <summary>
/// 连接测试服务
/// </summary>
public class ConnectionTestService
{
    private readonly HttpClient _httpClient;

    public ConnectionTestService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// 测试 EKP SQL Server 连接
    /// </summary>
    public async Task<ConnectionTestResult> TestEkpDatabaseAsync(SyncConfiguration config)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(config.EkpConnectionString);
            await conn.OpenAsync();

            // 测试查询组织数量
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM vw_org_structure_sync", conn);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

            sw.Stop();
            return new ConnectionTestResult
            {
                Success = true,
                Message = $"连接成功！检测到 {count} 个组织",
                Details = $"服务器: {config.EkpServer}:{config.EkpPort}\n数据库: {config.EkpDatabase}\n延迟: {sw.ElapsedMilliseconds}ms",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = "连接失败",
                Details = $"错误: {ex.Message}\n\n请检查:\n- 服务器地址和端口是否正确\n- 用户名密码是否正确\n- SQL Server 是否允许远程连接\n- 防火墙是否开放端口",
                Duration = sw.Elapsed
            };
        }
    }

    /// <summary>
    /// 测试 Casdoor API 连接
    /// </summary>
    public async Task<ConnectionTestResult> TestCasdoorApiAsync(SyncConfiguration config)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 测试获取组织列表
            var url = $"{config.CasdoorEndpoint.TrimEnd('/')}/api/get-organizations?owner={config.CasdoorOwner}&clientId={config.CasdoorClientId}&clientSecret={config.CasdoorClientSecret}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CasdoorApiResponse>();
            
            sw.Stop();
            
            if (result?.Status == "ok")
            {
                return new ConnectionTestResult
                {
                    Success = true,
                    Message = $"API 连接成功！",
                    Details = $"接口地址: {config.CasdoorEndpoint}\nOwner: {config.CasdoorOwner}\n延迟: {sw.ElapsedMilliseconds}ms",
                    Duration = sw.Elapsed
                };
            }
            else
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "API 返回错误",
                    Details = $"返回消息: {result?.Msg ?? "未知错误"}",
                    Duration = sw.Elapsed
                };
            }
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = "API 连接失败",
                Details = $"错误: {ex.Message}\n\n请检查:\n- Casdoor 地址是否正确\n- Client ID 和 Secret 是否正确\n- 网络是否畅通",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = "发生未知错误",
                Details = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    /// <summary>
    /// 测试 Casdoor 数据库连接
    /// </summary>
    public async Task<ConnectionTestResult> TestCasdoorDatabaseAsync(SyncConfiguration config)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new MySqlConnection(config.CasdoorDbConnectionString);
            await conn.OpenAsync();

            // 测试查询组织数量
            await using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `group` WHERE owner=@owner", conn);
            cmd.Parameters.AddWithValue("@owner", config.CasdoorOwner);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            sw.Stop();
            return new ConnectionTestResult
            {
                Success = true,
                Message = $"数据库连接成功！检测到 {count} 个组织",
                Details = $"服务器: {config.CasdoorDbHost}:{config.CasdoorDbPort}\n数据库: {config.CasdoorDbName}\n延迟: {sw.ElapsedMilliseconds}ms",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = "数据库连接失败",
                Details = $"错误: {ex.Message}\n\n请检查:\n- MySQL 服务器地址和端口是否正确\n- 用户名密码是否正确\n- 数据库是否存在\n- 防火墙是否开放端口",
                Duration = sw.Elapsed
            };
        }
    }

    /// <summary>
    /// 验证配置完整性
    /// </summary>
    public ValidationResult ValidateConfiguration(SyncConfiguration config)
    {
        var result = new ValidationResult { IsValid = true };

        // 验证 EKP 配置
        if (string.IsNullOrWhiteSpace(config.EkpServer))
            result.Errors.Add("EKP 服务器地址不能为空");
        if (string.IsNullOrWhiteSpace(config.EkpDatabase))
            result.Errors.Add("EKP 数据库名不能为空");
        if (string.IsNullOrWhiteSpace(config.EkpUsername))
            result.Errors.Add("EKP 用户名不能为空");
        if (string.IsNullOrWhiteSpace(config.EkpPassword))
            result.Warnings.Add("EKP 密码为空，可能导致连接失败");

        // 验证 Casdoor API 配置
        if (string.IsNullOrWhiteSpace(config.CasdoorEndpoint))
            result.Errors.Add("Casdoor 接口地址不能为空");
        else if (!Uri.TryCreate(config.CasdoorEndpoint, UriKind.Absolute, out _))
            result.Errors.Add("Casdoor 接口地址格式不正确");
        
        if (string.IsNullOrWhiteSpace(config.CasdoorClientId))
            result.Errors.Add("Casdoor Client ID 不能为空");
        if (string.IsNullOrWhiteSpace(config.CasdoorClientSecret))
            result.Errors.Add("Casdoor Client Secret 不能为空");
        if (string.IsNullOrWhiteSpace(config.CasdoorOwner))
            result.Errors.Add("Casdoor Owner 不能为空");

        // 验证同步规则
        if (!config.SyncOrganizations && !config.SyncUsers)
            result.Warnings.Add("未启用任何同步项，不会执行同步操作");

        // 验证调度配置
        if (config.EnableSchedule)
        {
            if (config.ScheduleMode == ScheduleMode.Interval && config.IntervalHours <= 0)
                result.Errors.Add("间隔时间必须大于 0");
            if (config.ScheduleMode == ScheduleMode.Daily && config.DailyTime.TotalHours >= 24)
                result.Errors.Add("每日执行时间必须在 0-24 小时之间");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// 获取 EKP 数据预览
    /// </summary>
    public async Task<DataPreview> GetEkpDataPreviewAsync(SyncConfiguration config)
    {
        try
        {
            await using var conn = new SqlConnection(config.EkpConnectionString);
            await conn.OpenAsync();

            var preview = new DataPreview();

            // 组织统计
            await using (var cmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN parent_id IS NULL THEN 1 ELSE 0 END) as RootCount
                FROM vw_org_structure_sync", conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    preview.TotalOrganizations = reader.GetInt32(0);
                    preview.RootOrganizations = reader.GetInt32(1);
                    // 不再计算 MaxDepth,因为视图没有 depth 字段
                    preview.MaxOrgDepth = 0;
                }
            }

            // 用户统计
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM vw_casdoor_users_sync", conn))
            {
                preview.TotalUsers = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            // 示例组织
            await using (var cmd = new SqlCommand("SELECT TOP 5 id, name, display_name, parent_id FROM vw_org_structure_sync ORDER BY name", conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var parentId = reader.IsDBNull(3) ? "根节点" : reader.GetString(3);
                    preview.SampleOrganizations.Add(new SampleData
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        DisplayName = reader.GetString(2),
                        Extra = $"上级: {parentId}"
                    });
                }
            }

            return preview;
        }
        catch (Exception ex)
        {
            throw new Exception($"获取数据预览失败: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Casdoor API 响应
/// </summary>
internal class CasdoorApiResponse
{
    public string? Status { get; set; }
    public string? Msg { get; set; }
}

/// <summary>
/// 数据预览
/// </summary>
public class DataPreview
{
    public int TotalOrganizations { get; set; }
    public int RootOrganizations { get; set; }
    public int MaxOrgDepth { get; set; }
    public int TotalUsers { get; set; }
    public List<SampleData> SampleOrganizations { get; set; } = new();
    public List<SampleData> SampleUsers { get; set; } = new();
}

public class SampleData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Extra { get; set; } = "";
}
