using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;";

Console.WriteLine("正在测试张璟的视图匹配逻辑...\n");
using var conn = new SqlConnection(connStr);
conn.Open();

// 测试person_info CTE对张璟的DeptId计算
Console.WriteLine("========== 1. person_info CTE - 张璟的DeptId ==========");
var sql1 = @"
WITH person_info AS (
    SELECT 
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
        COALESCE(
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
                    ELSE NULL END
                FROM dbo.sys_org_post_person spp
                INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
                WHERE spp.fd_personid = e.fd_id 
                    AND pe.fd_org_type = 4
                    AND pe.fd_is_available = 1
            ),
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
                    ELSE NULL END
            )
        ) AS DeptId
    FROM dbo.sys_org_element e
    INNER JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8 
      AND e.fd_is_available = 1
      AND p.fd_login_name IS NOT NULL
)
SELECT 
    PersonId,
    PersonName,
    LoginName,
    DeptId,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = DeptId) AS DeptName
FROM person_info
WHERE LoginName = 'zhangjing';";

using (var cmd = new SqlCommand(sql1, conn))
using (var reader = cmd.ExecuteReader())
{
    if (reader.Read())
    {
        var personId = reader.GetString(0);
        var name = reader.GetString(1);
        var login = reader.GetString(2);
        var deptId = reader.IsDBNull(3) ? "NULL" : reader.GetString(3);
        var deptName = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);
        
        Console.WriteLine($"PersonId: {personId}");
        Console.WriteLine($"姓名: {name}");
        Console.WriteLine($"登录名: {login}");
        Console.WriteLine($"DeptId: {deptId}");
        Console.WriteLine($"部门名: {deptName}");
        
        if (deptId == "NULL")
        {
            Console.WriteLine("\n✗ 问题: DeptId为NULL！");
        }
    }
    else
    {
        Console.WriteLine("✗ 未找到zhangjing!");
    }
}

// 测试dept_company CTE对技术管理部的CompanyId计算
Console.WriteLine("\n========== 2. dept_company CTE - 技术管理部的CompanyId ==========");
var sql2 = @"
WITH dept_company AS (
    SELECT 
        d.fd_id AS DeptId,
        d.fd_name AS DeptName,
        d.fd_parentid,
        d.fd_parentorgid,
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
        ) AS CompanyId
    FROM dbo.sys_org_element d
    WHERE d.fd_org_type = 2 
        AND d.fd_is_available = 1
)
SELECT 
    DeptId,
    DeptName,
    fd_parentid,
    fd_parentorgid,
    CompanyId,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = CompanyId) AS CompanyName,
    CASE WHEN CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581') 
         THEN '✓ 目标公司' 
         ELSE '✗ 非目标公司' END AS IsTarget
FROM dept_company
WHERE DeptName LIKE '%技术管理%';";

using (var cmd = new SqlCommand(sql2, conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        var deptId = reader.GetString(0);
        var deptName = reader.GetString(1);
        var parentId = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
        var parentOrgId = reader.IsDBNull(3) ? "NULL" : reader.GetString(3);
        var companyId = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);
        var companyName = reader.IsDBNull(5) ? "NULL" : reader.GetString(5);
        var isTarget = reader.GetString(6);
        
        Console.WriteLine($"部门ID: {deptId.Substring(0, 8)}...");
        Console.WriteLine($"部门名: {deptName}");
        Console.WriteLine($"父ID: {parentId.Substring(0, Math.Min(8, parentId.Length))}...");
        Console.WriteLine($"父组织ID: {parentOrgId.Substring(0, Math.Min(8, parentOrgId.Length))}...");
        Console.WriteLine($"CompanyId: {companyId}");
        Console.WriteLine($"公司名: {companyName}");
        Console.WriteLine($"匹配: {isTarget}");
        Console.WriteLine();
    }
}

// 完整视图测试
Console.WriteLine("========== 3. 完整视图查询 - 张璟是否在结果中 ==========");
var sql3 = @"
WITH person_info AS (
    SELECT 
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
        p.fd_email AS Email,
        p.fd_mobile_no AS MobileNo,
        p.fd_sex AS Sex,
        e.fd_create_time AS CreatedTime,
        e.fd_alter_time AS UpdatedTime,
        COALESCE(
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
                    ELSE NULL END
                FROM dbo.sys_org_post_person spp
                INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
                WHERE spp.fd_personid = e.fd_id 
                    AND pe.fd_org_type = 4
                    AND pe.fd_is_available = 1
            ),
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
                    ELSE NULL END
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
WHERE p.LoginName = 'zhangjing';";

using (var cmd = new SqlCommand(sql3, conn))
using (var reader = cmd.ExecuteReader())
{
    if (reader.Read())
    {
        var username = reader.GetString(0);
        var name = reader.GetString(1);
        var deptId = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
        var affiliation = reader.IsDBNull(3) ? "NULL" : reader.GetString(3);
        var companyId = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);
        var companyName = reader.IsDBNull(5) ? "NULL" : reader.GetString(5);
        
        Console.WriteLine($"用户名: {username}");
        Console.WriteLine($"姓名: {name}");
        Console.WriteLine($"部门ID: {deptId}");
        Console.WriteLine($"部门名: {affiliation}");
        Console.WriteLine($"公司ID: {companyId}");
        Console.WriteLine($"公司名: {companyName}");
        
        var isTarget = companyId == "16f1c1a4910426f41649fd14862b99a1" || companyId == "18e389224b660b4d67413f8466285581";
        Console.WriteLine($"\n匹配目标公司: {(isTarget ? "✓ 是" : "✗ 否")}");
        
        if (!isTarget && companyId != "NULL")
        {
            Console.WriteLine($"\n✗ 问题: 公司ID ({companyId.Substring(0, 8)}...) 不在目标列表中");
        }
        else if (companyId == "NULL")
        {
            Console.WriteLine("\n✗ 问题: LEFT JOIN后CompanyId为NULL，WHERE子句会过滤掉此记录");
        }
    }
    else
    {
        Console.WriteLine("✗ 未找到zhangjing在LEFT JOIN结果中!");
    }
}

// 最终视图过滤后的结果
Console.WriteLine("\n========== 4. 应用WHERE过滤后 ==========");
var sql4 = @"
WITH person_info AS (
    SELECT 
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
        COALESCE(
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
                    ELSE NULL END
                FROM dbo.sys_org_post_person spp
                INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
                WHERE spp.fd_personid = e.fd_id 
                    AND pe.fd_org_type = 4
                    AND pe.fd_is_available = 1
            ),
            (
                SELECT TOP 1 CASE 
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
                    WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
                    ELSE NULL END
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
    p.PersonName AS display_name
FROM person_info p
LEFT JOIN dept_company dc ON p.DeptId = dc.DeptId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
  AND p.LoginName = 'zhangjing';";

using (var cmd = new SqlCommand(sql4, conn))
using (var reader = cmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine($"✓ 找到: {reader.GetString(0)} - {reader.GetString(1)}");
    }
    else
    {
        Console.WriteLine("✗ WHERE过滤后张璟被排除了！");
        Console.WriteLine("原因: LEFT JOIN后dc.CompanyId为NULL，或不匹配目标公司");
    }
}

Console.WriteLine("\n========== 诊断完成 ==========");
