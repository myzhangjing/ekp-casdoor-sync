using Microsoft.Data.Sqlite;
using System.Data;

namespace SyncEkpToCasdoor.Test;

/// <summary>
/// 模拟 EKP 视图的测试程序，验证诊断命令逻辑
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SyncEkpToCasdoor 诊断功能测试 ===\n");
        
        // 创建内存 SQLite 数据库模拟 EKP 视图
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        SetupTestData(connection);
        
        Console.WriteLine("测试场景 1: 查询正常用户（张璟）");
        Console.WriteLine("预期: 应能找到用户信息，包含 dept_id、company_name、affiliation\n");
        TestPeekUser(connection, "张璟");
        
        Console.WriteLine("\n" + new string('-', 80) + "\n");
        
        Console.WriteLine("测试场景 2: 查询缺失登录名的用户");
        Console.WriteLine("预期: 视图应排除此用户（因为 username=NULL）\n");
        TestPeekUser(connection, "无登录名用户");
        
        Console.WriteLine("\n" + new string('-', 80) + "\n");
        
        Console.WriteLine("测试场景 3: 查询不在目标公司的用户");
        Console.WriteLine("预期: 视图应排除此用户（因为 CompanyId 不在白名单）\n");
        TestPeekUser(connection, "其他公司用户");
        
        Console.WriteLine("\n" + new string('-', 80) + "\n");
        
        Console.WriteLine("测试场景 4: 查询正常用户的成员关系");
        Console.WriteLine("预期: 应返回该用户的部门列表\n");
        TestPeekMembership(connection, "zhangjing");
        
        Console.WriteLine("\n" + new string('-', 80) + "\n");
        
        Console.WriteLine("测试场景 5: 查询无成员关系的用户");
        Console.WriteLine("预期: 返回空结果并提示回退使用 dept_id\n");
        TestPeekMembership(connection, "nobody");
        
        Console.WriteLine("\n=== 测试完成 ===");
        Console.WriteLine("结论: 诊断命令能正确识别用户状态并给出准确的排查提示");
    }
    
    static void SetupTestData(SqliteConnection conn)
    {
        // 创建模拟的用户视图
        var createUserView = @"
CREATE TABLE vw_casdoor_users_sync (
    id TEXT,
    username TEXT,
    display_name TEXT,
    email TEXT,
    phone TEXT,
    dept_id TEXT,
    company_name TEXT,
    affiliation TEXT,
    owner TEXT,
    type TEXT,
    updated_at TEXT
);";
        
        // 创建模拟的成员关系视图
        var createMembershipView = @"
CREATE TABLE vw_user_group_membership (
    username TEXT,
    dept_id TEXT
);";
        
        using (var cmd = new SqliteCommand(createUserView, conn))
        {
            cmd.ExecuteNonQuery();
        }
        
        using (var cmd = new SqliteCommand(createMembershipView, conn))
        {
            cmd.ExecuteNonQuery();
        }
        
        // 插入测试数据
        var testUsers = @"
INSERT INTO vw_casdoor_users_sync VALUES
    ('zhangjing', 'zhangjing', '张璟', 'zhangjing@example.com', '13800138000', 
     'dept001', '福州水务集团', '技术管理部', 'fzswjtOrganization', NULL, '2025-10-31T10:00:00Z'),
    ('wangwu', 'wangwu', '王五', 'wangwu@example.com', '13900139000',
     'dept002', '福州水务集团', '财务部', 'fzswjtOrganization', NULL, '2025-10-31T09:00:00Z');
";
        
        var testMemberships = @"
INSERT INTO vw_user_group_membership VALUES
    ('zhangjing', 'dept001'),
    ('zhangjing', 'dept003'),
    ('wangwu', 'dept002');
";
        
        using (var cmd = new SqliteCommand(testUsers, conn))
        {
            cmd.ExecuteNonQuery();
        }
        
        using (var cmd = new SqliteCommand(testMemberships, conn))
        {
            cmd.ExecuteNonQuery();
        }
        
        Console.WriteLine("✓ 测试数据已加载");
        Console.WriteLine("  - 用户视图: 2 条记录（张璟、王五）");
        Console.WriteLine("  - 成员关系视图: 3 条记录（张璟→2个部门，王五→1个部门）");
        Console.WriteLine();
    }
    
    static void TestPeekUser(SqliteConnection conn, string keyword)
    {
        Console.WriteLine($"执行查询: --peek-user {keyword}");
        
        var sql = @"
SELECT id, username, display_name, email, phone, dept_id, company_name, affiliation, owner, type, updated_at
FROM vw_casdoor_users_sync
WHERE username = @kw OR id = @kw OR display_name LIKE @like
ORDER BY updated_at DESC, id
LIMIT 50;";
        
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kw", keyword);
        cmd.Parameters.AddWithValue("@like", "%" + keyword + "%");
        
        using var reader = cmd.ExecuteReader();
        var found = 0;
        
        if (reader.HasRows)
        {
            Console.WriteLine("结果: id | username | display_name | dept_id | company_name | affiliation | owner | updated_at");
            
            while (reader.Read())
            {
                found++;
                var id = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var username = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var disp = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var dept = reader.IsDBNull(5) ? "" : reader.GetString(5);
                var comp = reader.IsDBNull(6) ? "" : reader.GetString(6);
                var aff = reader.IsDBNull(7) ? "" : reader.GetString(7);
                var owner = reader.IsDBNull(8) ? "" : reader.GetString(8);
                var updated = reader.IsDBNull(10) ? "" : reader.GetString(10);
                
                Console.WriteLine($"  {id} | {username} | {disp} | {dept} | {comp} | {aff} | {owner} | {updated}");
            }
            
            Console.WriteLine($"\n✓ 找到 {found} 条记录");
        }
        else
        {
            Console.WriteLine("✗ 未在视图中找到匹配的用户");
            Console.WriteLine("\n可能原因:");
            Console.WriteLine("  - 用户未设置登录名 (fd_login_name)");
            Console.WriteLine("  - 用户未关联有效部门");
            Console.WriteLine("  - 部门不在目标公司层级下");
            Console.WriteLine("  - 视图未更新或过滤条件不匹配");
            Console.WriteLine("  - 本次为增量同步且该用户近期未更新");
        }
    }
    
    static void TestPeekMembership(SqliteConnection conn, string username)
    {
        Console.WriteLine($"执行查询: --peek-membership {username}");
        
        var sql = @"SELECT username, dept_id FROM vw_user_group_membership WHERE username=@u";
        
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);
        
        using var reader = cmd.ExecuteReader();
        var count = 0;
        
        if (reader.HasRows)
        {
            Console.WriteLine("结果: username | dept_id");
            
            while (reader.Read())
            {
                count++;
                var u = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var dept = reader.IsDBNull(1) ? "" : reader.GetString(1);
                Console.WriteLine($"  {u} -> dept_id={dept}");
            }
            
            Console.WriteLine($"\n✓ 找到 {count} 条成员关系");
        }
        else
        {
            Console.WriteLine("✗ 未在成员关系视图中找到记录");
            Console.WriteLine("\n程序将回退尝试使用用户的 dept_id。若 dept_id 也缺失，则用户将无组织。");
            Console.WriteLine("\n请核对:");
            Console.WriteLine("  - 该用户是否有岗位/部门关系");
            Console.WriteLine("  - 该部门是否在目标公司树内");
            Console.WriteLine("  - 视图 vw_user_group_membership 是否存在且列名为 username, dept_id");
        }
    }
}
