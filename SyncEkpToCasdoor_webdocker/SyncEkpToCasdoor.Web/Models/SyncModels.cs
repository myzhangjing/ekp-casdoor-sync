namespace SyncEkpToCasdoor.Web.Models;

/// <summary>
/// 应用配置
/// </summary>
public class AppConfig
{
    public string EkpConnectionString { get; set; } = string.Empty;
    public string CasdoorEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "SyncEkpToCasdoor";
    public string DefaultOwner { get; set; } = "fzswjtOrganization";
    public string StateFilePath { get; set; } = "sync_state.json";
    public DateTime? SinceUtc { get; set; }
    public string? MembershipViewName { get; set; }
    public List<string> TargetCompanyIds { get; set; } = new();
    public List<(string owner, string name)> EnforcerNames { get; set; } = new();
    
    public static AppConfig LoadFromConfiguration(IConfiguration configuration)
    {
        var config = new AppConfig
        {
            EkpConnectionString = configuration.GetValue<string>("EkpConnection") 
                ?? Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN") 
                ?? throw new InvalidOperationException("缺少 EKP 数据库连接配置"),
                
            CasdoorEndpoint = configuration.GetValue<string>("Casdoor:Endpoint")
                ?? Environment.GetEnvironmentVariable("CASDOOR_ENDPOINT")
                ?? throw new InvalidOperationException("缺少 Casdoor 端点配置"),
                
            ClientId = configuration.GetValue<string>("Casdoor:ClientId")
                ?? Environment.GetEnvironmentVariable("CASDOOR_CLIENT_ID")
                ?? throw new InvalidOperationException("缺少 Casdoor ClientId"),
                
            ClientSecret = configuration.GetValue<string>("Casdoor:ClientSecret")
                ?? Environment.GetEnvironmentVariable("CASDOOR_CLIENT_SECRET")
                ?? throw new InvalidOperationException("缺少 Casdoor ClientSecret"),
                
            OrganizationName = configuration.GetValue<string>("Casdoor:OrganizationName")
                ?? Environment.GetEnvironmentVariable("CASDOOR_ORG_NAME")
                ?? "fzswjtOrganization",
                
            DefaultOwner = configuration.GetValue<string>("Casdoor:DefaultOwner")
                ?? Environment.GetEnvironmentVariable("DEFAULT_OWNER")
                ?? "fzswjtOrganization",
                
            StateFilePath = configuration.GetValue<string>("StateFilePath")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_state.json")
        };
        
        // 目标公司 IDs
        var companyIds = configuration.GetValue<string>("TargetCompanyIds")
            ?? Environment.GetEnvironmentVariable("TARGET_COMPANY_IDS");
        if (!string.IsNullOrEmpty(companyIds))
        {
            config.TargetCompanyIds = companyIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();
        }
        
        return config;
    }
}

/// <summary>
/// 同步状态
/// </summary>
public class SyncStateData
{
    public DateTime? LastRunUtc { get; set; }
    public DateTime? LastFullSync { get; set; }
    public DateTime? LastIncrementalSync { get; set; }
    public string? LastSyncType { get; set; }
}

/// <summary>
/// 组织信息（从 EKP）
/// </summary>
public class OrganizationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int Type { get; set; }
    public int Order { get; set; }
    public string CompanyName { get; set; } = string.Empty;
}

/// <summary>
/// 成员关系
/// </summary>
public class MembershipInfo
{
    public string UserId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

/// <summary>
/// Casdoor 用户对象（用于同步）
/// </summary>
public class CasdoorSyncUser
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Casdoor 组织对象
/// </summary>
public class CasdoorGroup
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int DisplayOrder { get; set; }
}
