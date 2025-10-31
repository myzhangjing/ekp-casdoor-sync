using System.Text.Json;
using Microsoft.Data.SqlClient;
using SyncEkpToCasdoor.Web.Models;

namespace SyncEkpToCasdoor.Web.Services;

/// <summary>
/// 同步服务实现
/// </summary>
public class SyncService : ISyncService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncService> _logger;
    private readonly string _syncStateFile;
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static DateTime? _syncStartTime = null;
    
    public SyncService(IConfiguration configuration, ILogger<SyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _syncStateFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_state.json");
    }
    
    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteSyncAsync(incremental: false, cancellationToken);
    }
    
    public async Task<SyncResult> SyncIncrementalAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteSyncAsync(incremental: true, cancellationToken);
    }

    private async Task<SyncResult> ExecuteSyncAsync(bool incremental, CancellationToken cancellationToken)
    {
        // 非阻塞尝试获取锁
        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            var runningTime = _syncStartTime.HasValue 
                ? $"(已运行 {(DateTime.Now - _syncStartTime.Value).TotalSeconds:F0} 秒)" 
                : "";
            return new SyncResult
            {
                Success = false,
                Message = $"同步任务正在运行中，请稍后再试 {runningTime}"
            };
        }

        var result = new SyncResult { StartTime = DateTime.Now };
        _syncStartTime = DateTime.Now;

        try
        {
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("未配置 EKP 数据库连接字符串");
            }

            _logger.LogInformation("开始{Mode}同步...", incremental ? "增量" : "全量");
            MemoryLogCollector.AddLog("Info", $"开始{(incremental ? "增量" : "全量")}同步");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // 读取用户视图统计
            var usersCount = await ExecuteScalarAsync<int>(connection, "SELECT COUNT(*) FROM vw_casdoor_users_sync", cancellationToken);
            _logger.LogInformation("用户视图记录数: {Count}", usersCount);
            MemoryLogCollector.AddLog("Info", $"用户视图记录数: {usersCount}");

            // 读取组织视图统计
            var orgCount = await ExecuteScalarAsync<int>(connection, "SELECT COUNT(*) FROM vw_org_structure_sync", cancellationToken);
            _logger.LogInformation("组织视图记录数: {Count}", orgCount);
            MemoryLogCollector.AddLog("Info", $"组织视图记录数: {orgCount}");

            // 读取成员关系统计
            var memCount = await ExecuteScalarAsync<int>(connection, "SELECT COUNT(*) FROM vw_user_group_membership", cancellationToken);
            _logger.LogInformation("成员关系视图记录数: {Count}", memCount);
            MemoryLogCollector.AddLog("Info", $"成员关系视图记录数: {memCount}");

            // 实际调用 Casdoor：先组织、后用户，并带成员关系
            var cfg = AppConfig.LoadFromConfiguration(_configuration);
            using (var cas = new SimpleCasdoorRepository(cfg))
            {
                // 1) 组织
                var orgs = new List<(string Id, string DisplayName, string? ParentId, string Type, string Owner, bool IsEnabled)>();
                const string orgSql = @"SELECT id, display_name, parent_id, type, owner, is_enabled FROM vw_org_structure_sync ORDER BY CASE WHEN parent_id IS NULL THEN 0 ELSE 1 END, display_name";
                await using (var cmd = new SqlCommand(orgSql, connection) { CommandTimeout = 300 })
                await using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        orgs.Add(new(
                            rdr.GetString(0),
                            rdr.IsDBNull(1) ? rdr.GetString(0) : rdr.GetString(1),
                            rdr.IsDBNull(2) ? null : rdr.GetString(2),
                            rdr.IsDBNull(3) ? "department" : rdr.GetString(3),
                            rdr.IsDBNull(4) ? cfg.DefaultOwner : rdr.GetString(4),
                            !rdr.IsDBNull(5) && rdr.GetBoolean(5)
                        ));
                    }
                }

                int orgIndex = 0;
                _logger.LogInformation("开始同步组织，共 {Count} 个", orgs.Count);
                MemoryLogCollector.AddLog("Info", $"开始同步组织，共 {orgs.Count} 个");
                foreach (var o in orgs)
                {
                    orgIndex++;
                    
                    var g = new EkpGroup
                    {
                        Id = o.Id,
                        DisplayName = string.IsNullOrWhiteSpace(o.DisplayName) ? o.Id : o.DisplayName,
                        ParentId = o.ParentId,
                        Owner = string.IsNullOrWhiteSpace(o.Owner) ? cfg.DefaultOwner : o.Owner,
                        Type = o.Type,
                        DeptId = o.Id,
                        IsEnabled = o.IsEnabled
                    };
                    
                    try 
                    { 
                        cas.UpsertGroup(g); 
                        
                        // 每10个输出进度
                        if (orgIndex % 10 == 0 || orgIndex == orgs.Count)
                        {
                            var msg = $"组织同步进度: {orgIndex}/{orgs.Count} ({orgIndex * 100 / orgs.Count}%) - {g.DisplayName}";
                            _logger.LogInformation(msg);
                            MemoryLogCollector.AddLog("Info", msg);
                        }
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogWarning(ex, "同步组失败: {GroupId}", o.Id);
                        MemoryLogCollector.AddLog("Warning", $"同步组失败: {o.Id}", ex.Message);
                    }
                }

                // 2) 加载 Casdoor 组映射
                cas.LoadCasdoorGroupMapping();

                // 3) 成员映射：username -> deptIds
                var memberships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                const string memSqlAll = @"SELECT username, dept_id FROM vw_user_group_membership";
                await using (var memCmd = new SqlCommand(memSqlAll, connection) { CommandTimeout = 300 })
                await using (var mr = await memCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await mr.ReadAsync(cancellationToken))
                    {
                        var u = mr.IsDBNull(0) ? string.Empty : mr.GetString(0);
                        var d = mr.IsDBNull(1) ? string.Empty : mr.GetString(1);
                        if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(d)) continue;
                        if (!memberships.TryGetValue(u, out var list)) { list = new List<string>(); memberships[u] = list; }
                        if (!list.Contains(d, StringComparer.OrdinalIgnoreCase)) list.Add(d);
                    }
                }

                // 4) 用户
                _logger.LogInformation("开始同步用户，预计 {Count} 个", usersCount);
                MemoryLogCollector.AddLog("Info", $"开始同步用户，预计 {usersCount} 个");
                const string userSql = @"SELECT id, username, display_name, email, phone, gender, language, dept_id, company_name, owner, password_md5 FROM vw_casdoor_users_sync ORDER BY display_name";
                int usersSynced = 0;
                int totalUsersEstimate = usersCount; // 使用之前查询的总数
                await using (var userCmd = new SqlCommand(userSql, connection) { CommandTimeout = 300 })
                await using (var ur = await userCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await ur.ReadAsync(cancellationToken))
                    {
                        var id = ur.IsDBNull(1) ? ur.GetString(0) : ur.GetString(1);
                        var u = new EkpUser
                        {
                            Id = id,
                            DisplayName = ur.IsDBNull(2) ? id : ur.GetString(2),
                            Email = ur.IsDBNull(3) ? null : ur.GetString(3),
                            Phone = ur.IsDBNull(4) ? null : ur.GetString(4),
                            Gender = ur.IsDBNull(5) ? string.Empty : ur.GetString(5),
                            Language = ur.IsDBNull(6) ? "zh" : ur.GetString(6),
                            Department = ur.IsDBNull(7) ? string.Empty : ur.GetString(7),
                            CompanyName = ur.IsDBNull(8) ? string.Empty : ur.GetString(8),
                            PasswordMd5 = ur.IsDBNull(10) ? string.Empty : ur.GetString(10)
                        };
                        var owner = ur.IsDBNull(9) ? cfg.DefaultOwner : ur.GetString(9);
                        var groupIds = memberships.TryGetValue(id, out var gid) ? gid : (u.Department is { Length: > 0 } ? new List<string> { u.Department } : null);
                        
                        try 
                        { 
                            cas.UpsertUser(u, owner, true, groupIds); 
                            usersSynced++;
                            
                            // 每50个输出进度
                            if (usersSynced % 50 == 0 || usersSynced == totalUsersEstimate)
                            {
                                var msg = $"用户同步进度: {usersSynced}/{totalUsersEstimate} ({usersSynced * 100 / totalUsersEstimate}%) - {u.DisplayName}";
                                _logger.LogInformation(msg);
                                MemoryLogCollector.AddLog("Info", msg);
                            }
                        }
                        catch (Exception ex) 
                        { 
                            _logger.LogWarning(ex, "同步用户失败: {User}", id);
                            MemoryLogCollector.AddLog("Warning", $"同步用户失败: {id}", ex.Message);
                        }
                    }
                }
                
                _logger.LogInformation("用户同步完成: {Total} 个用户已处理", usersSynced);
                MemoryLogCollector.AddLog("Info", $"用户同步完成: {usersSynced} 个用户已处理");

                result.UsersProcessed = usersSynced;
                result.OrganizationsProcessed = orgs.Count;
            }

            result.Success = true;
            result.Message = (incremental ? "增量" : "全量") + "同步完成";
            MemoryLogCollector.AddLog("Info", result.Message);
            result.EndTime = DateTime.Now;

            await SaveSyncStateAsync(incremental ? "incremental" : "full", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Mode}同步失败", incremental ? "增量" : "全量");
            MemoryLogCollector.AddLog("Error", $"同步失败: {ex.Message}", ex.ToString());
            result.Success = false;
            result.Message = $"同步失败: {ex.Message}";
            result.Errors.Add(ex.ToString());
            result.EndTime = DateTime.Now;
        }
        finally
        {
            _syncStartTime = null;
            _syncLock.Release();
        }

        return result;
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqlConnection conn, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        var obj = await cmd.ExecuteScalarAsync(cancellationToken);
        if (obj == null || obj is DBNull)
            return default!;
        return (T)Convert.ChangeType(obj, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
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

            // 目标公司ID（从配置或默认）
            var companyIds = _configuration.GetValue<string>("TargetCompanyIds")
                ?? Environment.GetEnvironmentVariable("TARGET_COMPANY_IDS")
                ?? "16f1c1a4910426f41649fd14862b99a1,18e389224b660b4d67413f8466285581";
            var idList = string.Join(", ", companyIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(id => $"'{id}'"));

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            // 1) 重建 vw_casdoor_users_sync 视图（包含 password_md5）
            var dropUsers = "IF OBJECT_ID('vw_casdoor_users_sync', 'V') IS NOT NULL DROP VIEW vw_casdoor_users_sync;";
            await using (var cmd = new SqlCommand(dropUsers, conn) { CommandTimeout = 300 })
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var usersSql = $@"
CREATE VIEW [dbo].[vw_casdoor_users_sync] AS
WITH person_info AS (
    SELECT
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
        p.fd_email AS Email,
        p.fd_mobile_no AS MobileNo,
        p.fd_sex AS Sex,
        p.fd_password AS PasswordMd5,
        e.fd_create_time AS CreatedTime,
        e.fd_alter_time AS UpdatedTime,
        COALESCE(
            (
                SELECT TOP 1 CASE
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
                    ELSE NULL END
            ),
            (
                SELECT TOP 1 CASE
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
                    ELSE NULL END
                FROM dbo.sys_org_post_person spp
                INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
                WHERE spp.fd_personid = e.fd_id
                    AND pe.fd_org_type = 4
                    AND pe.fd_is_available = 1
            )
        ) AS DeptId
    FROM dbo.sys_org_element e
    INNER JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8
      AND e.fd_is_available = 1
      AND p.fd_login_name IS NOT NULL
),
dept_company AS (
    SELECT
        d.fd_id AS DeptId,
        COALESCE(
            (SELECT TOP 1 p.fd_id FROM dbo.sys_org_element p WHERE p.fd_id = d.fd_parentorgid AND p.fd_org_type = 1 AND p.fd_is_available = 1),
            (SELECT TOP 1 p.fd_id FROM dbo.sys_org_element p WHERE p.fd_id = d.fd_parentid AND p.fd_org_type = 1 AND p.fd_is_available = 1),
            (SELECT TOP 1 gp.fd_id
             FROM dbo.sys_org_element pp
             INNER JOIN dbo.sys_org_element gp ON (gp.fd_id = pp.fd_parentorgid OR gp.fd_id = pp.fd_parentid)
             WHERE pp.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                 AND gp.fd_org_type = 1 AND gp.fd_is_available = 1),
            (SELECT TOP 1 ggp.fd_id
             FROM dbo.sys_org_element p1
             LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
             LEFT JOIN dbo.sys_org_element ggp ON (ggp.fd_id = p2.fd_parentorgid OR ggp.fd_id = p2.fd_parentid)
             WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                 AND ggp.fd_org_type = 1 AND ggp.fd_is_available = 1)
        ) AS CompanyId,
        d.fd_name AS DeptName
    FROM dbo.sys_org_element d
    WHERE d.fd_org_type = 2
        AND d.fd_is_available = 1
)
SELECT 
    p.LoginName AS id,
    p.LoginName AS username,
    p.PersonName AS display_name,
    p.Email AS email,
    p.MobileNo AS phone,
    p.CreatedTime AS created_at,
    p.UpdatedTime AS updated_at,
    CASE p.Sex
        WHEN 'M' THEN 'Male'
        WHEN 'F' THEN 'Female'
        ELSE ''
    END AS gender,
    N'zh' AS language,
    p.DeptId AS dept_id,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = dc.CompanyId) AS company_name,
    dc.DeptName AS affiliation,
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    NULL AS type,
    p.PasswordMd5 AS password_md5
FROM person_info p
LEFT JOIN dept_company dc ON p.DeptId = dc.DeptId
WHERE dc.CompanyId IN ({idList});";

            await using (var cmd = new SqlCommand(usersSql, conn) { CommandTimeout = 300 })
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2) 重建 vw_user_group_membership（成员-部门映射）
            var dropMem = "IF OBJECT_ID('vw_user_group_membership', 'V') IS NOT NULL DROP VIEW vw_user_group_membership;";
            await using (var cmd = new SqlCommand(dropMem, conn) { CommandTimeout = 300 })
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var memSql = $@"
CREATE VIEW [dbo].[vw_user_group_membership] AS
WITH person AS (
    SELECT
        e.fd_id AS PersonId,
        p.fd_login_name AS LoginName
    FROM dbo.sys_org_element e
    INNER JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8
      AND e.fd_is_available = 1
      AND p.fd_login_name IS NOT NULL
),
post_dept AS (
    SELECT DISTINCT
        spp.fd_personid AS PersonId,
        CASE
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
            ELSE NULL
        END AS DeptId
    FROM dbo.sys_org_post_person spp
    INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
    WHERE pe.fd_org_type = 4
      AND pe.fd_is_available = 1
),
person_dept AS (
    SELECT DISTINCT
        e.fd_id AS PersonId,
        CASE
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
            ELSE NULL
        END AS DeptId
    FROM dbo.sys_org_element e
    WHERE e.fd_org_type = 8 AND e.fd_is_available = 1
),
all_person_dept AS (
    SELECT PersonId, DeptId FROM post_dept WHERE DeptId IS NOT NULL
    UNION
    SELECT PersonId, DeptId FROM person_dept WHERE DeptId IS NOT NULL
),
valid_depts AS (
    SELECT DISTINCT d.fd_id AS DeptId
    FROM dbo.sys_org_element d
    WHERE d.fd_is_available = 1
      AND d.fd_org_type = 2
      AND (
            d.fd_parentorgid IN ({idList})
         OR d.fd_parentid    IN ({idList})
         OR EXISTS (
                SELECT 1
                FROM dbo.sys_org_element p1
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p1.fd_parentorgid IN ({idList}) OR p1.fd_parentid IN ({idList}))
          )
         OR EXISTS (
                SELECT 1
                FROM dbo.sys_org_element p1
                LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p2.fd_parentorgid IN ({idList}) OR p2.fd_parentid IN ({idList}))
          )
      )
)
SELECT DISTINCT
    p.LoginName AS username,
    apd.DeptId AS dept_id
FROM person p
INNER JOIN all_person_dept apd ON p.PersonId = apd.PersonId
INNER JOIN valid_depts vd ON apd.DeptId = vd.DeptId;";

            await using (var cmd = new SqlCommand(memSql, conn) { CommandTimeout = 300 })
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3) 修复/优化组织层级视图 vw_org_structure_sync
            var orgSql = $@"
ALTER VIEW [dbo].[vw_org_structure_sync] AS
WITH
target_companies AS (
    SELECT fd_id AS company_id
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
      AND fd_org_type = 1
      AND fd_id IN ({idList})
),
base AS (
    SELECT
        e.fd_id AS id,
        e.fd_name AS display_name,
        e.fd_org_type AS org_type,
        e.fd_is_available AS is_enabled,
        e.fd_create_time AS created_time,
        e.fd_alter_time AS updated_time,
        CAST(
            CASE
                WHEN e.fd_parentid IS NOT NULL AND EXISTS (
                    SELECT 1 FROM dbo.sys_org_element p
                    WHERE p.fd_id = e.fd_parentid AND p.fd_org_type IN (1,2) AND p.fd_is_available = 1
                ) THEN e.fd_parentid
                ELSE e.fd_parentorgid
            END
        AS nvarchar(255)) AS parent_candidate_id
    FROM dbo.sys_org_element e
    WHERE e.fd_is_available = 1
      AND e.fd_org_type IN (1,2)
),
roots AS (
    SELECT
        b.id,
        b.display_name,
        b.org_type,
        CAST(NULL AS nvarchar(255)) AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
        b.created_time,
        b.updated_time,
        b.is_enabled,
        0 AS depth
    FROM base b
    INNER JOIN target_companies tc ON b.id = tc.company_id
),
org_hierarchy AS (
    SELECT * FROM roots
    UNION ALL
    SELECT
        c.id,
        c.display_name,
        c.org_type,
        c.parent_candidate_id AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
        c.created_time,
        c.updated_time,
        c.is_enabled,
        oh.depth + 1
    FROM base c
    INNER JOIN org_hierarchy oh ON c.parent_candidate_id = oh.id
    WHERE oh.depth < 20
)
SELECT
    id,
    id AS name,
    display_name,
    parent_id,
    CASE WHEN org_type = 1 THEN 'company' ELSE 'department' END AS type,
    owner,
    created_time,
    updated_time,
    CAST(NULL AS NVARCHAR(255)) AS dept_id,
    CAST(CASE WHEN is_enabled = 1 THEN 1 ELSE 0 END AS BIT) AS is_enabled
FROM org_hierarchy
WHERE is_enabled = 1;";

            await using (var cmd = new SqlCommand(orgSql, conn) { CommandTimeout = 300 })
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

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
                    IsRunning = _syncLock.CurrentCount == 0
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取同步状态失败");
        }
        
        return new SyncState { IsRunning = _syncLock.CurrentCount == 0 };
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
    
    public async Task<ConnectionTestResult> TestConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ConnectionTestResult();
        
        // 测试 EKP 连接
        try
        {
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                result.EkpMessage = "未配置 EKP 数据库连接字符串";
            }
            else
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                
                result.EkpUsersCount = await ExecuteScalarAsync<int>(connection, 
                    "SELECT COUNT(*) FROM vw_casdoor_users_sync", cancellationToken);
                result.EkpOrgsCount = await ExecuteScalarAsync<int>(connection, 
                    "SELECT COUNT(*) FROM vw_org_structure_sync", cancellationToken);
                
                result.EkpConnected = true;
                result.EkpMessage = $"连接成功 (用户: {result.EkpUsersCount}, 组织: {result.EkpOrgsCount})";
            }
        }
        catch (Exception ex)
        {
            result.EkpConnected = false;
            result.EkpMessage = $"连接失败: {ex.Message}";
            _logger.LogError(ex, "EKP 连接测试失败");
        }
        
        // 测试 Casdoor 连接
        try
        {
            var cfg = AppConfig.LoadFromConfiguration(_configuration);
            result.CasdoorEndpoint = cfg.CasdoorEndpoint;
            result.CasdoorOwner = cfg.DefaultOwner;
            
            using var cas = new SimpleCasdoorRepository(cfg);
            
            // 尝试获取组织列表
            var testHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"{cfg.CasdoorEndpoint}/api/get-organizations?clientId={Uri.EscapeDataString(cfg.ClientId)}&clientSecret={Uri.EscapeDataString(cfg.ClientSecret)}";
            
            var response = await testHttp.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(json))
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                if (doc.TryGetProperty("status", out var status) && status.GetString() == "ok")
                {
                    result.CasdoorConnected = true;
                    result.CasdoorMessage = "连接成功";
                    
                    // 尝试获取用户和组数量
                    try
                    {
                        var groupsUrl = $"{cfg.CasdoorEndpoint}/api/get-groups?owner={Uri.EscapeDataString(cfg.DefaultOwner)}&clientId={Uri.EscapeDataString(cfg.ClientId)}&clientSecret={Uri.EscapeDataString(cfg.ClientSecret)}";
                        var groupsResp = await testHttp.GetAsync(groupsUrl, cancellationToken);
                        var groupsJson = await groupsResp.Content.ReadAsStringAsync(cancellationToken);
                        var groupsDoc = JsonSerializer.Deserialize<JsonElement>(groupsJson);
                        if (groupsDoc.TryGetProperty("data", out var groupsData) && groupsData.ValueKind == JsonValueKind.Array)
                        {
                            result.CasdoorGroupsCount = groupsData.GetArrayLength();
                        }
                        
                        var usersUrl = $"{cfg.CasdoorEndpoint}/api/get-users?owner={Uri.EscapeDataString(cfg.DefaultOwner)}&clientId={Uri.EscapeDataString(cfg.ClientId)}&clientSecret={Uri.EscapeDataString(cfg.ClientSecret)}";
                        var usersResp = await testHttp.GetAsync(usersUrl, cancellationToken);
                        var usersJson = await usersResp.Content.ReadAsStringAsync(cancellationToken);
                        var usersDoc = JsonSerializer.Deserialize<JsonElement>(usersJson);
                        if (usersDoc.TryGetProperty("data", out var usersData) && usersData.ValueKind == JsonValueKind.Array)
                        {
                            result.CasdoorUsersCount = usersData.GetArrayLength();
                        }
                        
                        result.CasdoorMessage = $"连接成功 (用户: {result.CasdoorUsersCount}, 组: {result.CasdoorGroupsCount})";
                    }
                    catch
                    {
                        // 获取数量失败不影响连接状态
                    }
                }
                else
                {
                    result.CasdoorMessage = "API 返回错误状态";
                }
            }
            else
            {
                result.CasdoorMessage = $"HTTP {(int)response.StatusCode}: {json.Substring(0, Math.Min(100, json.Length))}";
            }
            
            testHttp.Dispose();
        }
        catch (Exception ex)
        {
            result.CasdoorConnected = false;
            result.CasdoorMessage = $"连接失败: {ex.Message}";
            _logger.LogError(ex, "Casdoor 连接测试失败");
        }
        
        return result;
    }
    
    public async Task<SyncPreview> PreviewSyncAsync(CancellationToken cancellationToken = default)
    {
        var preview = new SyncPreview { GeneratedAt = DateTime.Now };
        
        try
        {
            var cfg = AppConfig.LoadFromConfiguration(_configuration);
            var connectionString = cfg.EkpConnectionString;
            
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            // 从 EKP 读取组织和用户
            var ekpOrgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ekpUsers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            const string orgSql = "SELECT id, display_name FROM vw_org_structure_sync";
            await using (var cmd = new SqlCommand(orgSql, connection) { CommandTimeout = 60 })
            await using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await rdr.ReadAsync(cancellationToken))
                {
                    var id = rdr.GetString(0);
                    var name = rdr.IsDBNull(1) ? id : rdr.GetString(1);
                    ekpOrgs[id] = name;
                }
            }
            
            const string userSql = "SELECT username, display_name FROM vw_casdoor_users_sync";
            await using (var cmd = new SqlCommand(userSql, connection) { CommandTimeout = 60 })
            await using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await rdr.ReadAsync(cancellationToken))
                {
                    var id = rdr.GetString(0);
                    var name = rdr.IsDBNull(1) ? id : rdr.GetString(1);
                    ekpUsers[id] = name;
                }
            }
            
            // 从 Casdoor 读取组织和用户
            var casdoorOrgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var casdoorUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            using var testHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            var groupsUrl = $"{cfg.CasdoorEndpoint}/api/get-groups?owner={Uri.EscapeDataString(cfg.DefaultOwner)}&clientId={Uri.EscapeDataString(cfg.ClientId)}&clientSecret={Uri.EscapeDataString(cfg.ClientSecret)}";
            var groupsResp = await testHttp.GetAsync(groupsUrl, cancellationToken);
            if (groupsResp.IsSuccessStatusCode)
            {
                var groupsJson = await groupsResp.Content.ReadAsStringAsync(cancellationToken);
                var groupsDoc = JsonSerializer.Deserialize<JsonElement>(groupsJson);
                if (groupsDoc.TryGetProperty("data", out var groupsData) && groupsData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in groupsData.EnumerateArray())
                    {
                        if (g.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(name)) casdoorOrgs.Add(name!);
                        }
                    }
                }
            }
            
            var usersUrl = $"{cfg.CasdoorEndpoint}/api/get-users?owner={Uri.EscapeDataString(cfg.DefaultOwner)}&clientId={Uri.EscapeDataString(cfg.ClientId)}&clientSecret={Uri.EscapeDataString(cfg.ClientSecret)}";
            var usersResp = await testHttp.GetAsync(usersUrl, cancellationToken);
            if (usersResp.IsSuccessStatusCode)
            {
                var usersJson = await usersResp.Content.ReadAsStringAsync(cancellationToken);
                var usersDoc = JsonSerializer.Deserialize<JsonElement>(usersJson);
                if (usersDoc.TryGetProperty("data", out var usersData) && usersData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var u in usersData.EnumerateArray())
                    {
                        if (u.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(name)) casdoorUsers.Add(name!);
                        }
                    }
                }
            }
            
            // 对比差异
            foreach (var (id, name) in ekpOrgs)
            {
                if (!casdoorOrgs.Contains(id))
                {
                    preview.OrgsToCreate++;
                    if (preview.OrgCreateSamples.Count < 20)
                        preview.OrgCreateSamples.Add($"{id} ({name})");
                }
                else
                {
                    preview.OrgsToUpdate++;
                    if (preview.OrgUpdateSamples.Count < 10)
                        preview.OrgUpdateSamples.Add($"{id} ({name})");
                }
            }
            
            foreach (var org in casdoorOrgs)
            {
                if (!ekpOrgs.ContainsKey(org))
                {
                    preview.OrgsInCasdoorOnly++;
                }
            }
            
            foreach (var (id, name) in ekpUsers)
            {
                if (!casdoorUsers.Contains(id))
                {
                    preview.UsersToCreate++;
                    if (preview.UserCreateSamples.Count < 20)
                        preview.UserCreateSamples.Add($"{id} ({name})");
                }
                else
                {
                    preview.UsersToUpdate++;
                    if (preview.UserUpdateSamples.Count < 10)
                        preview.UserUpdateSamples.Add($"{id} ({name})");
                }
            }
            
            foreach (var user in casdoorUsers)
            {
                if (!ekpUsers.ContainsKey(user))
                {
                    preview.UsersInCasdoorOnly++;
                }
            }
            
            preview.Success = true;
            preview.Message = "预览生成成功";
        }
        catch (Exception ex)
        {
            preview.Success = false;
            preview.Message = $"预览失败: {ex.Message}";
            _logger.LogError(ex, "预览同步失败");
        }
        
        return preview;
    }
    
    public async Task<List<CompanyInfo>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = new List<CompanyInfo>();
        
        try
        {
            var connectionString = _configuration.GetValue<string>("EkpConnection");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            const string sql = @"
                SELECT 
                    c.fd_id AS CompanyId,
                    c.fd_name AS CompanyName,
                    (SELECT COUNT(*) FROM vw_casdoor_users_sync u WHERE u.company_name = c.fd_name) AS UserCount,
                    (SELECT COUNT(*) FROM vw_org_structure_sync o WHERE o.parent_id = c.fd_id OR o.id = c.fd_id) AS DeptCount
                FROM sys_org_element c
                WHERE c.fd_org_type = 1 AND c.fd_is_available = 1
                ORDER BY c.fd_name";
            
            await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                companies.Add(new CompanyInfo
                {
                    Id = reader.GetString(0),
                    Name = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                    UserCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    DeptCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取公司列表失败");
        }
        
        return companies;
    }
    
    public async Task<SyncResult> SyncByCompanyAsync(string companyId, bool incremental, CancellationToken cancellationToken = default)
    {
        MemoryLogCollector.AddLog("Info", $"开始同步公司: {companyId}");
        // 临时实现：调用全量同步（后续可优化为真正的按公司同步）
        return await ExecuteSyncAsync(incremental, cancellationToken);
    }
    
    public async Task SyncCompanyAsync(string companyId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始同步公司 {CompanyId}", companyId);
            var result = await SyncByCompanyAsync(companyId, incremental: true, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("公司 {CompanyId} 同步成功: 处理 {Users} 个用户, {Orgs} 个组织", 
                    companyId, result.UsersProcessed, result.OrganizationsProcessed);
            }
            else
            {
                _logger.LogWarning("公司 {CompanyId} 同步失败: {Message}", companyId, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "公司 {CompanyId} 同步异常", companyId);
            throw;
        }
    }
    
    public async Task<List<SyncLog>> GetSyncLogsAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(MemoryLogCollector.GetLogs(count));
    }
    
    public async Task<int> CleanOrphanDataAsync(string owner, CancellationToken cancellationToken = default)
    {
        int cleaned = 0;
        
        try
        {
            var cfg = AppConfig.LoadFromConfiguration(_configuration);
            using var cas = new SimpleCasdoorRepository(cfg);
            
            MemoryLogCollector.AddLog("Info", $"开始清理孤立数据: {owner}");
            
            // 调用 PurgeExceptOwner
            cas.PurgeExceptOwner(owner);
            
            MemoryLogCollector.AddLog("Info", "孤立数据清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理孤立数据失败");
            MemoryLogCollector.AddLog("Error", $"清理失败: {ex.Message}");
        }
        
        return await Task.FromResult(cleaned);
    }
    
    public async Task<string> ExportOrganizationHierarchyAsync(CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"org_hierarchy_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        
        try
        {
            var cfg = AppConfig.LoadFromConfiguration(_configuration);
            using var cas = new SimpleCasdoorRepository(cfg);
            
            // 需要先同步一次来构建层级
            var connectionString = cfg.EkpConnectionString;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            // 读取组织（简化版）
            var orgs = new List<(string Id, string DisplayName, string? ParentId)>();
            const string orgSql = @"SELECT id, display_name, parent_id FROM vw_org_structure_sync ORDER BY display_name";
            
            await using (var cmd = new SqlCommand(orgSql, connection) { CommandTimeout = 300 })
            await using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await rdr.ReadAsync(cancellationToken))
                {
                    orgs.Add((
                        rdr.GetString(0),
                        rdr.IsDBNull(1) ? rdr.GetString(0) : rdr.GetString(1),
                        rdr.IsDBNull(2) ? null : rdr.GetString(2)
                    ));
                }
            }
            
            // 导出到CSV
            using var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            writer.WriteLine("组织ID,组织名称,父组织ID");
            foreach (var org in orgs)
            {
                writer.WriteLine($"\"{org.Id}\",\"{org.DisplayName}\",\"{org.ParentId ?? ""}\"");
            }
            
            MemoryLogCollector.AddLog("Info", $"组织层级已导出: {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出组织层级失败");
            MemoryLogCollector.AddLog("Error", $"导出失败: {ex.Message}");
        }
        
        return await Task.FromResult(filePath);
    }
    
    private class SyncStateData
    {
        public DateTime? LastFullSync { get; set; }
        public DateTime? LastIncrementalSync { get; set; }
        public string? LastSyncType { get; set; }
    }
}
