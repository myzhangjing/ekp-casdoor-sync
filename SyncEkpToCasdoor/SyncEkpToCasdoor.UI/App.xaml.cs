using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SyncEkpToCasdoor.UI.Services;

namespace SyncEkpToCasdoor.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "logs"
    );
    
    private static readonly string ErrorLogPath = Path.Combine(
        LogDirectory,
        $"error_{DateTime.Now:yyyyMMdd}.log"
    );

    private static string? _pendingAuthorizationCode;
    private static Views.LoginWindow? _activeLoginWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 确保日志目录存在
        Directory.CreateDirectory(LogDirectory);
        
        // 全局异常处理
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 检查是否需要登录
        if (RequiresLogin())
        {
            ShowLoginWindow();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("已有缓存 token，直接显示主窗口");
            
            // 已经登录，直接显示主窗口
            var mainWindow = new MainWindow();
            e
            // 设置为应用程序的主窗口
            Application.Current.MainWindow = mainWindow;
            
            // 监听主窗口关闭事件，关闭时退出应用
            mainWindow.Closed += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine("主窗口已关闭，退出应用");
                Shutdown();
            };
            
            mainWindow.Show();
            
            System.Diagnostics.Debug.WriteLine("主窗口已显示（缓存 token 路径）");
        }
    }

    /// <summary>
    /// 处理自定义 URI Scheme 回调
    /// </summary>
    private void HandleUriSchemeCallback(string uri)
    {
        if (UriSchemeRegistrar.TryParseCallbackUri(uri, out var code, out var state, out var error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show($"授权失败: {error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (!string.IsNullOrEmpty(code))
            {
                // 如果有活动的登录窗口，通知它
                if (_activeLoginWindow != null && _activeLoginWindow.DataContext is ViewModels.LoginViewModel loginVm)
                {
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await loginVm.HandleAuthorizationCodeAsync(code);
                    });
                }
                else
                {
                    // 保存授权码，等待登录窗口打开
                    _pendingAuthorizationCode = code;
                }
            }
        }
        else
        {
            MessageBox.Show("无效的回调地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // 不自动关闭，等待登录窗口处理
    }

    /// <summary>
    /// 检查是否需要登录
    /// </summary>
    private bool RequiresLogin()
    {
        // 检查是否有有效的访问令牌缓存
        var cachedToken = Application.Current.Properties["AccessToken"] as string;
        if (!string.IsNullOrEmpty(cachedToken))
        {
            // TODO: 验证 token 是否过期
            return false; // 有缓存的 token，不需要重新登录
        }

        // 必须进行登录验证
        return true;
    }

    /// <summary>
    /// 显示登录窗口
    /// </summary>
    private void ShowLoginWindow()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("开始显示登录窗口");
            
            // 使用内置 Casdoor 配置
            var endpoint = Configuration.CasdoorConfig.Endpoint;
            var clientId = Configuration.CasdoorConfig.ClientId;

            System.Diagnostics.Debug.WriteLine($"配置信息: Endpoint={endpoint}, ClientId={clientId}");

            // 验证配置是否完整
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(clientId))
            {
                MessageBox.Show(
                    "Casdoor 配置错误，无法进行身份验证。\n\n" +
                    "请联系管理员配置程序。\n\n" +
                    "应用将退出。",
                    "配置错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            System.Diagnostics.Debug.WriteLine("正在创建登录窗口");
            
            // 创建登录窗口（现在使用 WebView2 内嵌登录）
            var loginWindow = new Views.LoginWindow();

            System.Diagnostics.Debug.WriteLine("登录窗口已创建");

            // 保存活动窗口引用
            _activeLoginWindow = loginWindow;

            System.Diagnostics.Debug.WriteLine("显示登录窗口对话框");
            
            // 显示登录窗口（模态，必须登录）
            var result = loginWindow.ShowDialog();
            
            System.Diagnostics.Debug.WriteLine($"登录窗口关闭，结果: {result}");
            
            _activeLoginWindow = null;
            
            if (result == true)
            {
                System.Diagnostics.Debug.WriteLine("登录成功，创建主窗口");
                
                // 登录成功，显示主窗口
                var mainWindow = new MainWindow();
                
                // 设置为应用程序的主窗口
                Application.Current.MainWindow = mainWindow;
                
                // 监听主窗口关闭事件，关闭时退出应用
                mainWindow.Closed += (s, args) =>
                {
                    System.Diagnostics.Debug.WriteLine("主窗口已关闭，退出应用");
                    Shutdown();
                };
                
                mainWindow.Show();
                
                System.Diagnostics.Debug.WriteLine("主窗口已显示");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("登录失败或取消");
                
                // 登录失败或取消，必须退出应用
                MessageBox.Show(
                    "您必须通过 Casdoor 身份验证才能使用本程序。\n\n应用将退出。",
                    "需要身份验证",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowLoginWindow 异常: {ex}");
            MessageBox.Show($"显示登录窗口时发生错误：{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// 保存访问令牌
    /// </summary>
    private void SaveAccessToken(string accessToken)
    {
        // 保存到应用程序属性
        Application.Current.Properties["AccessToken"] = accessToken;
        
        // 获取用户信息
        _ = GetUserInfoAsync(accessToken);
    }

    /// <summary>
    /// 获取用户信息
    /// </summary>
    private async System.Threading.Tasks.Task GetUserInfoAsync(string accessToken)
    {
        try
        {
            // 使用内置配置
            var endpoint = Configuration.CasdoorConfig.Endpoint;

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync($"{endpoint}/api/userinfo");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userInfo = System.Text.Json.JsonDocument.Parse(content);
                
                // 保存用户信息
                if (userInfo.RootElement.TryGetProperty("name", out var name))
                {
                    Application.Current.Properties["UserName"] = name.GetString();
                }
                if (userInfo.RootElement.TryGetProperty("displayName", out var displayName))
                {
                    Application.Current.Properties["UserDisplayName"] = displayName.GetString();
                }
                
                System.Diagnostics.Debug.WriteLine($"用户登录成功: {name.GetString()}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取用户信息失败: {ex.Message}");
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var errorMessage = $"应用程序发生错误:\n\n{e.Exception.Message}\n\n详细信息:\n{e.Exception}";
        
        // 写入日志文件
        LogError(e.Exception);
        
        // 输出到控制台便于调试
        Console.WriteLine("========== 应用程序异常 ==========");
        Console.WriteLine(errorMessage);
        Console.WriteLine("=====================================");
        
        // 显示可复制的错误对话框
        try
        {
            var errorWindow = new ErrorLogWindow(errorMessage, ErrorLogPath);
            errorWindow.ShowDialog();
        }
        catch
        {
            // 如果错误窗口创建失败，回退到简单消息框
            MessageBox.Show(errorMessage + $"\n\n日志已保存到: {ErrorLogPath}", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        // 标记为已处理，防止程序崩溃
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var errorMessage = $"应用程序发生严重错误:\n\n{exception?.Message ?? "未知错误"}\n\n详细信息:\n{exception}";
        
        // 写入日志文件
        if (exception != null)
        {
            LogError(exception);
        }
        
        MessageBox.Show(errorMessage + $"\n\n日志已保存到: {ErrorLogPath}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void LogError(Exception exception)
    {
        try
        {
            var logEntry = $"""
                ========================================
                时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                错误类型: {exception.GetType().FullName}
                错误消息: {exception.Message}
                堆栈跟踪:
                {exception.StackTrace}
                
                完整异常:
                {exception}
                ========================================
                
                """;

            File.AppendAllText(ErrorLogPath, logEntry);
        }
        catch
        {
            // 如果日志写入失败，忽略错误
        }
    }
}

