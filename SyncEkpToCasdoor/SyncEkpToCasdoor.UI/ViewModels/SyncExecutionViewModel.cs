using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncEkpToCasdoor.UI.Models;
using SyncEkpToCasdoor.UI.Services;

namespace SyncEkpToCasdoor.UI.ViewModels;

/// <summary>
/// 同步执行 ViewModel
/// </summary>
public partial class SyncExecutionViewModel : ObservableObject
{
    private readonly SyncEngineService _syncEngine;
    private readonly ConfigurationStorageService _configService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private SyncConfiguration _configuration;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private string _progressMessage = "就绪";

    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    [ObservableProperty]
    private ObservableCollection<string> _realtimeLogs = new();

    [ObservableProperty]
    private int _organizationCount;

    [ObservableProperty]
    private int _userCount;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private DateTime? _syncStartTime;

    [ObservableProperty]
    private TimeSpan _syncDuration;

    [ObservableProperty]
    private string _lastSyncResult = "无";

    public SyncExecutionViewModel()
    {
        _syncEngine = new SyncEngineService();
        _configService = new ConfigurationStorageService();
        _configuration = _configService.LoadConfiguration();

        // 订阅同步事件
        _syncEngine.ProgressChanged += OnSyncProgressChanged;
        _syncEngine.LogReceived += OnLogReceived;
        _syncEngine.SyncCompleted += OnSyncCompleted;

        AddLog("同步执行模块已初始化");
    }

    /// <summary>
    /// 开始增量同步
    /// </summary>
    [RelayCommand]
    private async Task StartIncrementalSyncAsync()
    {
        if (IsSyncing) return;

        if (!ValidateConfiguration())
        {
            MessageBox.Show("配置不完整或无效，请先在【配置管理】页面完成配置。", 
                "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "确定要执行增量同步吗？\n\n这将同步自上次同步以来发生变化的数据。",
            "确认同步",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await ExecuteSyncAsync(false);
    }

    /// <summary>
    /// 开始全量同步
    /// </summary>
    [RelayCommand]
    private async Task StartFullSyncAsync()
    {
        if (IsSyncing) return;

        if (!ValidateConfiguration())
        {
            MessageBox.Show("配置不完整或无效，请先在【配置管理】页面完成配置。",
                "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "确定要执行全量同步吗？\n\n⚠️ 这将重新同步所有数据，可能需要较长时间。",
            "确认全量同步",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await ExecuteSyncAsync(true);
    }

    /// <summary>
    /// 取消同步
    /// </summary>
    [RelayCommand]
    private void CancelSync()
    {
        if (!IsSyncing) return;

        var result = MessageBox.Show(
            "确定要取消正在进行的同步吗？",
            "取消同步",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _cancellationTokenSource?.Cancel();
            _syncEngine.CancelSync();
            AddLog("正在取消同步...");
        }
    }

    /// <summary>
    /// 清除日志
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        RealtimeLogs.Clear();
        AddLog("日志已清除");
    }

    private async Task ExecuteSyncAsync(bool isFullSync)
    {
        IsSyncing = true;
        _cancellationTokenSource = new CancellationTokenSource();

        // 重置统计
        OrganizationCount = 0;
        UserCount = 0;
        SuccessCount = 0;
        ErrorCount = 0;
        ProgressPercentage = 0;
        SyncStartTime = DateTime.Now;

        AddLog("=".PadRight(50, '='));
        AddLog($"开始{(isFullSync ? "全量" : "增量")}同步");
        AddLog("=".PadRight(50, '='));

        try
        {
            SyncResult result;
            if (isFullSync)
            {
                result = await _syncEngine.ExecuteFullSyncAsync(Configuration, _cancellationTokenSource.Token);
            }
            else
            {
                result = await _syncEngine.ExecuteSyncAsync(Configuration, _cancellationTokenSource.Token);
            }

            // 更新统计
            OrganizationCount = result.OrganizationCount;
            UserCount = result.UserCount;
            SuccessCount = result.SuccessCount;
            ErrorCount = result.ErrorCount;
            SyncDuration = result.Duration;

            LastSyncResult = result.Success 
                ? $"成功 - {result.EndTime:HH:mm:ss}" 
                : $"失败 - {result.ErrorMessage}";

            StatusMessage = result.Success ? "同步完成" : "同步失败";

            // 显示结果
            var message = result.Success
                ? $"同步成功完成！\n\n组织: {result.OrganizationCount}\n用户: {result.UserCount}\n成功: {result.SuccessCount}\n错误: {result.ErrorCount}\n耗时: {result.Duration.TotalSeconds:F1} 秒"
                : $"同步失败！\n\n错误: {result.ErrorMessage}\n退出代码: {result.ExitCode}";

            MessageBox.Show(message, 
                result.Success ? "同步完成" : "同步失败",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            AddLog($"同步异常: {ex.Message}", LogLevel.Error);
            StatusMessage = "同步异常";
            LastSyncResult = $"异常 - {ex.Message}";

            MessageBox.Show($"同步过程发生异常:\n\n{ex.Message}",
                "异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSyncing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            AddLog("=".PadRight(50, '='));
            AddLog("同步流程结束");
            AddLog("=".PadRight(50, '='));
        }
    }

    private bool ValidateConfiguration()
    {
        // 简单验证
        return !string.IsNullOrWhiteSpace(Configuration.EkpServer) &&
               !string.IsNullOrWhiteSpace(Configuration.EkpDatabase) &&
               !string.IsNullOrWhiteSpace(Configuration.CasdoorEndpoint) &&
               !string.IsNullOrWhiteSpace(Configuration.CasdoorClientId) &&
               !string.IsNullOrWhiteSpace(Configuration.CasdoorClientSecret);
    }

    private void OnSyncProgressChanged(object? sender, SyncProgressEventArgs e)
    {
        // 在 UI 线程更新
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressPercentage = e.Percentage;
            ProgressMessage = e.Message;
            StatusMessage = $"同步中... {e.Percentage}%";
        });
    }

    private void OnLogReceived(object? sender, LogMessageEventArgs e)
    {
        AddLog(e.Message, e.Level);

        // 更新持续时间
        if (SyncStartTime.HasValue)
        {
            SyncDuration = DateTime.Now - SyncStartTime.Value;
        }
    }

    private void OnSyncCompleted(object? sender, SyncCompletedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressPercentage = 100;
            ProgressMessage = e.Result.Success ? "同步完成" : "同步失败";
        });
    }

    private void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = level switch
            {
                LogLevel.Error => "❌",
                LogLevel.Warning => "⚠️",
                LogLevel.Debug => "🔍",
                _ => "ℹ️"
            };

            RealtimeLogs.Insert(0, $"[{timestamp}] {prefix} {message}");

            // 限制日志数量
            while (RealtimeLogs.Count > 500)
            {
                RealtimeLogs.RemoveAt(RealtimeLogs.Count - 1);
            }
        });
    }
}
