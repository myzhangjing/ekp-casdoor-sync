using System;
using System.Linq;
using Microsoft.Data.SqlClient;

var connStr = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;";

using var conn = new SqlConnection(connStr);
conn.Open();

var sql = "SELECT TOP 20 Id, display_name, parent_id FROM vw_org_structure_sync ORDER BY Id";
using var cmd = new SqlCommand(sql, conn);
using var reader = cmd.ExecuteReader();

Console.WriteLine("组织ID | 组织名称 | 父组织ID");
Console.WriteLine(new string('-', 120));

while (reader.Read())
{
    var id = reader.GetString(0);
    var name = reader.GetString(1);
    var parentId = reader.IsDBNull(2) ? "<NULL>" : reader.GetString(2);
    
    Console.WriteLine($"{id} | {name} | {parentId}");
}

Console.WriteLine("\n分析父组织关系：");
reader.Close();

var sql2 = @"
SELECT 
    parent_id,
    COUNT(*) AS child_count
FROM vw_org_structure_sync
WHERE parent_id IS NOT NULL
GROUP BY parent_id
ORDER BY child_count DESC
";

using var cmd2 = new SqlCommand(sql2, conn);
using var reader2 = cmd2.ExecuteReader();

Console.WriteLine("\n父组织ID | 子组织数量");
Console.WriteLine(new string('-', 60));

while (reader2.Read())
{
    var parentId = reader2.GetString(0);
    var count = reader2.GetInt32(1);
    Console.WriteLine($"{parentId} | {count}");
}
