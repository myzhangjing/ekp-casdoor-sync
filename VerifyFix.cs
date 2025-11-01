using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;";

Console.WriteLine("========== 验证修复效果 ==========\n");
Console.WriteLine("连接数据库...");

using var conn = new SqlConnection(connStr);
conn.Open();
Console.WriteLine("✓ 连接成功\n");

Console.WriteLine("查询视图 vw_casdoor_users_sync 中的张璟...\n");

var sql = @"
SELECT TOP 5
    username,
    display_name,
    dept_id,
    affiliation,
    company_name
FROM dbo.vw_casdoor_users_sync
WHERE username = 'zhangjing' OR display_name LIKE '%张璟%';";

using var cmd = new SqlCommand(sql, conn);
using var reader = cmd.ExecuteReader();

var found = false;
while (reader.Read())
{
    found = true;
    var username = reader.GetString(0);
    var displayName = reader.GetString(1);
    var deptId = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
    var affiliation = reader.IsDBNull(3) ? "NULL" : reader.GetString(3);
    var companyName = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);
    
    Console.WriteLine($"✓ 找到: {displayName} ({username})");
    Console.WriteLine($"  部门ID: {deptId.Substring(0, Math.Min(8, deptId.Length))}...");
    Console.WriteLine($"  部门名: {affiliation}");
    Console.WriteLine($"  公司: {companyName}");
    Console.WriteLine();
}

if (!found)
{
    Console.WriteLine("✗ 未找到张璟！修复可能未生效。");
    Console.WriteLine("\n请手动运行:");
    Console.WriteLine("  .\\SyncEkpToCasdoor.exe --apply-optimized-views");
}
else
{
    Console.WriteLine("✓✓✓ 修复成功！张璟现在可以被视图查询到了。");
}

Console.WriteLine("\n========== 完成 ==========");
