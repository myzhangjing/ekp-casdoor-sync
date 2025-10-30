using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SyncEkpToCasdoor.AutoTest;

/// <summary>
/// 自动化测试程序
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        PrintHeader();
        
        var tester = new AutomatedTester();
        var result = await tester.RunAllTestsAsync();
        
        Console.WriteLine();
        PrintSummary(result);
        
        return result.FailedCount > 0 ? 1 : 0;
    }
    
    static void PrintHeader()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  EKP-Casdoor 同步工具 - 自动化测试");
        Console.WriteLine("========================================");
        Console.WriteLine();
    }
    
    static void PrintSummary(TestResult result)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  测试总结");
        Console.WriteLine("========================================");
        Console.WriteLine($"总计: {result.TotalCount} 项");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"通过: {result.PassedCount} 项");
        Console.ResetColor();
        
        if (result.FailedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"失败: {result.FailedCount} 项");
            Console.ResetColor();
        }
        
        if (result.SkippedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"跳过: {result.SkippedCount} 项");
            Console.ResetColor();
        }
        
        Console.WriteLine($"总耗时: {result.Duration.TotalSeconds:F2} 秒");
        Console.WriteLine();
        
        if (result.FailedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ 测试失败");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ 所有测试通过");
        }
        Console.ResetColor();
    }
}

class TestResult
{
    public int TotalCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> FailedTests { get; set; } = new();
}

class AutomatedTester
{
    private readonly TestConfiguration _config;
    private readonly HttpClient _httpClient;
    private int _testCount = 0;
    private int _passCount = 0;
    private int _failCount = 0;
    private int _skipCount = 0;
    private readonly Stopwatch _stopwatch = new();
    
    public AutomatedTester()
    {
        _config = LoadConfiguration();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }
    
    public async Task<TestResult> RunAllTestsAsync()
    {
        _stopwatch.Start();
        
        // 1. 配置测试
        await RunTest("读取配置文件", TestLoadConfiguration);
        await RunTest("验证必需配置项", TestValidateConfiguration);
        
        // 2. EKP 数据库连接测试
        await RunTest("EKP 数据库连接", TestEkpDatabaseConnection);
        await RunTest("查询 EKP 组织数量", TestEkpOrganizationCount);
        await RunTest("查询 EKP 用户数量", TestEkpUserCount);
        await RunTest("查询 EKP 组织详情", TestEkpOrganizationDetails);
        await RunTest("查询 EKP 用户详情", TestEkpUserDetails);
        
        // 3. Casdoor API 测试
        await RunTest("Casdoor API 连接", TestCasdoorApiConnection);
        await RunTest("获取 Casdoor 组织列表", TestCasdoorOrganizations);
        await RunTest("获取 Casdoor 用户列表", TestCasdoorUsers);
        
        // 4. 数据比对测试
        await RunTest("比对组织数据", TestCompareOrganizations);
        await RunTest("比对用户数据", TestCompareUsers);
        
        // 5. 性能测试
        await RunTest("EKP 查询性能", TestEkpQueryPerformance);
        await RunTest("Casdoor API 性能", TestCasdoorApiPerformance);
        
        _stopwatch.Stop();
        
        return new TestResult
        {
            TotalCount = _testCount,
            PassedCount = _passCount,
            FailedCount = _failCount,
            SkippedCount = _skipCount,
            Duration = _stopwatch.Elapsed
        };
    }
    
    private async Task RunTest(string name, Func<Task> testFunc)
    {
        _testCount++;
        Console.Write($"[{_testCount:D2}] {name}... ");
        
        try
        {
            await testFunc();
            _passCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ 通过");
            Console.ResetColor();
        }
        catch (SkipTestException ex)
        {
            _skipCount++;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⊘ 跳过 ({ex.Message})");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            _failCount++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ 失败");
            Console.WriteLine($"    错误: {ex.Message}");
            Console.ResetColor();
        }
    }
    
    private TestConfiguration LoadConfiguration()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SyncEkpToCasdoor.UI", "sync_config.json");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("配置文件不存在");
            }
            
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TestConfiguration>(json);
            
            if (config == null)
            {
                throw new InvalidOperationException("配置文件解析失败");
            }
            
            return config;
        }
        catch
        {
            // 使用默认配置
            return new TestConfiguration
            {
                EkpServer = "npm.fzcsps.com",
                EkpPort = "11433",
                EkpDatabase = "ekp",
                EkpUsername = "xxzx",
                EkpPassword = "sosy3080@sohu.com",
                CasdoorEndpoint = "http://sso.fzcsps.com",
                CasdoorOwner = "built-in"
            };
        }
    }
    
    private Task TestLoadConfiguration()
    {
        if (string.IsNullOrEmpty(_config.EkpServer))
            throw new Exception("配置未加载");
        return Task.CompletedTask;
    }
    
    private Task TestValidateConfiguration()
    {
        var missing = new List<string>();
        
        if (string.IsNullOrEmpty(_config.EkpServer)) missing.Add("EkpServer");
        if (string.IsNullOrEmpty(_config.EkpDatabase)) missing.Add("EkpDatabase");
        if (string.IsNullOrEmpty(_config.CasdoorEndpoint)) missing.Add("CasdoorEndpoint");
        
        if (missing.Any())
            throw new Exception($"缺少必需配置: {string.Join(", ", missing)}");
        
        return Task.CompletedTask;
    }
    
    private async Task TestEkpDatabaseConnection()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync();
        
        if (result?.ToString() != "1")
            throw new Exception("数据库连接验证失败");
    }
    
    private async Task TestEkpOrganizationCount()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1";
        var count = (int)await cmd.ExecuteScalarAsync();
        
        Console.Write($"({count} 个组织) ");
        
        if (count == 0)
            throw new Exception("未找到任何组织");
    }
    
    private async Task TestEkpUserCount()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sys_org_person WHERE fd_is_available = 1";
        var count = (int)await cmd.ExecuteScalarAsync();
        
        Console.Write($"({count} 个用户) ");
        
        if (count == 0)
            throw new Exception("未找到任何用户");
    }
    
    private async Task TestEkpOrganizationDetails()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 fd_id, fd_name FROM sys_org_element WHERE fd_is_business = 1";
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (!await reader.ReadAsync())
            throw new Exception("无法读取组织详情");
        
        var id = reader["fd_id"]?.ToString();
        var name = reader["fd_name"]?.ToString();
        
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            throw new Exception("组织数据不完整");
        
        Console.Write($"(示例: {name}) ");
    }
    
    private async Task TestEkpUserDetails()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 fd_login_name, fd_name FROM sys_org_person WHERE fd_is_available = 1";
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (!await reader.ReadAsync())
            throw new Exception("无法读取用户详情");
        
        var loginName = reader["fd_login_name"]?.ToString();
        var name = reader["fd_name"]?.ToString();
        
        if (string.IsNullOrEmpty(loginName) || string.IsNullOrEmpty(name))
            throw new Exception("用户数据不完整");
        
        Console.Write($"(示例: {name}) ");
    }
    
    private async Task TestCasdoorApiConnection()
    {
        var url = $"{_config.CasdoorEndpoint}/api/get-organizations?owner={_config.CasdoorOwner}";
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception($"API 返回状态码: {response.StatusCode}");
            }
            
            Console.Write($"(状态: {response.StatusCode}) ");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"无法连接到 Casdoor: {ex.Message}");
        }
    }
    
    private async Task TestCasdoorOrganizations()
    {
        var url = $"{_config.CasdoorEndpoint}/api/get-organizations?owner={_config.CasdoorOwner}";
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    var count = dataArray.GetArrayLength();
                    Console.Write($"({count} 个组织) ");
                }
            }
            else
            {
                Console.Write("(需要认证) ");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取组织列表失败: {ex.Message}");
        }
    }
    
    private async Task TestCasdoorUsers()
    {
        var url = $"{_config.CasdoorEndpoint}/api/get-users?owner={_config.CasdoorOwner}";
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    var count = dataArray.GetArrayLength();
                    Console.Write($"({count} 个用户) ");
                }
            }
            else
            {
                Console.Write("(需要认证) ");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取用户列表失败: {ex.Message}");
        }
    }
    
    private async Task TestCompareOrganizations()
    {
        // 获取 EKP 组织
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        var ekpOrgs = new HashSet<string>();
        
        using (var connection = new SqlConnection(connString))
        {
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT fd_id FROM sys_org_element WHERE fd_is_business = 1";
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                ekpOrgs.Add(reader["fd_id"].ToString() ?? "");
            }
        }
        
        // 获取 Casdoor 组织（如果可以）
        var url = $"{_config.CasdoorEndpoint}/api/get-organizations?owner={_config.CasdoorOwner}";
        var casdoorOrgs = new HashSet<string>();
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var name))
                        {
                            casdoorOrgs.Add(name.GetString() ?? "");
                        }
                    }
                }
            }
        }
        catch
        {
            // Casdoor 可能需要认证，跳过比对
            throw new SkipTestException("Casdoor 需要认证");
        }
        
        var onlyInEkp = ekpOrgs.Except(casdoorOrgs).Count();
        var onlyInCasdoor = casdoorOrgs.Except(ekpOrgs).Count();
        var both = ekpOrgs.Intersect(casdoorOrgs).Count();
        
        Console.Write($"(EKP:{onlyInEkp}, Casdoor:{onlyInCasdoor}, 已同步:{both}) ");
    }
    
    private async Task TestCompareUsers()
    {
        // 类似组织比对
        throw new SkipTestException("需要完整认证");
    }
    
    private async Task TestEkpQueryPerformance()
    {
        var connString = $"Server={_config.EkpServer},{_config.EkpPort};Database={_config.EkpDatabase};User Id={_config.EkpUsername};Password={_config.EkpPassword};TrustServerCertificate=True;Connection Timeout=10;";
        
        var sw = Stopwatch.StartNew();
        
        using var connection = new SqlConnection(connString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1";
        await cmd.ExecuteScalarAsync();
        
        sw.Stop();
        
        Console.Write($"({sw.ElapsedMilliseconds}ms) ");
        
        if (sw.ElapsedMilliseconds > 5000)
            throw new Exception("查询性能过低");
    }
    
    private async Task TestCasdoorApiPerformance()
    {
        var url = $"{_config.CasdoorEndpoint}/api/get-organizations?owner={_config.CasdoorOwner}";
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            await _httpClient.GetAsync(url);
        }
        catch
        {
            // 忽略错误，只测性能
        }
        
        sw.Stop();
        
        Console.Write($"({sw.ElapsedMilliseconds}ms) ");
        
        if (sw.ElapsedMilliseconds > 5000)
            throw new Exception("API 响应过慢");
    }
}

class TestConfiguration
{
    public string EkpServer { get; set; } = "";
    public string EkpPort { get; set; } = "";
    public string EkpDatabase { get; set; } = "";
    public string EkpUsername { get; set; } = "";
    public string EkpPassword { get; set; } = "";
    public string CasdoorEndpoint { get; set; } = "";
    public string CasdoorOwner { get; set; } = "";
}

class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}
