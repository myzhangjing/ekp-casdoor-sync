using System;
using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;";

Console.WriteLine("正在连接到EKP数据库...");
using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine("✓ 连接成功\n");

// 1. 查找"张璟"相关人员
Console.WriteLine("========== 1. 查找张璟相关人员 ==========");
var sql1 = @"
SELECT TOP 20
    p.fd_id,
    p.fd_login_name,
    e.fd_name AS display_name,
    e.fd_org_type,
    e.fd_is_available,
    e.fd_parentid,
    e.fd_parentorgid
FROM dbo.sys_org_person p
INNER JOIN dbo.sys_org_element e ON e.fd_id = p.fd_id
WHERE e.fd_name LIKE '%张璟%' 
   OR e.fd_name LIKE '%张菁%'
   OR e.fd_name LIKE '%章璟%'
   OR p.fd_login_name LIKE '%zhangjing%'
   OR p.fd_login_name LIKE '%zhang%jing%';";

using (var cmd = new SqlCommand(sql1, conn))
using (var reader = cmd.ExecuteReader())
{
    var found = 0;
    while (reader.Read())
    {
        found++;
        Console.WriteLine($"  ID: {reader.GetString(0)}");
        Console.WriteLine($"  登录名: {(reader.IsDBNull(1) ? "【无】" : reader.GetString(1))}");
        Console.WriteLine($"  显示名: {reader.GetString(2)}");
        Console.WriteLine($"  类型: {reader.GetString(3)} (8=人员)");
        Console.WriteLine($"  启用: {reader.GetString(4)}");
        Console.WriteLine($"  父ID: {(reader.IsDBNull(5) ? "NULL" : reader.GetString(5))}");
        Console.WriteLine($"  父组织ID: {(reader.IsDBNull(6) ? "NULL" : reader.GetString(6))}");
        Console.WriteLine();
    }
    if (found == 0)
    {
        Console.WriteLine("  ✗ 未找到任何相关人员！");
    }
}

// 2. 查找"技术管理部"
Console.WriteLine("\n========== 2. 查找技术管理部 ==========");
var sql2 = @"
SELECT 
    fd_id,
    fd_name,
    fd_org_type,
    fd_is_available,
    fd_parentid,
    fd_parentorgid
FROM dbo.sys_org_element
WHERE fd_name LIKE '%技术管理%'
  AND fd_org_type IN (1, 2)
  AND fd_is_available = 1;";

string? techDeptId = null;
using (var cmd = new SqlCommand(sql2, conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        techDeptId = reader.GetString(0);
        Console.WriteLine($"  ID: {techDeptId}");
        Console.WriteLine($"  名称: {reader.GetString(1)}");
        Console.WriteLine($"  类型: {reader.GetString(2)} (1=公司, 2=部门)");
        Console.WriteLine($"  父ID: {(reader.IsDBNull(4) ? "NULL" : reader.GetString(4))}");
        Console.WriteLine($"  父组织ID: {(reader.IsDBNull(5) ? "NULL" : reader.GetString(5))}");
        Console.WriteLine();
    }
}

if (techDeptId == null)
{
    Console.WriteLine("  ✗ 未找到技术管理部！");
    return;
}

// 3. 查找技术管理部下的所有人员
Console.WriteLine($"\n========== 3. 技术管理部 ({techDeptId}) 下的人员 ==========");
var sql3 = $@"
SELECT 
    p.fd_login_name,
    e.fd_name AS display_name,
    e.fd_id,
    e.fd_parentid,
    e.fd_parentorgid,
    CASE WHEN p.fd_login_name IS NULL THEN '无登录名' ELSE '有登录名' END AS has_login
FROM dbo.sys_org_element e
LEFT JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
WHERE e.fd_org_type = 8
  AND e.fd_is_available = 1
  AND (e.fd_parentid = '{techDeptId}' OR e.fd_parentorgid = '{techDeptId}');";

using (var cmd = new SqlCommand(sql3, conn))
using (var reader = cmd.ExecuteReader())
{
    Console.WriteLine("直接隶属:");
    var count = 0;
    while (reader.Read())
    {
        count++;
        var login = reader.IsDBNull(0) ? "【无登录名】" : reader.GetString(0);
        var name = reader.GetString(1);
        var hasLogin = reader.GetString(5);
        Console.WriteLine($"  {count}. {name} ({login}) - {hasLogin}");
    }
    if (count == 0)
    {
        Console.WriteLine("  (无)");
    }
}

// 4. 通过岗位关系
Console.WriteLine("\n通过岗位关系:");
var sql4 = $@"
SELECT DISTINCT
    p.fd_login_name,
    e.fd_name AS display_name,
    post.fd_name AS post_name,
    CASE WHEN p.fd_login_name IS NULL THEN '无登录名' ELSE '有登录名' END AS has_login
FROM dbo.sys_org_post_person spp
INNER JOIN dbo.sys_org_element post ON post.fd_id = spp.fd_postid
INNER JOIN dbo.sys_org_element e ON e.fd_id = spp.fd_personid
LEFT JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
WHERE e.fd_org_type = 8
  AND e.fd_is_available = 1
  AND post.fd_org_type = 4
  AND (post.fd_parentid = '{techDeptId}' OR post.fd_parentorgid = '{techDeptId}');";

using (var cmd = new SqlCommand(sql4, conn))
using (var reader = cmd.ExecuteReader())
{
    var count = 0;
    while (reader.Read())
    {
        count++;
        var login = reader.IsDBNull(0) ? "【无登录名】" : reader.GetString(0);
        var name = reader.GetString(1);
        var post = reader.GetString(2);
        var hasLogin = reader.GetString(3);
        Console.WriteLine($"  {count}. {name} ({login}) - 岗位: {post} - {hasLogin}");
    }
    if (count == 0)
    {
        Console.WriteLine("  (无)");
    }
}

// 5. 验证部门到公司的追溯
Console.WriteLine($"\n========== 4. 技术管理部到公司的追溯 ==========");
var sql5 = $@"
;WITH dept_path AS (
    SELECT 
        fd_id,
        fd_name,
        fd_org_type,
        fd_parentid,
        fd_parentorgid,
        0 AS level,
        CAST(fd_name AS NVARCHAR(MAX)) AS path
    FROM dbo.sys_org_element
    WHERE fd_id = '{techDeptId}'
    
    UNION ALL
    
    SELECT 
        e.fd_id,
        e.fd_name,
        e.fd_org_type,
        e.fd_parentid,
        e.fd_parentorgid,
        dp.level + 1,
        CAST(e.fd_name + ' > ' + dp.path AS NVARCHAR(MAX))
    FROM dbo.sys_org_element e
    INNER JOIN dept_path dp ON (e.fd_id = dp.fd_parentid OR e.fd_id = dp.fd_parentorgid)
    WHERE dp.level < 10
      AND e.fd_org_type IN (1, 2)
)
SELECT 
    level,
    fd_name,
    fd_org_type,
    fd_id,
    path,
    CASE 
        WHEN fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581') 
        THEN '✓ 目标公司' 
        ELSE '' 
    END AS is_target
FROM dept_path
ORDER BY level DESC;";

using (var cmd = new SqlCommand(sql5, conn))
using (var reader = cmd.ExecuteReader())
{
    var foundTarget = false;
    while (reader.Read())
    {
        var level = reader.GetInt32(0);
        var name = reader.GetString(1);
        var type = reader.GetString(2);
        var id = reader.GetString(3);
        var path = reader.GetString(4);
        var isTarget = reader.GetString(5);
        
        Console.WriteLine($"Level {level}: {name} (类型:{type}, ID:{id.Substring(0, Math.Min(8, id.Length))}...) {isTarget}");
        
        if (!string.IsNullOrEmpty(isTarget))
        {
            foundTarget = true;
            Console.WriteLine($"  完整路径: {path}");
        }
    }
    
    if (!foundTarget)
    {
        Console.WriteLine("\n✗ 警告: 技术管理部无法追溯到目标公司！");
        Console.WriteLine("  这就是为什么视图中没有该部门下的用户。");
        Console.WriteLine("  需要修复组织架构关系或修改视图逻辑。");
    }
}

Console.WriteLine("\n========== 诊断完成 ==========");
