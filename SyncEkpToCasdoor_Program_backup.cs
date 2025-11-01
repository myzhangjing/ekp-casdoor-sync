using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace SyncEkpToCasdoor;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("EKP -> Casdoor 同步程序启动。");

        try
        {
            // 解析带参数的调试命令（必须尽早处理）
            string? GetArgValue(string flag)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length) return args[i + 1];
                        return null;
                    }
                }
                return null;
            }

            // 修复视图命令（仅需EKP连接字符串）
            if (args.Contains("--fix-view", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("执行视图修复...");
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                FixOrgHierarchyView(connStr);
                return 0;
            }

            // 检查组织视图数量
            if (args.Contains("--check-org-view", StringComparer.OrdinalIgnoreCase))
            {
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                CheckOrgView(connStr);
                return 0;
            }

            // 查看组织视图示例数据
            if (args.Contains("--peek-org-view", StringComparer.OrdinalIgnoreCase))
            {
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                PeekOrgView(connStr);
                return 0;
            }

            // 查看用户视图中的指定用户（按用户名或显示名模糊查询）
            var peekUserKey = GetArgValue("--peek-user");
            if (!string.IsNullOrWhiteSpace(peekUserKey))
            {
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                PeekUserView(connStr, peekUserKey!);
                return 0;
            }

            // 查看成员关系视图中某一用户的组织列表
            var peekMemberUser = GetArgValue("--peek-membership");
            if (!string.IsNullOrWhiteSpace(peekMemberUser))
            {
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                PeekMembershipView(connStr, peekMemberUser!);
                return 0;
            }

            // 直连 Casdoor MySQL 检查 group 表的父子关系
            if (args.Contains("--check-casdoor-db", StringComparer.OrdinalIgnoreCase))
            {
                var host = Environment.GetEnvironmentVariable("CASDOOR_DB_HOST") ?? "127.0.0.1";
                var port = Environment.GetEnvironmentVariable("CASDOOR_DB_PORT") ?? "3306";
                var user = Environment.GetEnvironmentVariable("CASDOOR_DB_USER") ?? "root";
                var pwd = Environment.GetEnvironmentVariable("CASDOOR_DB_PASSWORD") ?? string.Empty;
                var db  = Environment.GetEnvironmentVariable("CASDOOR_DB_NAME") ?? "casdoor";
                CheckCasdoorDb(host, port, user, pwd, db);
                return 0;
            }

            // 从导出的层级CSV修复 Casdoor group.parent_id
            if (args.Contains("--fix-casdoor-parentid-from-csv", StringComparer.OrdinalIgnoreCase))
            {
                var host = Environment.GetEnvironmentVariable("CASDOOR_DB_HOST") ?? "127.0.0.1";
                var port = Environment.GetEnvironmentVariable("CASDOOR_DB_PORT") ?? "3306";
                var user = Environment.GetEnvironmentVariable("CASDOOR_DB_USER") ?? "root";
                var pwd = Environment.GetEnvironmentVariable("CASDOOR_DB_PASSWORD") ?? string.Empty;
                var db  = Environment.GetEnvironmentVariable("CASDOOR_DB_NAME") ?? "casdoor";
                var owner = Environment.GetEnvironmentVariable("CASDOOR_DEFAULT_OWNER") ?? Environment.GetEnvironmentVariable("DEFAULT_OWNER") ?? "fzswjtOrganization";
                var csvPath = Environment.GetEnvironmentVariable("CASDOOR_HIERARCHY_CSV");
                FixCasdoorParentIdFromCsv(host, port, user, pwd, db, owner, csvPath);
                return 0;
            }
            
            // 更新用户视图以包含密码字段
            if (args.Contains("--update-user-view", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("更新用户视图以包含密码字段...");
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                UpdateUserViewWithPassword(connStr);
                return 0;
            }

            // 一键应用优化后的视图（用户视图 + 成员关系视图 + 组织视图）
            if (args.Contains("--apply-optimized-views", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("应用优化后的视图定义（用户/成员关系/组织）...");
                var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("缺少必要环境变量：EKP_SQLSERVER_CONN");
                ApplyOptimizedViews(connStr);
                Console.WriteLine("✓ 视图优化已应用");
                return 0;
            }
            
            var config = AppConfig.LoadFromEnvironment();
            
            var stateStore = new SyncStateStore(config.StateFilePath);
            var state = stateStore.Load();

            var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase) ||
                         string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "true", StringComparison.OrdinalIgnoreCase);

            if (dryRun)
            {
                Console.WriteLine("当前运行在演练模式，仅打印配置信息：");
                Console.WriteLine($"  状态文件：{config.StateFilePath}");
                Console.WriteLine($"  增量时间：{config.SinceUtc?.ToString("o") ?? "全量"}");
                Console.WriteLine($"  组织同步视图：{config.MembershipViewName ?? "自动推导"}");
                return 0;
            }

            using var ekp = new EkpRepository(config.EkpConnectionString);
            using var casdoor = new SimpleCasdoorRepository(config);  // 使用简化版仓储
            var service = new SyncService(ekp, casdoor, config, state);

            var purgeFlag = args.Contains("--purge-except-built-in", StringComparer.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_EXCEPT_BUILTIN"), "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_EXCEPT_BUILTIN"), "true", StringComparison.OrdinalIgnoreCase);
            var purgeOnly = args.Contains("--purge-only", StringComparer.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_ONLY"), "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Environment.GetEnvironmentVariable("PURGE_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

            if (purgeFlag)
            {
                Console.WriteLine($"开始执行清理：仅保留 owner = {config.DefaultOwner} 的用户与组织。");
                service.PurgeExceptOwner(config.DefaultOwner);
                Console.WriteLine("清理完成。");
                if (purgeOnly)
                {
                    Console.WriteLine("根据参数设置，仅执行清理步骤，程序结束。");
                    state.LastRunUtc = DateTime.UtcNow;
                    stateStore.Save(state);
                    return 0;
                }
            }

            service.SyncGroups();
            service.SyncUsers();
            service.SyncMemberships();

            foreach (var enforcer in config.EnforcerNames)
            {
                casdoor.RefreshEnforcer(enforcer.owner, enforcer.name);
            }

            state.LastRunUtc = DateTime.UtcNow;
            stateStore.Save(state);
            Console.WriteLine($"同步流程结束，检查点已写入：{config.StateFilePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"同步失败：{ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PeekUserView(string connectionString, string keyword)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        Console.WriteLine($"查询 vw_casdoor_users_sync 中的用户，关键字: {keyword}");

        // 支持精确用户名匹配或显示名模糊匹配
        var sql = @"
SELECT TOP 50 id, username, display_name, email, phone, dept_id, company_name, affiliation, owner, type, updated_at
FROM vw_casdoor_users_sync WITH (NOLOCK)
WHERE username = @kw OR id = @kw OR display_name LIKE @like
ORDER BY updated_at DESC, id;";

        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@kw", keyword);
        cmd.Parameters.AddWithValue("@like", "%" + keyword + "%");

        using var reader = cmd.ExecuteReader();
        var found = 0;
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
            var updated = reader.IsDBNull(10) ? "" : reader.GetDateTime(10).ToString("o");
            Console.WriteLine($"  {id} | {username} | {disp} | {dept} | {comp} | {aff} | {owner} | {updated}");
        }

        if (found == 0)
        {
            Console.WriteLine("未在视图中找到匹配的用户。可能原因：\n- 用户未设置登录名(fd_login_name)\n- 用户未关联有效部门\n- 部门不在目标公司层级下\n- 视图未更新或过滤条件不匹配\n- 本次为增量同步且该用户近期未更新");
        }
    }

    private static void PeekMembershipView(string connectionString, string username)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        Console.WriteLine($"查询 vw_user_group_membership 中 {username} 的组织信息...");
        var sql = @"SELECT username, dept_id FROM vw_user_group_membership WHERE username=@u";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@u", username);

        using var reader = cmd.ExecuteReader();
        var count = 0;
        while (reader.Read())
        {
            count++;
            var u = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var dept = reader.IsDBNull(1) ? "" : reader.GetString(1);
            Console.WriteLine($"  {u} -> dept_id={dept}");
        }
        if (count == 0)
        {
            Console.WriteLine("未在成员关系视图中找到记录，程序将回退尝试使用用户的 dept_id。若 dept_id 也缺失，则用户将无组织。\n请核对：\n- 该用户是否有岗位/部门关系\n- 该部门是否在目标公司树内\n- 视图 vw_user_group_membership 是否存在且列名为 username, dept_id");
        }
    }

    private static void FixCasdoorParentIdFromCsv(string host, string port, string user, string pwd, string database, string owner, string? csvPath)
    {
        // 定位CSV
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logsDir))
            {
                throw new InvalidOperationException($"未找到日志目录: {logsDir}");
            }
            var latest = Directory.EnumerateFiles(logsDir, "organization_hierarchy_*.csv")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (latest == null)
            {
                throw new InvalidOperationException("未找到任何组织层级CSV文件");
            }
            csvPath = latest.FullName;
        }

        Console.WriteLine($"使用CSV: {csvPath}");
        var lines = File.ReadAllLines(csvPath!);
        if (lines.Length <= 1)
        {
            Console.WriteLine("CSV为空或仅有表头，无需处理。");
            return;
        }

        // 解析表头索引
        var header = lines[0].Split(',');
        int idxOrgId = Array.FindIndex(header, h => h.Contains("组织ID", StringComparison.OrdinalIgnoreCase));
        int idxParentCasdoor = Array.FindIndex(header, h => h.Contains("Casdoor父组织名称", StringComparison.OrdinalIgnoreCase));
        if (idxOrgId < 0 || idxParentCasdoor < 0)
        {
            throw new InvalidOperationException("CSV表头缺少 组织ID 或 Casdoor父组织名称 字段");
        }

        var cs = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.TryParse(port, out var p) ? p : 3306,
            UserID = user,
            Password = pwd,
            Database = database,
            CharacterSet = "utf8mb4",
            ConnectionTimeout = 10,
            DefaultCommandTimeout = 30
        }.ConnectionString;

        using var conn = new MySqlConnection(cs);
        conn.Open();
        Console.WriteLine($"连接 MySQL 成功，准备更新 owner={owner} 的 parent_id ...");

        long toUpdate = 0, updated = 0;
        using var tx = conn.BeginTransaction();
        try
        {
            for (int i = 1; i < lines.Length; i++)
            {
                var raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // 简单CSV分割（字段中有逗号的已被引号包裹，这里采用粗略处理，足够应对当前导出格式）
                var cols = SplitCsvLine(raw);
                if (cols.Length <= Math.Max(idxOrgId, idxParentCasdoor)) continue;

                var orgId = cols[idxOrgId].Trim('"');
                var parentFull = cols[idxParentCasdoor].Trim('"'); // 例如 fzswjtOrganization/18e9...
                if (string.IsNullOrWhiteSpace(orgId)) continue;

                string parentNameOnly;
                if (!string.IsNullOrWhiteSpace(parentFull))
                {
                    var slash = parentFull.LastIndexOf('/');
                    parentNameOnly = slash >= 0 ? parentFull[(slash + 1)..] : parentFull;
                }
                else
                {
                    // 根节点：parent_id 设为 owner
                    parentNameOnly = owner;
                }

                toUpdate++;
                using var cmd = new MySqlCommand("UPDATE `group` SET parent_id = @pid WHERE owner=@o AND name=@n", conn, tx);
                cmd.Parameters.AddWithValue("@pid", parentNameOnly);
                cmd.Parameters.AddWithValue("@o", owner);
                cmd.Parameters.AddWithValue("@n", orgId);
                updated += cmd.ExecuteNonQuery();
            }

            tx.Commit();
            Console.WriteLine($"✓ 更新完成: 目标 {toUpdate} 条, 影响行数 {updated}");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 转义双引号
                    cur.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(cur.ToString());
                cur.Clear();
            }
            else
            {
                cur.Append(ch);
            }
        }
        result.Add(cur.ToString());
        return result.ToArray();
    }

    private static void CheckCasdoorDb(string host, string port, string user, string pwd, string database)
    {
        var cs = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.TryParse(port, out var p) ? p : 3306,
            UserID = user,
            Password = pwd,
            Database = database,
            CharacterSet = "utf8mb4",
            ConnectionTimeout = 10,
            DefaultCommandTimeout = 30
        }.ConnectionString;

        Console.WriteLine($"连接 Casdoor 数据库 {host}:{port}/{database} ...");
        using var conn = new MySqlConnection(cs);
        conn.Open();
        Console.WriteLine("✓ 已连接");

        // 列出 group 表字段
        var groupColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new MySqlCommand("SHOW COLUMNS FROM `group`", conn))
        using (var r = cmd.ExecuteReader())
        {
            Console.WriteLine("\n`group` 表字段:");
            while (r.Read())
            {
                var col = r.GetString(0);
                groupColumns.Add(col);
                Console.WriteLine($"  - {col} {r.GetString(1)}");
            }
        }

        // 总数与父字段统计
        string owner = Environment.GetEnvironmentVariable("CASDOOR_DEFAULT_OWNER") ?? Environment.GetEnvironmentVariable("DEFAULT_OWNER") ?? "fzswjtOrganization";
        Console.WriteLine($"\n统计 owner={owner} 的组织:");
        int total;
        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM `group` WHERE owner=@o", conn))
        {
            cmd.Parameters.AddWithValue("@o", owner);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }
        Console.WriteLine($"  总数: {total}");

        // 统计 parent_id / parent_name 的空值与非空值
        var hasParentName = groupColumns.Contains("parent_name");
        var statSql = hasParentName
            ? @"SELECT 
  SUM(CASE WHEN (parent_id IS NULL OR parent_id='') THEN 1 ELSE 0 END) AS no_parent_id,
  SUM(CASE WHEN (parent_id IS NOT NULL AND parent_id<>'') THEN 1 ELSE 0 END) AS with_parent_id,
  SUM(CASE WHEN (parent_name IS NULL OR parent_name='') THEN 1 ELSE 0 END) AS no_parent_name,
  SUM(CASE WHEN (parent_name IS NOT NULL AND parent_name<>'') THEN 1 ELSE 0 END) AS with_parent_name
FROM `group`
WHERE owner=@o;"
            : @"SELECT 
  SUM(CASE WHEN (parent_id IS NULL OR parent_id='') THEN 1 ELSE 0 END) AS no_parent_id,
  SUM(CASE WHEN (parent_id IS NOT NULL AND parent_id<>'') THEN 1 ELSE 0 END) AS with_parent_id
FROM `group`
WHERE owner=@o;";
        using (var cmd = new MySqlCommand(statSql, conn))
        {
            cmd.Parameters.AddWithValue("@o", owner);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                Console.WriteLine($"  parent_id 为空: {r.GetInt64(0)}, 非空: {r.GetInt64(1)}");
                if (hasParentName)
                {
                    Console.WriteLine($"  parent_name为空: {r.GetInt64(2)}, 非空: {r.GetInt64(3)}");
                }
            }
        }

        // 抽样前 20 条查看父链
        Console.WriteLine(hasParentName
            ? "\n示例(前20条): name | display_name | parent_id | parent_name"
            : "\n示例(前20条): name | display_name | parent_id");
        var sampleSql = hasParentName
            ? @"SELECT name, display_name, parent_id, parent_name FROM `group` WHERE owner=@o ORDER BY display_name LIMIT 20;"
            : @"SELECT name, display_name, parent_id FROM `group` WHERE owner=@o ORDER BY display_name LIMIT 20;";
        using (var cmd = new MySqlCommand(sampleSql, conn))
        {
            cmd.Parameters.AddWithValue("@o", owner);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.IsDBNull(0) ? "" : r.GetString(0);
                var disp = r.IsDBNull(1) ? "" : r.GetString(1);
                var pid  = r.IsDBNull(2) ? "" : r.GetString(2);
                if (hasParentName)
                {
                    var pname= r.IsDBNull(3) ? "" : r.GetString(3);
                    Console.WriteLine($"  {name} | {disp} | {pid} | {pname}");
                }
                else
                {
                    Console.WriteLine($"  {name} | {disp} | {pid}");
                }
            }
        }

        // 检查根节点（两个目标公司）的 parent_id 是否正确设为 owner
        Console.WriteLine("\n检查根节点公司:");
        using (var cmd = new MySqlCommand(@"SELECT name, display_name, parent_id 
FROM `group` 
WHERE owner=@o AND name IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
ORDER BY name;", conn))
        {
            cmd.Parameters.AddWithValue("@o", owner);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.IsDBNull(0) ? "" : r.GetString(0);
                var disp = r.IsDBNull(1) ? "" : r.GetString(1);
                var pid = r.IsDBNull(2) ? "" : r.GetString(2);
                var status = pid == owner ? "✓ 正确(parent_id=owner)" : $"✗ 错误(parent_id={pid})";
                Console.WriteLine($"  {disp}: {status}");
            }
        }
    }
    
    private static void UpdateUserViewWithPassword(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        
        // 先删除视图
        var dropSql = "IF OBJECT_ID('vw_casdoor_users_sync', 'V') IS NOT NULL DROP VIEW vw_casdoor_users_sync;";
        using (var cmd = new SqlCommand(dropSql, conn) { CommandTimeout = 120 })
        {
            cmd.ExecuteNonQuery();
        }
        
        // 再创建视图（基于现有优化版本，添加 password_md5 字段）
        var createSql = @"
CREATE VIEW [dbo].[vw_casdoor_users_sync] AS
WITH elem AS (
    SELECT 
        fd_id AS ElemId,
        fd_name AS ElemName,
        fd_org_type AS OrgType,
        fd_parentid AS ParentId,
        fd_parentorgid AS ParentOrgId
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
),
person AS (
    SELECT 
        e.ElemId AS PersonId,
        e.ElemName AS PersonName,
        p.fd_login_name AS LoginName,
        p.fd_email AS Email,
        p.fd_mobile_no AS MobileNo,
        p.fd_sex AS Sex,
        p.fd_password AS PasswordMd5
    FROM elem e
    INNER JOIN dbo.sys_org_person p ON e.ElemId = p.fd_id
    WHERE e.OrgType = 8
      AND p.fd_login_name IS NOT NULL
),
post AS (
    SELECT 
        ElemId AS PostId,
        ParentId,
        ParentOrgId
    FROM elem
    WHERE OrgType = 4
),
dept AS (
    SELECT 
        ElemId AS DeptId,
        ElemName AS DeptName
    FROM elem
    WHERE OrgType IN (1, 2)
),
company AS (
    SELECT 
        ElemId AS CompanyId,
        ElemName AS CompanyName
    FROM elem
    WHERE OrgType = 1
),
person_dept AS (
    SELECT 
        x.PersonId,
        x.DeptId,
        ROW_NUMBER() OVER (PARTITION BY x.PersonId ORDER BY (SELECT 1)) AS RowNum
    FROM (
        SELECT DISTINCT p.PersonId, COALESCE(po.ParentOrgId, po.ParentId) AS DeptId
        FROM person p
        INNER JOIN dbo.sys_org_post_person spp ON spp.fd_personid = p.PersonId
        INNER JOIN post po ON po.PostId = spp.fd_postid
        
        UNION
        
        SELECT p.PersonId, COALESCE(pe.ParentOrgId, pe.ParentId) AS DeptId
        FROM person p
        INNER JOIN elem pe ON pe.ElemId = p.PersonId AND pe.OrgType = 8
        WHERE NOT EXISTS (SELECT 1 FROM dbo.sys_org_post_person spp WHERE spp.fd_personid = p.PersonId)
    ) AS x
),
dept_company AS (
    SELECT DISTINCT
        d.DeptId,
        COALESCE(
            (SELECT TOP 1 c.CompanyId FROM company c WHERE c.CompanyId = d.DeptId),
            (SELECT TOP 1 se.fd_parentorgid 
             FROM dbo.sys_org_element se 
             WHERE se.fd_id = d.DeptId AND se.fd_parentorgid IN (SELECT CompanyId FROM company))
        ) AS CompanyId
    FROM dept d
),
seed AS (
    SELECT 
        d.DeptId,
        d.DeptName,
        CAST(d.DeptId AS nvarchar(MAX)) AS Path,
        0 AS Level
    FROM dept d
    WHERE d.DeptId IN (SELECT CompanyId FROM company)
    
    UNION ALL
    
    SELECT 
        e.fd_id,
        e.fd_name,
        s.Path,
        s.Level + 1
    FROM dbo.sys_org_element e
    INNER JOIN seed s ON (e.fd_parentid = s.DeptId OR e.fd_parentorgid = s.DeptId)
    WHERE e.fd_org_type IN (1, 2)
      AND e.fd_is_available = 1
),
ascend AS (
    SELECT 
        DeptId,
        DeptName,
        Path,
        Level,
        CAST(DeptName AS nvarchar(MAX)) AS FullPath
    FROM seed
    WHERE Level = 0
    
    UNION ALL
    
    SELECT 
        s.DeptId,
        s.DeptName,
        s.Path,
        s.Level,
        CAST(a.FullPath + N' / ' + s.DeptName AS nvarchar(MAX))
    FROM seed s
    INNER JOIN ascend a ON s.Path = a.DeptId
    WHERE s.Level = a.Level + 1
),
aff_path AS (
    SELECT 
        DeptId,
        MAX(FullPath) AS Affiliation
    FROM ascend
    GROUP BY DeptId
)
SELECT 
    p.LoginName AS id,
    p.LoginName AS username,
    p.PersonName AS display_name,
    p.Email AS email,
    p.MobileNo AS phone,
    pe.fd_create_time AS created_at,
    pe.fd_alter_time AS updated_at,
    CASE 
        WHEN p.Sex = 'M' THEN 'Male'
        WHEN p.Sex = 'F' THEN 'Female'
        ELSE ''
    END AS gender,
    N'zh' AS language,
    pd.DeptId AS dept_id,
    c.CompanyName AS company_name,
    ISNULL(ap.Affiliation, d.DeptName) AS affiliation,
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    CAST(NULL AS nvarchar(50)) AS type,
    p.PasswordMd5 AS password_md5
FROM person p
INNER JOIN dbo.sys_org_element pe ON pe.fd_id = p.PersonId AND pe.fd_org_type = 8
LEFT JOIN person_dept pd ON p.PersonId = pd.PersonId AND pd.RowNum = 1
LEFT JOIN dept_company dc ON pd.DeptId = dc.DeptId
LEFT JOIN dept d ON pd.DeptId = d.DeptId
LEFT JOIN aff_path ap ON pd.DeptId = ap.DeptId
LEFT JOIN company c ON dc.CompanyId = c.CompanyId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581');
";
        using (var cmd = new SqlCommand(createSql, conn) { CommandTimeout = 120 })
        {
            cmd.ExecuteNonQuery();
        }
        
        Console.WriteLine("✓ 已更新 vw_casdoor_users_sync 视图，新增 password_md5 字段");
    }

    private static void ApplyOptimizedViews(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        // 优化版用户视图（含 password_md5）
        var dropUsers = "IF OBJECT_ID('vw_casdoor_users_sync', 'V') IS NOT NULL DROP VIEW vw_casdoor_users_sync;";
        using (var cmd = new SqlCommand(dropUsers, conn) { CommandTimeout = 300 })
            cmd.ExecuteNonQuery();

        var usersSql = @"
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
    (SELECT fd_name 
     FROM dbo.sys_org_element 
     WHERE fd_id = dc.CompanyId) AS company_name,
    dc.DeptName AS affiliation,
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    NULL AS type,
    p.PasswordMd5 AS password_md5
FROM person_info p
LEFT JOIN dept_company dc ON p.DeptId = dc.DeptId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581');
";
        using (var cmd = new SqlCommand(usersSql, conn) { CommandTimeout = 300 })
            cmd.ExecuteNonQuery();

        // 优化版成员关系视图（列名与程序匹配：username, dept_id）
        var dropMem = "IF OBJECT_ID('vw_user_group_membership', 'V') IS NOT NULL DROP VIEW vw_user_group_membership;";
        using (var cmd = new SqlCommand(dropMem, conn) { CommandTimeout = 300 })
            cmd.ExecuteNonQuery();

        var memSql = @"
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
        using (var cmd = new SqlCommand(memSql, conn) { CommandTimeout = 300 })
            cmd.ExecuteNonQuery();

        // 同时修复/优化组织视图（使用已有方法）
        FixOrgHierarchyView(connectionString);
    }

    private static void CheckOrgView(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM vw_org_structure_sync", conn))
        {
            cmd.CommandTimeout = 120;
            var total = (int)cmd.ExecuteScalar();
            Console.WriteLine($"vw_org_structure_sync 总记录: {total}");
        }

        using (var cmd = new SqlCommand("SELECT COUNT(DISTINCT id) FROM vw_org_structure_sync", conn))
        {
            cmd.CommandTimeout = 120;
            var distinct = (int)cmd.ExecuteScalar();
            Console.WriteLine($"vw_org_structure_sync 去重后ID数: {distinct}");
        }

        var depthSql = @"
WITH depth_calc AS (
    SELECT 
        v.id,
        v.parent_id,
        CASE 
            WHEN v.parent_id IS NULL THEN 0
            WHEN v.parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL) THEN 1
            WHEN v.parent_id IN (
                SELECT id FROM dbo.vw_org_structure_sync 
                WHERE parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL)
            ) THEN 2
            WHEN v.parent_id IN (
                SELECT id FROM dbo.vw_org_structure_sync v2
                WHERE v2.parent_id IN (
                    SELECT id FROM dbo.vw_org_structure_sync v3
                    WHERE v3.parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL)
                )
            ) THEN 3
            ELSE 4
        END AS depth_level
    FROM dbo.vw_org_structure_sync v
)
SELECT depth_level, COUNT(*) cnt FROM depth_calc GROUP BY depth_level ORDER BY depth_level;";

        using (var cmd2 = new SqlCommand(depthSql, conn))
        using (var reader = cmd2.ExecuteReader())
        {
            Console.WriteLine("层级分布:");
            while (reader.Read())
            {
                Console.WriteLine($"  层级 {reader.GetInt32(0)}: {reader.GetInt32(1)}");
            }
        }
    }

    private static void PeekOrgView(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        var sql = @"SELECT TOP 20 id, name, display_name, parent_id, type, owner FROM vw_org_structure_sync ORDER BY display_name";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        using var reader = cmd.ExecuteReader();
        Console.WriteLine("示例数据: id | name | display_name | parent_id | type | owner");
        while (reader.Read())
        {
            var id = reader.IsDBNull(0) ? "<null>" : reader.GetString(0);
            var name = reader.IsDBNull(1) ? "<null>" : reader.GetString(1);
            var disp = reader.IsDBNull(2) ? "<null>" : reader.GetString(2);
            var parent = reader.IsDBNull(3) ? "<null>" : reader.GetString(3);
            var type = reader.IsDBNull(4) ? "<null>" : reader.GetString(4);
            var owner = reader.IsDBNull(5) ? "<null>" : reader.GetString(5);
            Console.WriteLine($"  {id} | {name} | {disp} | {parent} | {type} | {owner}");
        }
    }

    private static void FixOrgHierarchyView(string connectionString)
    {
        // 修正视图定义：
        // 1) 根节点 parent_id 为 NULL
        // 2) 递归优先使用 fd_parentid（直接父级，且父级必须为 1/2 类型），否则回退到 fd_parentorgid
        // 3) 仅纳入 org_type IN (1,2) 的公司与部门节点，保证多层结构
        // 4) 通过预计算 parent_candidate_id 简化递归连接条件，减少过滤误差
        var sql = @"
ALTER VIEW [dbo].[vw_org_structure_sync] AS
WITH 
target_companies AS (
    SELECT fd_id AS company_id
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
      AND fd_org_type = 1
      AND fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
),
base AS (
    -- 仅选取公司/部门，并预先计算 parent_candidate_id：
    -- 若 fd_parentid 指向的节点为 1/2 类型，则使用之；否则回退 fd_parentorgid
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
    -- 锚点：目标公司本身
    SELECT * FROM roots
    UNION ALL
    -- 递归：使用预计算的 parent_candidate_id 与上一层 id 连接
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

        using var conn = new SqlConnection(connectionString);
        conn.Open();

        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        cmd.ExecuteNonQuery();

        Console.WriteLine("✓ 视图修复成功");
    }
}

internal sealed class AppConfig
{
    public string EkpConnectionString { get; init; } = string.Empty;
    public string CasdoorEndpoint { get; init; } = string.Empty;
    public string CasdoorClientId { get; init; } = string.Empty;
    public string CasdoorClientSecret { get; init; } = string.Empty;
    public string? CasdoorOrganization { get; init; }
    public string? CasdoorApplication { get; init; }
    public string DefaultOwner { get; init; } = "built-in";
    public bool ForceOwnerRefresh { get; init; }
    public bool MinimalMode { get; init; }
    public string? MembershipViewName { get; init; }
    public DateTime? SinceUtc { get; init; }
    public string StateFilePath { get; init; } = "sync_state.json";
    public IReadOnlyList<(string owner, string name)> EnforcerNames { get; init; } = Array.Empty<(string owner, string name)>();

    public static AppConfig LoadFromEnvironment()
    {
        string Require(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"缺少必要环境变量：{key}");
            return value;
        }

        string? Optional(string key) => Environment.GetEnvironmentVariable(key);

    var defaultOwner = Optional("CASDOOR_DEFAULT_OWNER") ?? Optional("DEFAULT_OWNER") ?? "built-in";
        DateTime? since = null;
        var sinceRaw = Optional("SYNC_SINCE_UTC");
        if (!string.IsNullOrWhiteSpace(sinceRaw) && DateTime.TryParse(sinceRaw, null,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            since = parsed.ToUniversalTime();
        }

        var enforcerRaw = Optional("CASDOOR_ENFORCERS");
        var enforcers = new List<(string owner, string name)>();
        if (!string.IsNullOrWhiteSpace(enforcerRaw))
        {
            foreach (var part in enforcerRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                var slash = trimmed.IndexOf('/');
                if (slash > 0)
                {
                    enforcers.Add((trimmed[..slash], trimmed[(slash + 1)..]));
                }
            }
        }
        if (enforcers.Count == 0)
        {
            enforcers.Add(("built-in", "user-enforcer-built-in"));
        }

        return new AppConfig
        {
            EkpConnectionString = Require("EKP_SQLSERVER_CONN"),
            CasdoorEndpoint = Require("CASDOOR_ENDPOINT"),
            CasdoorClientId = Require("CASDOOR_CLIENT_ID"),
            CasdoorClientSecret = Require("CASDOOR_CLIENT_SECRET"),
            CasdoorOrganization = Optional("CASDOOR_ORGANIZATION"),
            CasdoorApplication = Optional("CASDOOR_APPLICATION"),
            DefaultOwner = defaultOwner,
            ForceOwnerRefresh = string.Equals(Optional("FORCE_OWNER_REFRESH"), "1", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Optional("FORCE_OWNER_REFRESH"), "true", StringComparison.OrdinalIgnoreCase),
            MinimalMode = string.Equals(Optional("CASDOOR_MINIMAL_MODE"), "1", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(Optional("CASDOOR_MINIMAL_MODE"), "true", StringComparison.OrdinalIgnoreCase),
            MembershipViewName = Optional("EKP_USER_GROUP_VIEW"),
            SinceUtc = since,
            StateFilePath = Optional("SYNC_STATE_FILE") ?? "sync_state.json",
            EnforcerNames = enforcers
        };
    }
}

internal sealed class SyncState
{
    public DateTime? LastGroupSyncUtc { get; set; }
    public DateTime? LastUserSyncUtc { get; set; }
    public DateTime? LastMembershipSyncUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
}

internal sealed class SyncStateStore
{
    private readonly string _path;

    public SyncStateStore(string path) => _path = path;

    public SyncState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new SyncState();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<SyncState>(json) ?? new SyncState();
        }
        catch
        {
            return new SyncState();
        }
    }

    public void Save(SyncState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}

internal static class Slug
{
    public static string Name(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var chars = input.Trim()
            .ToLowerInvariant()
            .Select(c =>
                char.IsLetterOrDigit(c) ? c :
                (char.IsWhiteSpace(c) || c == '-' || c == '_') ? '-' : '\0')
            .Where(c => c != '\0')
            .ToArray();

        var result = new string(chars);
        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }
        return result.Trim('-');
    }
}

internal record EkpUser(
    string Id,
    string Name,
    string DisplayName,
    string? Email,
    string? Phone,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? Gender,
    string? Language,
    string? DeptId,
    string? CompanyName,
    string? Department,
    string? Owner,
    // string? Groups, // This property is obsolete.
    string? Type,
    string? PasswordMd5  // MD5密码哈希
);

internal record EkpGroup(
    string Id,
    string Name,
    string DisplayName,
    string? ParentId,
    string Type,
    string Owner,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string? DeptId,
    bool IsEnabled
);

internal sealed class EkpRepository : IDisposable
{
    private readonly SqlConnection _connection;

    public EkpRepository(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        _connection.Open();
    }

    public IEnumerable<EkpGroup> GetGroups(DateTime? sinceUtc)
    {
        var columns = GetViewColumns("vw_org_structure_sync");

        string Pick(params string[] options) =>
            options.FirstOrDefault(o => columns.Contains(o, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;

        string SelectOrNull(string column, string alias, string sqlType) =>
            string.IsNullOrEmpty(column) ? $"CAST(NULL AS {sqlType}) AS {alias}" : $"[{column}] AS {alias}";

        var idCol = Pick("id", "fd_id", "org_id");
        var nameCol = Pick("name", "fd_id");
        var displayCol = Pick("display_name", "fd_name");
        var parentCol = Pick("parent_id", "parent_dept_id", "fd_parentid", "fd_parentorgid");
        var typeCol = Pick("type", "org_type");
        var ownerCol = Pick("owner", "company_name", "org_owner");
        var createdCol = Pick("created_time", "create_time", "fd_create_time");
        var updatedCol = Pick("updated_time", "update_time", "fd_alter_time");
        var deptCol = Pick("dept_id", "parent_dept_id");
        var enabledCol = Pick("is_enabled", "enabled", "fd_is_available");

        var sql = $@"
SELECT
    {SelectOrNull(idCol, "id", "NVARCHAR(255)")},
    {SelectOrNull(nameCol, "name", "NVARCHAR(255)")},
    {SelectOrNull(displayCol, "display_name", "NVARCHAR(255)")},
    {SelectOrNull(parentCol, "parent_id", "NVARCHAR(255)")},
    {SelectOrNull(typeCol, "type", "NVARCHAR(255)")},
    {SelectOrNull(ownerCol, "owner", "NVARCHAR(255)")},
    {SelectOrNull(createdCol, "created_time", "DATETIME")},
    {SelectOrNull(updatedCol, "updated_time", "DATETIME")},
    {SelectOrNull(deptCol, "dept_id", "NVARCHAR(255)")},
    {SelectOrNull(enabledCol, "is_enabled", "BIT")}
FROM vw_org_structure_sync";

        if (sinceUtc.HasValue && !string.IsNullOrEmpty(updatedCol))
        {
            sql += $" WHERE [{updatedCol}] > @since";
        }

        using var cmd = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 120
        };
        if (sinceUtc.HasValue && !string.IsNullOrEmpty(updatedCol))
        {
            cmd.Parameters.AddWithValue("@since", sinceUtc.Value);
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var createdObj = reader.GetValue(6);
            var updatedObj = reader.GetValue(7);
            var deptObj = reader.GetValue(8);
            var enabledObj = reader.GetValue(9);

            yield return new EkpGroup(
                Id: reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Name: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                DisplayName: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ParentId: reader.IsDBNull(3) ? null : reader.GetString(3),
                Type: reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Owner: reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                CreatedUtc: createdObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(createdObj).ToUniversalTime(),
                UpdatedUtc: updatedObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(updatedObj).ToUniversalTime(),
                DeptId: deptObj is DBNull ? null : Convert.ToString(deptObj),
                IsEnabled: enabledObj is DBNull ? true : Convert.ToBoolean(enabledObj)
            );
        }
    }

    public IEnumerable<EkpUser> GetUsers(DateTime? sinceUtc)
    {
        const int pageSize = 200; // 分页大小，避免一次性加载导致超时
        var offset = 0;

        while (true)
        {
            Console.WriteLine($"从视图分页读取用户：offset={offset}, pageSize={pageSize}{(sinceUtc.HasValue ? $", since={sinceUtc:O}" : string.Empty)}");
            var sql = @"
SELECT id, username, display_name, email, phone, created_at, updated_at, gender, language,
       dept_id, company_name, affiliation, owner, type, password_md5
FROM vw_casdoor_users_sync WITH (NOLOCK)";

            if (sinceUtc.HasValue)
            {
                sql += " WHERE updated_at > @since";
            }

            // OFFSET/FETCH 需要稳定的排序键，选用 updated_at, id
            sql += " ORDER BY updated_at, id OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            using var cmd = new SqlCommand(sql, _connection)
            {
                CommandTimeout = 120
            };
            if (sinceUtc.HasValue)
            {
                cmd.Parameters.AddWithValue("@since", sinceUtc.Value);
            }
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);

            var pageCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pageCount++;
                    var createdObj = reader.GetValue(5);
                    var updatedObj = reader.GetValue(6);

                    yield return new EkpUser(
                        Id: reader.GetString(0),
                        Name: reader.GetString(1),
                        DisplayName: reader.GetString(2),
                        Email: reader.IsDBNull(3) ? null : reader.GetString(3),
                        Phone: reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAtUtc: createdObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(createdObj).ToUniversalTime(),
                        UpdatedAtUtc: updatedObj is DBNull ? DateTime.UtcNow : Convert.ToDateTime(updatedObj).ToUniversalTime(),
                        Gender: reader.IsDBNull(7) ? null : reader.GetString(7),
                        Language: reader.IsDBNull(8) ? null : reader.GetString(8),
                        DeptId: reader.IsDBNull(9) ? null : reader.GetString(9),
                        CompanyName: reader.IsDBNull(10) ? null : reader.GetString(10),
                        Department: reader.IsDBNull(11) ? null : reader.GetString(11),
                        Owner: reader.IsDBNull(12) ? null : reader.GetString(12),
                        Type: reader.IsDBNull(13) ? null : reader.GetString(13),
                        PasswordMd5: reader.IsDBNull(14) ? null : reader.GetString(14)
                    );
                }
            }

            Console.WriteLine($"  -> 本页读取 {pageCount} 条");

            if (pageCount == 0)
            {
                // 没有更多数据，结束
                yield break;
            }

            offset += pageCount;
        }
    }

    public Dictionary<string, List<string>> GetUserGroupMemberships(string? viewName)
    {
        var memberships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(viewName))
        {
            Console.WriteLine("警告: 未配置 EKP_USER_GROUP_VIEW, 用户将不会被自动添加到任何组织。");
            return memberships;
        }

        Console.WriteLine($"从视图 {viewName} 读取用户组织关系...");
        // 视图中的列名是 username 和 dept_id
        var sql = $"SELECT username, dept_id FROM {viewName}";

        using var cmd = new SqlCommand(sql, _connection);
        
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                
                var userId = reader.GetString(0);  // username
                var groupId = reader.GetString(1); // dept_id

                if (!memberships.TryGetValue(userId, out var groups))
                {
                    groups = new List<string>();
                    memberships[userId] = groups;
                }
                groups.Add(groupId);
            }
        }
        Console.WriteLine($"成功从视图 {viewName} 加载 {memberships.Count} 个用户的 {memberships.Values.Sum(g => g.Count)} 条组织关系。");
        return memberships;
    }

    private HashSet<string> GetViewColumns(string viewName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new SqlCommand("SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@name)", _connection);
        cmd.Parameters.AddWithValue("@name", viewName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public void Dispose() => _connection.Dispose();
}

internal interface ICasdoorRepository : IDisposable
{
    void UpsertGroup(EkpGroup group);
    void UpsertUser(EkpUser user, string owner, bool forceOwner, List<string>? groupIds);
    void PurgeExceptOwner(string owner);
    (string Owner, string Name)? ResolveUserKey(string owner, string userId);
    void RefreshEnforcer(string owner, string name);
    void ExportGroupHierarchy(string filePath);
    void LoadCasdoorGroupMapping();
}

internal sealed class SyncService
{
    private readonly EkpRepository _ekp;
    private readonly ICasdoorRepository _casdoor;
    private readonly AppConfig _config;
    private readonly SyncState _state;
    private readonly Dictionary<string, string> _userNameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _userOwnerCache = new(StringComparer.OrdinalIgnoreCase);

    public SyncService(EkpRepository ekp, ICasdoorRepository casdoor, AppConfig config, SyncState state)
    {
        _ekp = ekp;
        _casdoor = casdoor;
        _config = config;
        _state = state;
    }

    public void PurgeExceptOwner(string owner)
    {
        _casdoor.PurgeExceptOwner(owner);
        _state.LastGroupSyncUtc = null;
        _state.LastUserSyncUtc = null;
        _state.LastMembershipSyncUtc = null;
    }

    public void SyncGroups()
    {
        var since = _config.SinceUtc ?? _state.LastGroupSyncUtc;
        Console.WriteLine($"开始同步组织结构，时间范围：{since?.ToString("o") ?? "全量"}");
        var groups = _ekp.GetGroups(since).ToList();
        Console.WriteLine($"从视图读取到 {groups.Count} 条组织记录（包含可能的重复ID）...");
        var byId = new Dictionary<string, EkpGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            if (!string.IsNullOrWhiteSpace(g.Id))
            {
                byId[g.Id] = g;
            }
        }
        groups = byId.Values.ToList();
        Console.WriteLine($"去重后剩余 {groups.Count} 条组织记录。");

        var depthCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int ComputeDepth(EkpGroup group, HashSet<string>? stack = null)
        {
            if (string.IsNullOrWhiteSpace(group.Id)) return 0;
            if (depthCache.TryGetValue(group.Id, out var depth)) return depth;

            stack ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!stack.Add(group.Id))
            {
                depthCache[group.Id] = 0;
                return 0;
            }

            // 优先使用 ParentId（直接父组织），DeptId 仅作为备用
            var parentKey = !string.IsNullOrWhiteSpace(group.ParentId) ? group.ParentId : group.DeptId;
            if (string.IsNullOrWhiteSpace(parentKey) || !byId.TryGetValue(parentKey, out var parent))
            {
                depth = 0;
            }
            else
            {
                depth = 1 + ComputeDepth(parent, stack);
            }

            stack.Remove(group.Id);
            depthCache[group.Id] = depth;
            return depth;
        }

        var ordered = groups
            .OrderBy(g => ComputeDepth(g))
            .ThenBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var count = 0L;
        foreach (var group in ordered)
        {
            // 优先使用 ParentId（直接父组织），DeptId 仅作为备用
            var parentKey = !string.IsNullOrWhiteSpace(group.ParentId) ? group.ParentId : group.DeptId;
            var depth = ComputeDepth(group);
            string parentInfo;
            var hasParent = true;
            if (string.IsNullOrWhiteSpace(parentKey))
            {
                parentInfo = "<无>";
                hasParent = false;
            }
            else
            {
                hasParent = byId.ContainsKey(parentKey);
                parentInfo = hasParent ? $"存在({parentKey})" : $"缺失({parentKey})";
            }
            Console.WriteLine($"  -> 同步群组：{group.Id}（名称：{group.DisplayName}），层级：{depth}，父级：{parentInfo}");
            // 修复：即使当前批次中未找到父级，也不要清空 ParentId/DeptId，保留原始层级关系
            // 这样在后续同步到父级后，Casdoor 中将自动形成正确的多层级结构。
            _casdoor.UpsertGroup(group);
            count++;
        }
        Console.WriteLine($"组织同步完成，共处理 {count} 条记录。");
        
        // 导出组织层级关系到CSV文件
        var hierarchyFile = Path.Combine("logs", $"organization_hierarchy_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        Directory.CreateDirectory("logs");
        _casdoor.ExportGroupHierarchy(hierarchyFile);
        
        _state.LastGroupSyncUtc = DateTime.UtcNow;
    }

    public void SyncUsers()
    {
        // *** 关键步骤: 在同步用户之前,先从Casdoor加载所有组织映射 ***
        _casdoor.LoadCasdoorGroupMapping();
        
        var since = _config.SinceUtc ?? _state.LastUserSyncUtc;
        Console.WriteLine($"开始同步用户信息，时间范围：{since?.ToString("o") ?? "全量"}");

        // 预加载所有用户-组织关系
        var userGroupMemberships = _ekp.GetUserGroupMemberships(_config.MembershipViewName);

        var count = 0L;
        foreach (var user in _ekp.GetUsers(since))
        {
            var owner = string.IsNullOrWhiteSpace(user.Owner)
                ? (string.IsNullOrWhiteSpace(user.CompanyName) ? _config.DefaultOwner : user.CompanyName!.Trim())
                : user.Owner.Trim();
            
            userGroupMemberships.TryGetValue(user.Id, out var groupIds);

            // Fallback logic: if no groups from the dedicated view, use the user's dept_id
            if ((groupIds is null || groupIds.Count == 0) && !string.IsNullOrWhiteSpace(user.DeptId))
            {
                Console.WriteLine($"  -> 用户 {user.Id} 未在成员关系视图中找到, 回退使用其部门ID: {user.DeptId}");
                groupIds = new List<string> { user.DeptId };
            }
            else if ((groupIds is null || groupIds.Count == 0) && string.IsNullOrWhiteSpace(user.DeptId))
            {
                Console.WriteLine($"  -> 警告: 用户 {user.Id} ({user.DisplayName}) 未找到任何组织信息（成员视图无记录，且用户dept_id为空）。将创建用户但不添加到任何组织。");
            }
            
            _casdoor.UpsertUser(user, owner, _config.ForceOwnerRefresh, groupIds);
            
            // Cache user info after upsert
            var resolvedKey = ResolveUserKey(user.Id, owner);
            if (resolvedKey is not null)
            {
                 _userNameCache[user.Id] = resolvedKey.Value.Name;
                 _userOwnerCache[user.Id] = resolvedKey.Value.Owner;
            }
            count++;
        }
        Console.WriteLine($"用户同步完成，共处理 {count} 条记录。");
        _state.LastUserSyncUtc = DateTime.UtcNow;
    }

    public void SyncMemberships()
    {
        // This method is now obsolete. The logic is merged into SyncUsers.
        Console.WriteLine("成员关系同步步骤已合并到用户同步中，此步骤将被跳过。");
        _state.LastMembershipSyncUtc = DateTime.UtcNow;
    }

    private (string Owner, string Name)? ResolveUserKey(string userId, string preferredOwner)
    {
        if (_userNameCache.TryGetValue(userId, out var name) &&
            _userOwnerCache.TryGetValue(userId, out var owner))
        {
            return (owner, name);
        }

        var info = _casdoor.ResolveUserKey(preferredOwner, userId);
        if (info is null)
        {
            return null;
        }

        _userNameCache[userId] = info.Value.Name;
        _userOwnerCache[userId] = info.Value.Owner;
        return info;
    }
}
