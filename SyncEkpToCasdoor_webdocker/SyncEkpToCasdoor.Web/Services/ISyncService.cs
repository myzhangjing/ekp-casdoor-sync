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
    
    /// <summary>
    /// 测试连接（EKP 和 Casdoor）
    /// </summary>
    Task<ConnectionTestResult> TestConnectionsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 预览同步（只读对比，不写入）
    /// </summary>
    Task<SyncPreview> PreviewSyncAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取公司列表
    /// </summary>
    Task<List<CompanyInfo>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 按公司同步
    /// </summary>
    Task<SyncResult> SyncByCompanyAsync(string companyId, bool incremental, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 同步指定公司 (简化版本,用于定时任务)
    /// </summary>
    Task SyncCompanyAsync(string companyId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取同步日志
    /// </summary>
    Task<List<SyncLog>> GetSyncLogsAsync(int count = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清理孤立数据
    /// </summary>
    Task<int> CleanOrphanDataAsync(string owner, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 导出组织层级
    /// </summary>
    Task<string> ExportOrganizationHierarchyAsync(CancellationToken cancellationToken = default);
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

/// <summary>
/// 连接测试结果
/// </summary>
public class ConnectionTestResult
{
    public bool EkpConnected { get; set; }
    public string EkpMessage { get; set; } = string.Empty;
    public int EkpUsersCount { get; set; }
    public int EkpOrgsCount { get; set; }
    
    public bool CasdoorConnected { get; set; }
    public string CasdoorMessage { get; set; } = string.Empty;
    public string CasdoorEndpoint { get; set; } = string.Empty;
    public string CasdoorOwner { get; set; } = string.Empty;
    public int CasdoorUsersCount { get; set; }
    public int CasdoorGroupsCount { get; set; }
}

/// <summary>
/// 同步预览结果
/// </summary>
public class SyncPreview
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    
    // 组织
    public int OrgsToCreate { get; set; }
    public int OrgsToUpdate { get; set; }
    public int OrgsInCasdoorOnly { get; set; }
    public List<string> OrgCreateSamples { get; set; } = new();
    public List<string> OrgUpdateSamples { get; set; } = new();
    
    // 用户
    public int UsersToCreate { get; set; }
    public int UsersToUpdate { get; set; }
    public int UsersInCasdoorOnly { get; set; }
    public List<string> UserCreateSamples { get; set; } = new();
    public List<string> UserUpdateSamples { get; set; } = new();
    
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 公司信息
/// </summary>
public class CompanyInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int DeptCount { get; set; }
}

/// <summary>
/// 同步日志
/// </summary>
public class SyncLog
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty; // Info, Warning, Error
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
