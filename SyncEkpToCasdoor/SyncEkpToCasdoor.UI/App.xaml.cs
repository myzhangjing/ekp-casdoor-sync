using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 确保日志目录存在
        Directory.CreateDirectory(LogDirectory);
        
        // 全局异常处理
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
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

