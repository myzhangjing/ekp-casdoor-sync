using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTester
{
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        private static readonly string _baseUrl = "http://localhost:5233";
        
        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=====================================");
            Console.WriteLine("自动化测试 - 同步功能");
            Console.WriteLine("=====================================");
            Console.ResetColor();
            Console.WriteLine();
            
            // 等待应用启动
            Console.WriteLine("[初始化] 等待应用启动...");
            await Task.Delay(3000);
            Console.WriteLine();
            
            // 测试1: 页面访问
            await TestPageAccess();
            
            // 测试2: 测试连接（模拟点击）
            await TestConnections();
            
            // 测试3: 预览同步
            await TestPreviewSync();
            
            // 测试4: 执行全量同步
            await TestFullSync();
            
            // 测试5: 并发保护
            await TestConcurrency();
            
            // 测试6: 状态恢复
            await TestStateRecovery();
            
            // 总结
            PrintSummary();
        }
        
        static async Task TestPageAccess()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[1/6] 测试页面访问...");
            Console.ResetColor();
            
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/sync");
                if (response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ 页面访问成功");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ 页面访问失败: {response.StatusCode}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 页面访问异常: {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine();
            await Task.Delay(2000);
        }
        
        static async Task TestConnections()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[2/6] 测试连接功能 (需要等待30秒)...");
            Console.ResetColor();
            Console.WriteLine("    注意: 由于 Blazor Server 的特性，连接测试需要在浏览器中手动触发");
            Console.WriteLine("    请在浏览器中点击 '🔌 测试连接' 按钮");
            Console.WriteLine();
            
            // 等待用户操作
            Console.WriteLine("    等待30秒供您操作...");
            for (int i = 30; i > 0; i--)
            {
                Console.Write($"\r    倒计时: {i} 秒  ");
                await Task.Delay(1000);
            }
            Console.WriteLine("\r    ✓ 继续下一步测试                    ");
            Console.WriteLine();
        }
        
        static async Task TestPreviewSync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[3/6] 测试预览同步 (需要等待1分钟)...");
            Console.ResetColor();
            Console.WriteLine("    请在浏览器中点击 '👁️ 预览同步' 按钮");
            Console.WriteLine("    预期结果: 显示将要创建/更新的组织和用户数量");
            Console.WriteLine();
            
            // 等待用户操作
            Console.WriteLine("    等待60秒供您操作...");
            for (int i = 60; i > 0; i--)
            {
                if (i % 10 == 0)
                {
                    Console.Write($"\r    倒计时: {i} 秒  ");
                }
                await Task.Delay(1000);
            }
            Console.WriteLine("\r    ✓ 继续下一步测试                    ");
            Console.WriteLine();
        }
        
        static async Task TestFullSync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[4/6] 测试全量同步 (需要5-10分钟)...");
            Console.ResetColor();
            Console.WriteLine("    请在浏览器中点击 '▶️ 全量同步' 按钮");
            Console.WriteLine();
            Console.WriteLine("    关键观察点:");
            Console.WriteLine("    1. UI 显示'运行中'状态");
            Console.WriteLine("    2. 终端输出进度日志 (切换到 dotnet terminal 查看):");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("       - 同步组织进度: 10/177 (5%)");
            Console.WriteLine("       - 同步组织进度: 20/177 (11%)");
            Console.WriteLine("       - ...");
            Console.WriteLine("       - 同步用户进度: 50/1187 (4%)");
            Console.WriteLine("       - 同步用户进度: 100/1187 (8%)");
            Console.WriteLine("       - ...");
            Console.WriteLine("       - 用户同步完成: 1187 个用户已处理");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("    3. 完成后 UI 恢复'空闲'状态");
            Console.WriteLine();
            
            // 等待同步完成
            Console.WriteLine("    等待10分钟供同步完成...");
            for (int i = 600; i > 0; i--)
            {
                if (i % 30 == 0)
                {
                    Console.Write($"\r    倒计时: {i / 60}分{i % 60}秒  ");
                }
                await Task.Delay(1000);
            }
            Console.WriteLine("\r    ✓ 继续下一步测试                    ");
            Console.WriteLine();
        }
        
        static async Task TestConcurrency()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[5/6] 测试并发保护...");
            Console.ResetColor();
            Console.WriteLine("    请执行以下操作:");
            Console.WriteLine("    1. 点击 '▶️ 全量同步' 按钮");
            Console.WriteLine("    2. 立即再次点击 '▶️ 全量同步' 按钮 (趁还在运行)");
            Console.WriteLine();
            Console.WriteLine("    预期结果: 第二次点击应显示 '同步任务正在运行中，请稍后再试 (已运行 X 秒)'");
            Console.WriteLine();
            
            // 等待用户操作
            Console.WriteLine("    等待60秒供您操作...");
            for (int i = 60; i > 0; i--)
            {
                if (i % 10 == 0)
                {
                    Console.Write($"\r    倒计时: {i} 秒  ");
                }
                await Task.Delay(1000);
            }
            Console.WriteLine("\r    ✓ 继续下一步测试                    ");
            Console.WriteLine();
        }
        
        static async Task TestStateRecovery()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[6/6] 验证状态恢复...");
            Console.ResetColor();
            Console.WriteLine("    请检查以下几点:");
            Console.WriteLine("    1. ✓ UI 状态从'运行中'变为'空闲'");
            Console.WriteLine("    2. ✓ '最后全量同步'时间已更新");
            Console.WriteLine("    3. ✓ 可以立即开始新的同步 (不卡住)");
            Console.WriteLine("    4. ✓ 刷新页面后状态仍然正确");
            Console.WriteLine();
            
            // 等待用户确认
            Console.WriteLine("    等待30秒供您验证...");
            for (int i = 30; i > 0; i--)
            {
                Console.Write($"\r    倒计时: {i} 秒  ");
                await Task.Delay(1000);
            }
            Console.WriteLine("\r    ✓ 测试完成                    ");
            Console.WriteLine();
        }
        
        static void PrintSummary()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=====================================");
            Console.WriteLine("测试完成");
            Console.WriteLine("=====================================");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.WriteLine("请确认以下检查点:");
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("✓ 必须通过的测试:");
            Console.ResetColor();
            Console.WriteLine("  1. [ ] 页面访问成功");
            Console.WriteLine("  2. [ ] 连接测试显示 EKP 和 Casdoor 连接状态");
            Console.WriteLine("  3. [ ] 预览同步显示差异统计");
            Console.WriteLine("  4. [ ] 全量同步能看到进度日志");
            Console.WriteLine("  5. [ ] 同步完成后状态恢复为'空闲'");
            Console.WriteLine("  6. [ ] 并发点击返回正确提示");
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("✓ 关键修复验证:");
            Console.ResetColor();
            Console.WriteLine("  1. [ ] 终端输出: '同步组织进度: X/177 (Y%)'");
            Console.WriteLine("  2. [ ] 终端输出: '同步用户进度: X/1187 (Y%)'");
            Console.WriteLine("  3. [ ] 终端输出: '用户同步完成: 1187 个用户已处理'");
            Console.WriteLine("  4. [ ] 同步后不再卡在'运行中'状态");
            Console.WriteLine("  5. [ ] 异常后能正确恢复 (如果有异常)");
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("测试报告:");
            Console.ResetColor();
            Console.WriteLine("  - 测试时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("  - 应用地址: http://localhost:5233/sync");
            Console.WriteLine("  - 修复内容: 同步状态管理 + 进度日志");
            Console.WriteLine();
            
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
