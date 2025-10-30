using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SyncEkpToCasdoor.UI.Models;

namespace SyncEkpToCasdoor.UI.Services;

/// <summary>
/// EKP 视图设置服务 - 自动检测和创建所需的数据库视图
/// </summary>
public class EkpViewSetupService
{
    /// <summary>
    /// 检查并创建所需的 EKP 视图
    /// </summary>
    public async Task<(bool Success, string Message, string Details)> EnsureViewsExistAsync(SyncConfiguration config)
    {
        try
        {
            var connectionString = $"Server={config.EkpServer},{config.EkpPort};Database={config.EkpDatabase};" +
                                 $"User Id={config.EkpUsername};Password={config.EkpPassword};" +
                                 $"TrustServerCertificate=True;";

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var results = new System.Text.StringBuilder();
            results.AppendLine("=== EKP 视图检查与创建 ===\n");

            // 1. 检查用户组成员关系视图
            var viewName = string.IsNullOrWhiteSpace(config.UserGroupView) 
                ? "vw_user_group_membership" 
                : config.UserGroupView;

            var viewExists = await CheckViewExistsAsync(connection, viewName);
            
            if (viewExists)
            {
                results.AppendLine($"✓ 视图 [{viewName}] 已存在");
            }
            else
            {
                results.AppendLine($"✗ 视图 [{viewName}] 不存在，正在创建...");
                
                var createResult = await CreateUserGroupMembershipViewAsync(connection, viewName);
                
                if (createResult.Success)
                {
                    results.AppendLine($"✓ 视图 [{viewName}] 创建成功");
                }
                else
                {
                    results.AppendLine($"✗ 视图 [{viewName}] 创建失败: {createResult.Message}");
                    return (false, "视图创建失败", results.ToString());
                }
            }

            // 2. 验证视图可用性
            results.AppendLine($"\n验证视图 [{viewName}] 数据...");
            var testQuery = $"SELECT TOP 5 username, dept_id FROM {viewName}";
            
            try
            {
                using var cmd = new SqlCommand(testQuery, connection);
                using var reader = await cmd.ExecuteReaderAsync();
                
                var rowCount = 0;
                while (await reader.ReadAsync())
                {
                    rowCount++;
                }
                
                results.AppendLine($"✓ 视图可正常访问，获取到 {rowCount} 条测试数据");
            }
            catch (Exception ex)
            {
                results.AppendLine($"✗ 视图访问测试失败: {ex.Message}");
                return (false, "视图访问失败", results.ToString());
            }

            results.AppendLine("\n=== 所有视图检查完成 ===");
            
            return (true, "视图准备就绪", results.ToString());
        }
        catch (Exception ex)
        {
            return (false, $"视图检查失败: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// 检查视图是否存在
    /// </summary>
    private async Task<bool> CheckViewExistsAsync(SqlConnection connection, string viewName)
    {
        var query = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.VIEWS 
            WHERE TABLE_NAME = @ViewName";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@ViewName", viewName);
        
        var count = (int)await cmd.ExecuteScalarAsync();
        return count > 0;
    }

    /// <summary>
    /// 创建用户组成员关系视图
    /// </summary>
    private async Task<(bool Success, string Message)> CreateUserGroupMembershipViewAsync(
        SqlConnection connection, string viewName)
    {
        try
        {
            // 创建视图的 SQL（优化版，输出列为 username, dept_id，与控制台程序一致）
            var createViewSql = $@"
CREATE VIEW [dbo].[{viewName}] AS
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
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
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
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
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
            d.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR d.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR EXISTS (
                SELECT 1 
                FROM dbo.sys_org_element p1
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p1.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p1.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
         OR EXISTS (
                SELECT 1
                FROM dbo.sys_org_element p1
                LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p2.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p2.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
      )
)
SELECT DISTINCT
    p.LoginName AS username,
    apd.DeptId AS dept_id
FROM person p
INNER JOIN all_person_dept apd ON p.PersonId = apd.PersonId
INNER JOIN valid_depts vd ON apd.DeptId = vd.DeptId;
";

            using var cmd = new SqlCommand(createViewSql, connection);
            await cmd.ExecuteNonQueryAsync();
            
            return (true, "视图创建成功");
        }
        catch (SqlException ex) when (ex.Number == 2714)
        {
            // 视图已存在（可能是并发创建）
            return (true, "视图已存在");
        }
        catch (Exception ex)
        {
            return (false, $"创建失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取视图创建脚本（供手动执行）
    /// </summary>
    public string GetViewCreationScript(string viewName = "vw_user_group_membership")
    {
        return $@"
-- ============================================
-- EKP 用户组成员关系视图（优化版，与控制台同步器一致）
-- 列名：username, dept_id
-- ============================================

IF OBJECT_ID('{viewName}', 'V') IS NOT NULL
    DROP VIEW {viewName};
GO

CREATE VIEW [dbo].[{viewName}] AS
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
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
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
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
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
            d.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR d.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR EXISTS (
                SELECT 1 
                FROM dbo.sys_org_element p1
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p1.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p1.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
         OR EXISTS (
                SELECT 1
                FROM dbo.sys_org_element p1
                LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p2.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p2.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
      )
)
SELECT DISTINCT
    p.LoginName AS username,
    apd.DeptId AS dept_id
FROM person p
INNER JOIN all_person_dept apd ON p.PersonId = apd.PersonId
INNER JOIN valid_depts vd ON apd.DeptId = vd.DeptId;
GO

-- 测试查询
SELECT TOP 10 username, dept_id FROM {viewName};
GO
";
    }
}
