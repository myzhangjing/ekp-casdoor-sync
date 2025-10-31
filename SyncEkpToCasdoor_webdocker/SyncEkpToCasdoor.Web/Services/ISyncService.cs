namespace SyncEkpToCasdoor.Web.Services;

/// <summary>
/// 同步服务接口
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// 执行全量同步
    /// </summary>
    Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行增量同步
    /// </summary>
    Task<SyncResult> SyncIncrementalAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 应用优化视图
    /// </summary>
    Task<bool> ApplyOptimizedViewsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询用户
    /// </summary>
    Task<UserInfo?> PeekUserAsync(string userName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询部门成员
    /// </summary>
    Task<List<UserInfo>> PeekMembershipAsync(string deptName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取同步状态
    /// </summary>
    Task<SyncState> GetSyncStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 同步结果
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UsersProcessed { get; set; }
    public int OrganizationsProcessed { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 用户信息
/// </summary>
public class UserInfo
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DeptId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Affiliation { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
}

/// <summary>
/// 同步状态
/// </summary>
public class SyncState
{
    public DateTime? LastFullSync { get; set; }
    public DateTime? LastIncrementalSync { get; set; }
    public string? LastSyncType { get; set; }
    public bool IsRunning { get; set; }
}
