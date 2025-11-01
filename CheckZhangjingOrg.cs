using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;";
using var conn = new SqlConnection(connStr);
conn.Open();

Console.WriteLine("========== 张璟的完整组织关系 ==========\n");

var sql = @"
SELECT 
    e.fd_id,
    e.fd_name,
    e.fd_org_type,
    e.fd_parentid,
    e.fd_parentorgid,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = e.fd_parentid) AS parent_name,
    (SELECT fd_name FROM dbo.sys_org_element WHERE fd_id = e.fd_parentorgid) AS parentorg_name,
    (SELECT fd_org_type FROM dbo.sys_org_element WHERE fd_id = e.fd_parentid) AS parent_type,
    (SELECT fd_org_type FROM dbo.sys_org_element WHERE fd_id = e.fd_parentorgid) AS parentorg_type
FROM dbo.sys_org_element e 
WHERE e.fd_name = '张璟' AND e.fd_org_type = 8;";

using var cmd = new SqlCommand(sql, conn);
using var reader = cmd.ExecuteReader();

if (reader.Read())
{
    Console.WriteLine($"张璟 ID: {reader.GetString(0)}");
    Console.WriteLine($"类型: {reader.GetValue(2)} (8=人员)\n");
    
    if (!reader.IsDBNull(3))
    {
        var parentId = reader.GetString(3);
        var parentName = reader.GetString(5);
        var parentType = reader.IsDBNull(7) ? "NULL" : reader.GetValue(7).ToString();
        Console.WriteLine($"fd_parentid:");
        Console.WriteLine($"  ID: {parentId}");
        Console.WriteLine($"  名称: {parentName}");
        Console.WriteLine($"  类型: {parentType} (1=公司, 2=部门, 4=岗位)");
    }
    else
    {
        Console.WriteLine("fd_parentid: NULL");
    }
    
    Console.WriteLine();
    
    if (!reader.IsDBNull(4))
    {
        var parentOrgId = reader.GetString(4);
        var parentOrgName = reader.GetString(6);
        var parentOrgType = reader.IsDBNull(8) ? "NULL" : reader.GetValue(8).ToString();
        Console.WriteLine($"fd_parentorgid:");
        Console.WriteLine($"  ID: {parentOrgId}");
        Console.WriteLine($"  名称: {parentOrgName}");
        Console.WriteLine($"  类型: {parentOrgType} (1=公司, 2=部门, 4=岗位)");
    }
    else
    {
        Console.WriteLine("fd_parentorgid: NULL");
    }
}
else
{
    Console.WriteLine("未找到张璟！");
}

Console.WriteLine("\n========== 结论 ==========");
Console.WriteLine("视图逻辑问题：");
Console.WriteLine("1. person_info CTE优先检查fd_parentorgid是否为部门(类型=2)");
Console.WriteLine("2. 张璟的fd_parentorgid指向公司(类型=1)，不满足条件");
Console.WriteLine("3. 本应使用fd_parentid(技术管理部)，但COALESCE逻辑有误");
Console.WriteLine("4. 结果张璟被关联到党支部，党支部追溯不到目标公司");
Console.WriteLine("5. 最终被WHERE子句过滤掉\n");
Console.WriteLine("修复方案：person_info CTE应优先使用fd_parentid(直接父节点)");
