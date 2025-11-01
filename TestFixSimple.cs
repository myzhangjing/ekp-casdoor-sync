using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;";

try
{
    using var conn = new SqlConnection(connStr);
    conn.Open();
    
    // 使用修复后的逻辑测试张璟
    var sql = @"
WITH person_info AS (
    SELECT 
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
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
    p.LoginName AS username,
    p.PersonName AS display_name,
    p.DeptId AS dept_id,
    dc.DeptName AS affiliation,
    dc.CompanyId AS company_id,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = dc.CompanyId) AS company_name
FROM person_info p
LEFT JOIN dept_company dc ON p.DeptId = dc.DeptId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
  AND p.LoginName = 'zhangjing';";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();
    
    if (reader.Read())
    {
        Console.WriteLine("✓✓✓ 修复成功！张璟现在能被查询到了！");
        Console.WriteLine($"  用户名: {reader.GetString(0)}");
        Console.WriteLine($"  姓名: {reader.GetString(1)}");
        Console.WriteLine($"  部门: {(reader.IsDBNull(3) ? "NULL" : reader.GetString(3))}");
        Console.WriteLine($"  公司: {(reader.IsDBNull(5) ? "NULL" : reader.GetString(5))}");
    }
    else
    {
        Console.WriteLine("✗ 修复后张璟仍无法查询到");
        Console.WriteLine("可能原因:");
        Console.WriteLine("1. 数据库视图未更新 - 需要运行 --apply-optimized-views");
        Console.WriteLine("2. 张璟确实不属于技术管理部");
        Console.WriteLine("3. 技术管理部无法追溯到目标公司");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}
