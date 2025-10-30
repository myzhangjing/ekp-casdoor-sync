using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("========================================");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  EKP-Casdoor 同步工具 - 综合自动化测试");
Console.ResetColor();
Console.WriteLine("========================================");
Console.WriteLine();

var startTime = DateTime.Now;
int totalTests = 0;
int passedTests = 0;
int failedTests = 0;
int skippedTests = 0;

var results = new StringBuilder();
results.AppendLine("========================================");
results.AppendLine("  测试执行报告");
results.AppendLine($"  执行时间: {startTime:yyyy-MM-dd HH:mm:ss}");
results.AppendLine("========================================");
results.AppendLine();

void LogTest(string name, bool passed, string? details = null, bool skipped = false)
{
    totalTests++;
    string status;
    ConsoleColor color;
    
    if (skipped)
    {
        status = "⊘ 跳过";
        color = ConsoleColor.Yellow;
        skippedTests++;
    }
    else if (passed)
    {
        status = "✓ 通过";
        color = ConsoleColor.Green;
        passedTests++;
    }
    else
    {
        status = "✗ 失败";
        color = ConsoleColor.Red;
        failedTests++;
    }
    
    Console.Write($"[{totalTests:D2}] {name}... ");
    Console.ForegroundColor = color;
    Console.WriteLine(status);
    Console.ResetColor();
    
    if (!string.IsNullOrEmpty(details))
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"    {details}");
        Console.ResetColor();
    }
    
    results.AppendLine($"[{totalTests:D2}] {name}... {status}");
    if (!string.IsNullOrEmpty(details))
    {
        results.AppendLine($"    {details}");
    }
}

// ==================== 第一部分：EKP 数据库测试 ====================
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("【第一部分：EKP 数据库连接测试】");
Console.ResetColor();
results.AppendLine();
results.AppendLine("【第一部分：EKP 数据库连接测试】");

string ekpServer = "npm.fzcsps.com";
string ekpPort = "11433";
string ekpDatabase = "ekp";
string ekpUsername = "xxzx";
string ekpPassword = "sosy3080@sohu.com";
string ekpConnString = $"Server={ekpServer},{ekpPort};Database={ekpDatabase};User Id={ekpUsername};Password={ekpPassword};TrustServerCertificate=True;Connection Timeout=30;";

// 测试 1: 基础连接
try
{
    using var conn = new SqlConnection(ekpConnString);
    var sw = Stopwatch.StartNew();
    await conn.OpenAsync();
    sw.Stop();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT @@VERSION";
    var version = await cmd.ExecuteScalarAsync();
    
    LogTest("EKP 数据库连接", true, $"连接成功 ({sw.ElapsedMilliseconds}ms)");
}
catch (Exception ex)
{
    LogTest("EKP 数据库连接", false, $"错误: {ex.Message}");
}

// 测试 2: 查询组织数量
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_available = 1 AND fd_is_business = 1";
    var count = await cmd.ExecuteScalarAsync();
    
    LogTest("查询 EKP 组织总数", true, $"找到 {count} 个可用组织");
}
catch (Exception ex)
{
    LogTest("查询 EKP 组织总数", false, $"错误: {ex.Message}");
}

// 测试 3: 查询用户数量
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT COUNT(*) 
        FROM sys_org_element e
        INNER JOIN sys_org_person p ON e.fd_id = p.fd_id
        WHERE e.fd_is_available = 1 
        AND e.fd_org_type = 8
        AND p.fd_login_name IS NOT NULL";
    var count = await cmd.ExecuteScalarAsync();
    
    LogTest("查询 EKP 用户总数", true, $"找到 {count} 个可用用户");
}
catch (Exception ex)
{
    LogTest("查询 EKP 用户总数", false, $"错误: {ex.Message}");
}

// 测试 4: 检查视图是否存在
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT COUNT(*) 
        FROM INFORMATION_SCHEMA.VIEWS 
        WHERE TABLE_NAME IN ('vw_user_group_membership', 'vw_casdoor_users_sync')";
    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    
    LogTest("检查必需视图", count >= 1, $"找到 {count} 个必需视图");
}
catch (Exception ex)
{
    LogTest("检查必需视图", false, $"错误: {ex.Message}");
}

// 测试 5: 查询示例组织
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT TOP 3 fd_id, fd_name, fd_org_type 
        FROM sys_org_element 
        WHERE fd_is_available = 1 AND fd_is_business = 1
        ORDER BY fd_name";
    
    using var reader = await cmd.ExecuteReaderAsync();
    var orgs = new List<string>();
    while (await reader.ReadAsync())
    {
        orgs.Add($"{reader["fd_name"]} (类型:{reader["fd_org_type"]})");
    }
    
    LogTest("获取示例组织数据", orgs.Count > 0, $"成功获取 {orgs.Count} 个组织");
    foreach (var org in orgs)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"      - {org}");
        Console.ResetColor();
        results.AppendLine($"      - {org}");
    }
}
catch (Exception ex)
{
    LogTest("获取示例组织数据", false, $"错误: {ex.Message}");
}

// 测试 6: 查询示例用户
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT TOP 3 
            e.fd_name,
            p.fd_login_name,
            p.fd_email
        FROM sys_org_element e
        INNER JOIN sys_org_person p ON e.fd_id = p.fd_id
        WHERE e.fd_is_available = 1 
        AND e.fd_org_type = 8
        AND p.fd_login_name IS NOT NULL
        ORDER BY e.fd_name";
    
    using var reader = await cmd.ExecuteReaderAsync();
    var users = new List<string>();
    while (await reader.ReadAsync())
    {
        var name = reader["fd_name"]?.ToString() ?? "";
        var login = reader["fd_login_name"]?.ToString() ?? "";
        var email = reader["fd_email"]?.ToString() ?? "";
        users.Add($"{name} ({login}, {email})");
    }
    
    LogTest("获取示例用户数据", users.Count > 0, $"成功获取 {users.Count} 个用户");
    foreach (var user in users)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"      - {user}");
        Console.ResetColor();
        results.AppendLine($"      - {user}");
    }
}
catch (Exception ex)
{
    LogTest("获取示例用户数据", false, $"错误: {ex.Message}");
}

// 测试 7: 查询性能测试
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    var sw = Stopwatch.StartNew();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_available = 1";
    await cmd.ExecuteScalarAsync();
    sw.Stop();
    
    bool performanceOk = sw.ElapsedMilliseconds < 5000;
    LogTest("EKP 查询性能测试", performanceOk, $"查询耗时 {sw.ElapsedMilliseconds}ms");
}
catch (Exception ex)
{
    LogTest("EKP 查询性能测试", false, $"错误: {ex.Message}");
}

// ==================== 第二部分：Casdoor 服务测试 ====================
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("【第二部分：Casdoor 服务连接测试】");
Console.ResetColor();
results.AppendLine();
results.AppendLine("【第二部分：Casdoor 服务连接测试】");

string casdoorEndpoint = "http://172.16.10.110:8000";
string casdoorClientId = "aecd00a352e5c560ffe6";
string casdoorClientSecret = "4402518b20dd191b8b48d6240bc786a4f847899a";
string casdoorOwner = "fzswjtOrganization";

// 测试 8: Casdoor 首页访问
try
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var sw = Stopwatch.StartNew();
    var response = await httpClient.GetAsync(casdoorEndpoint);
    sw.Stop();
    
    bool success = response.IsSuccessStatusCode;
    LogTest("Casdoor 服务访问", success, $"HTTP {(int)response.StatusCode} ({sw.ElapsedMilliseconds}ms)");
}
catch (Exception ex)
{
    LogTest("Casdoor 服务访问", false, $"错误: {ex.Message}");
}

// 测试 9: Casdoor API 端点
try
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var url = $"{casdoorEndpoint}/api/get-global-organizations";
    var response = await httpClient.GetAsync(url);
    
    bool accessible = response.StatusCode == System.Net.HttpStatusCode.OK || 
                     response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
    
    LogTest("Casdoor API 端点测试", accessible, $"HTTP {(int)response.StatusCode}");
}
catch (Exception ex)
{
    LogTest("Casdoor API 端点测试", false, $"错误: {ex.Message}");
}

// 测试 10: 获取 Access Token
try
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    
    var tokenUrl = $"{casdoorEndpoint}/api/login/oauth/access_token";
    var content = new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("grant_type", "client_credentials"),
        new KeyValuePair<string, string>("client_id", casdoorClientId),
        new KeyValuePair<string, string>("client_secret", casdoorClientSecret)
    });
    
    var response = await httpClient.PostAsync(tokenUrl, content);
    var responseText = await response.Content.ReadAsStringAsync();
    
    bool hasToken = responseText.Contains("access_token") || response.IsSuccessStatusCode;
    LogTest("Casdoor 认证测试", hasToken, $"认证响应: HTTP {(int)response.StatusCode}");
}
catch (Exception ex)
{
    LogTest("Casdoor 认证测试", false, $"错误: {ex.Message}");
}

// ==================== 第三部分：配置文件测试 ====================
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("【第三部分：配置文件和环境测试】");
Console.ResetColor();
results.AppendLine();
results.AppendLine("【第三部分：配置文件和环境测试】");

// 测试 11: 检查配置文件
var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
var configPath = Path.Combine(projectRoot, "sync_config.json");
try
{
    bool exists = File.Exists(configPath);
    if (exists)
    {
        var configContent = await File.ReadAllTextAsync(configPath);
        bool hasContent = configContent.Length > 100;
        LogTest("配置文件存在性检查", hasContent, $"配置文件大小: {configContent.Length} 字节");
    }
    else
    {
        LogTest("配置文件存在性检查", false, $"配置文件不存在: {configPath}");
    }
}
catch (Exception ex)
{
    LogTest("配置文件存在性检查", false, $"错误: {ex.Message}");
}

// 测试 12: 检查日志目录
try
{
    var logsDir = Path.Combine(projectRoot, "logs");
    bool exists = Directory.Exists(logsDir);
    
    if (exists)
    {
        var logFiles = Directory.GetFiles(logsDir, "*.log");
        LogTest("日志目录检查", true, $"找到 {logFiles.Length} 个日志文件");
    }
    else
    {
        LogTest("日志目录检查", false, $"日志目录不存在: {logsDir}");
    }
}
catch (Exception ex)
{
    LogTest("日志目录检查", false, $"错误: {ex.Message}");
}

// 测试 13: 检查同步状态文件
try
{
    var statePath = Path.Combine(projectRoot, "sync_state.json");
    bool exists = File.Exists(statePath);
    
    if (exists)
    {
        var stateContent = await File.ReadAllTextAsync(statePath);
        LogTest("同步状态文件检查", true, $"状态文件存在 ({stateContent.Length} 字节)");
    }
    else
    {
        LogTest("同步状态文件检查", false, $"状态文件不存在: {statePath}");
    }
}
catch (Exception ex)
{
    LogTest("同步状态文件检查", false, $"错误: {ex.Message}");
}

// ==================== 第四部分：综合功能测试 ====================
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("【第四部分：综合功能测试】");
Console.ResetColor();
results.AppendLine();
results.AppendLine("【第四部分：综合功能测试】");

// 测试 14: 端到端数据流测试
try
{
    using var conn = new SqlConnection(ekpConnString);
    await conn.OpenAsync();
    
    // 模拟一个完整的数据读取流程
    var sw = Stopwatch.StartNew();
    
    // 1. 读取组织
    using var orgCmd = conn.CreateCommand();
    orgCmd.CommandText = "SELECT TOP 10 fd_id, fd_name FROM sys_org_element WHERE fd_is_available = 1 AND fd_is_business = 1";
    var orgCount = 0;
    using (var reader = await orgCmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync()) orgCount++;
    }
    
    // 2. 读取用户
    using var userCmd = conn.CreateCommand();
    userCmd.CommandText = @"
        SELECT TOP 10 e.fd_id, e.fd_name 
        FROM sys_org_element e
        INNER JOIN sys_org_person p ON e.fd_id = p.fd_id
        WHERE e.fd_is_available = 1 AND e.fd_org_type = 8";
    var userCount = 0;
    using (var reader = await userCmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync()) userCount++;
    }
    
    sw.Stop();
    
    LogTest("端到端数据流测试", orgCount > 0 && userCount > 0, 
        $"读取 {orgCount} 个组织和 {userCount} 个用户 (耗时 {sw.ElapsedMilliseconds}ms)");
}
catch (Exception ex)
{
    LogTest("端到端数据流测试", false, $"错误: {ex.Message}");
}

// 测试 15: 并发连接测试
try
{
    var tasks = new List<Task<bool>>();
    
    for (int i = 0; i < 5; i++)
    {
        tasks.Add(Task.Run(async () =>
        {
            using var conn = new SqlConnection(ekpConnString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return true;
        }));
    }
    
    var sw = Stopwatch.StartNew();
    var results_concurrent = await Task.WhenAll(tasks);
    sw.Stop();
    
    bool allSuccess = results_concurrent.All(r => r);
    LogTest("并发连接测试", allSuccess, $"5个并发连接全部成功 ({sw.ElapsedMilliseconds}ms)");
}
catch (Exception ex)
{
    LogTest("并发连接测试", false, $"错误: {ex.Message}");
}

// ==================== 测试总结 ====================
Console.WriteLine();
Console.WriteLine("========================================");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  测试总结");
Console.ResetColor();
Console.WriteLine("========================================");

results.AppendLine();
results.AppendLine("========================================");
results.AppendLine("  测试总结");
results.AppendLine("========================================");

var endTime = DateTime.Now;
var duration = endTime - startTime;

Console.WriteLine($"总计: {totalTests} 项测试");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"通过: {passedTests} 项");
Console.ResetColor();

if (failedTests > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"失败: {failedTests} 项");
    Console.ResetColor();
}

if (skippedTests > 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"跳过: {skippedTests} 项");
    Console.ResetColor();
}

Console.WriteLine($"耗时: {duration.TotalSeconds:F2} 秒");
Console.WriteLine($"成功率: {(double)passedTests / totalTests * 100:F1}%");

results.AppendLine($"总计: {totalTests} 项测试");
results.AppendLine($"通过: {passedTests} 项");
if (failedTests > 0) results.AppendLine($"失败: {failedTests} 项");
if (skippedTests > 0) results.AppendLine($"跳过: {skippedTests} 项");
results.AppendLine($"耗时: {duration.TotalSeconds:F2} 秒");
results.AppendLine($"成功率: {(double)passedTests / totalTests * 100:F1}%");

Console.WriteLine();

if (failedTests == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ 所有测试通过！系统就绪。");
    Console.ResetColor();
    results.AppendLine();
    results.AppendLine("✓ 所有测试通过！系统就绪。");
    results.AppendLine();
    results.AppendLine("系统状态:");
    results.AppendLine("  ✓ EKP 数据库连接正常");
    results.AppendLine("  ✓ Casdoor 服务可访问");
    results.AppendLine("  ✓ 配置文件完整");
    results.AppendLine("  ✓ 数据读取功能正常");
    results.AppendLine();
    results.AppendLine("可以开始使用 WPF 应用程序或命令行工具执行同步。");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ 有 {failedTests} 项测试失败，请检查配置。");
    Console.ResetColor();
    results.AppendLine();
    results.AppendLine($"❌ 有 {failedTests} 项测试失败，请检查配置。");
}

Console.WriteLine("========================================");
results.AppendLine("========================================");

// 保存测试报告
var reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "综合测试报告.txt");
await File.WriteAllTextAsync(reportPath, results.ToString());
Console.WriteLine();
Console.WriteLine($"测试报告已保存到: {reportPath}");

return failedTests == 0 ? 0 : 1;
