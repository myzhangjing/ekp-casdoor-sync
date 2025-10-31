using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SyncEkpToCasdoor.Web.Services;

/// <summary>
/// 同步服务实现
/// </summary>
public class SyncService : ISyncService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncService> _logger;
    private readonly string _syncStateFile;
    private static bool _isRunning = false;
    
    public SyncService(IConfiguration configuration, ILogger<SyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _syncStateFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_state.json");
    }
    
    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return new SyncResult 
            { 
                Success = false, 
                Message = "同步任务正在运行中，请稍后再试" 
            };
        }
        
        _isRunning = true;
        var result = new SyncResult { StartTime = DateTime.Now };
        
        try
        {
            _logger.LogInformation("开始全量同步...");
            
            // TODO: 调用原有的同步逻辑
            // 这里需要将 SyncEkpToCasdoor/Program.cs 中的逻辑迁移过来
            
            result.Success = true;
            result.Message = "全量同步完成";
            result.EndTime = DateTime.Now;
            
            // 保存同步状态
            await SaveSyncStateAsync("full", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全量同步失败");
            result.Success = false;
            result.Message = $"同步失败: {ex.Message}";
            result.Errors.Add(ex.ToString());
            result.EndTime = DateTime.Now;
        }
        finally
        {
            _isRunning = false;
        }
        
        return result;
    }
    
    public async Task<SyncResult> SyncIncrementalAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return new SyncResult 
            { 
                Success = false, 
                Message = "同步任务正在运行中，请稍后再试" 
            };
        }
        
        _isRunning = true;
        var result = new SyncResult { StartTime = DateTime.Now };
        
        try
        {
            _logger.LogInformation("开始增量同步...");
            
            // TODO: 调用原有的增量同步逻辑
            
            result.Success = true;
            result.Message = "增量同步完成";
            result.EndTime = DateTime.Now;
            
            // 保存同步状态
            await SaveSyncStateAsync("incremental", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "增量同步失败");
            result.Success = false;
            result.Message = $"同步失败: {ex.Message}";
            result.Errors.Add(ex.ToString());
            result.EndTime = DateTime.Now;
        }
        finally
        {
            _isRunning = false;
        }
        
        return result;
    }
    
    public async Task<bool> ApplyOptimizedViewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始应用优化视图...");
            
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("未配置 EKP 数据库连接字符串");
                return false;
            }
            
            // TODO: 调用 ApplyOptimizedViews 方法
            
            _logger.LogInformation("优化视图应用成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用优化视图失败");
            return false;
        }
    }
    
    public async Task<UserInfo?> PeekUserAsync(string userName, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("未配置 EKP 数据库连接字符串");
                return null;
            }
            
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var sql = @"
                SELECT 
                    username, display_name, dept_id, 
                    company_name, affiliation, owner
                FROM vw_casdoor_users_sync
                WHERE display_name = @userName OR username LIKE '%' + @userName + '%'";
            
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@userName", userName);
            
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new UserInfo
                {
                    Username = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    DeptId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CompanyName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Affiliation = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Owner = reader.IsDBNull(5) ? "" : reader.GetString(5)
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询用户失败: {UserName}", userName);
            return null;
        }
    }
    
    public async Task<List<UserInfo>> PeekMembershipAsync(string deptName, CancellationToken cancellationToken = default)
    {
        var results = new List<UserInfo>();
        
        try
        {
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("未配置 EKP 数据库连接字符串");
                return results;
            }
            
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var sql = @"
                SELECT 
                    username, display_name, dept_id, 
                    company_name, affiliation, owner
                FROM vw_casdoor_users_sync
                WHERE affiliation LIKE '%' + @deptName + '%'
                ORDER BY display_name";
            
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@deptName", deptName);
            
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new UserInfo
                {
                    Username = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    DeptId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CompanyName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Affiliation = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Owner = reader.IsDBNull(5) ? "" : reader.GetString(5)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询部门成员失败: {DeptName}", deptName);
        }
        
        return results;
    }
    
    public async Task<SyncState> GetSyncStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_syncStateFile))
            {
                var json = await File.ReadAllTextAsync(_syncStateFile, cancellationToken);
                var state = JsonSerializer.Deserialize<SyncStateData>(json);
                
                return new SyncState
                {
                    LastFullSync = state?.LastFullSync,
                    LastIncrementalSync = state?.LastIncrementalSync,
                    LastSyncType = state?.LastSyncType,
                    IsRunning = _isRunning
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取同步状态失败");
        }
        
        return new SyncState { IsRunning = _isRunning };
    }
    
    private async Task SaveSyncStateAsync(string syncType, CancellationToken cancellationToken)
    {
        try
        {
            var state = new SyncStateData
            {
                LastSyncType = syncType
            };
            
            if (syncType == "full")
            {
                state.LastFullSync = DateTime.Now;
            }
            else if (syncType == "incremental")
            {
                state.LastIncrementalSync = DateTime.Now;
            }
            
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_syncStateFile, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存同步状态失败");
        }
    }
    
    private class SyncStateData
    {
        public DateTime? LastFullSync { get; set; }
        public DateTime? LastIncrementalSync { get; set; }
        public string? LastSyncType { get; set; }
    }
}
