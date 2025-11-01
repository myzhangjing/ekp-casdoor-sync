using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SyncEkpToCasdoor.UI.Models;

namespace SyncEkpToCasdoor.UI.Services;

/// <summary>
/// 同步引擎服务 - 封装命令行同步工具，提供事件驱动的同步执行
/// </summary>
public class SyncEngineService
{
    private Process? _syncProcess;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 同步进度变化事件
    /// </summary>
    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event EventHandler<LogMessageEventArgs>? LogReceived;

    /// <summary>
    /// 同步完成事件
    /// </summary>
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// 执行增量同步
    /// </summary>
    public async Task<SyncResult> ExecuteSyncAsync(SyncConfiguration config, CancellationToken cancellationToken = default)
    {
        return await ExecuteSyncInternalAsync(config, false, cancellationToken);
    }

    /// <summary>
    /// 执行全量同步
    /// </summary>
    public async Task<SyncResult> ExecuteFullSyncAsync(SyncConfiguration config, CancellationToken cancellationToken = default)
    {
        return await ExecuteSyncInternalAsync(config, true, cancellationToken);
    }

    /// <summary>
    /// 取消正在执行的同步
    /// </summary>
    public void CancelSync()
    {
        _cancellationTokenSource?.Cancel();
        
        if (_syncProcess != null && !_syncProcess.HasExited)
        {
            try
            {
                _syncProcess.Kill(true); // 终止进程树
                OnLogReceived("用户取消了同步操作", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                OnLogReceived($"取消同步时发生错误: {ex.Message}", LogLevel.Error);
            }
        }
    }

    private async Task<SyncResult> ExecuteSyncInternalAsync(
        SyncConfiguration config, 
        bool isFullSync, 
        CancellationToken cancellationToken)
    {
        var result = new SyncResult
        {
            StartTime = DateTime.Now,
            IsFullSync = isFullSync
        };

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            OnLogReceived($"开始{(isFullSync ? "全量" : "增量")}同步...", LogLevel.Info);
            OnProgressChanged(0, "准备同步环境...");

            // 查找同步工具可执行文件
            var exePath = FindSyncExecutable();
            if (string.IsNullOrEmpty(exePath))
            {
                throw new FileNotFoundException("未找到同步工具可执行文件。请先编译 SyncEkpToCasdoor 项目。");
            }

            OnLogReceived($"使用同步工具: {exePath}", LogLevel.Debug);

            // 设置环境变量
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };

            // 配置环境变量
            SetEnvironmentVariables(startInfo, config, isFullSync);

            // 启动进程
            _syncProcess = new Process { StartInfo = startInfo };
            
            _syncProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine(e.Data, result);
                }
            };

            _syncProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OnLogReceived(e.Data, LogLevel.Error);
                    result.ErrorCount++;
                }
            };

            OnLogReceived("启动同步进程...", LogLevel.Info);
            _syncProcess.Start();
            _syncProcess.BeginOutputReadLine();
            _syncProcess.BeginErrorReadLine();

            // 等待进程完成或取消
            await Task.Run(() => 
            {
                while (!_syncProcess.HasExited)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _syncProcess.Kill(true);
                        throw new OperationCanceledException("同步已被用户取消");
                    }
                    Thread.Sleep(100);
                }
            }, _cancellationTokenSource.Token);

            result.ExitCode = _syncProcess.ExitCode;
            result.Success = _syncProcess.ExitCode == 0;

        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "同步已取消";
            OnLogReceived("同步已取消", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            OnLogReceived($"同步失败: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            result.EndTime = DateTime.Now;
            result.Duration = result.EndTime.Value - result.StartTime;

            OnProgressChanged(100, result.Success ? "同步完成" : "同步失败");
            OnSyncCompleted(result);

            _syncProcess?.Dispose();
            _syncProcess = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        return result;
    }

    private string? FindSyncExecutable()
    {
        // 查找可执行文件的可能位置
        var possiblePaths = new[]
        {
            // 相对于 UI 项目的位置
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bin", "Release", "net8.0", "SyncEkpToCasdoor.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0", "SyncEkpToCasdoor.exe"),
            
            // 相对于当前目录
            Path.Combine(Directory.GetCurrentDirectory(), "..", "bin", "Release", "net8.0", "SyncEkpToCasdoor.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "bin", "Debug", "net8.0", "SyncEkpToCasdoor.exe"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private void SetEnvironmentVariables(ProcessStartInfo startInfo, SyncConfiguration config, bool isFullSync)
    {
        // EKP 数据库连接
        startInfo.Environment["EKP_SQLSERVER_CONN"] = config.EkpConnectionString;

        // Casdoor 配置
        startInfo.Environment["CASDOOR_ENDPOINT"] = config.CasdoorEndpoint;
        startInfo.Environment["CASDOOR_CLIENT_ID"] = config.CasdoorClientId;
        startInfo.Environment["CASDOOR_CLIENT_SECRET"] = config.CasdoorClientSecret;
        startInfo.Environment["CASDOOR_DEFAULT_OWNER"] = config.CasdoorOwner;

        // 可选配置
        if (!string.IsNullOrEmpty(config.UserGroupView))
        {
            startInfo.Environment["EKP_USER_GROUP_VIEW"] = config.UserGroupView;
        }

        // 全量同步标记
        if (isFullSync)
        {
            startInfo.Environment["SYNC_SINCE_UTC"] = "1970-01-01T00:00:00Z";
        }

        OnLogReceived("环境变量已配置", LogLevel.Debug);
    }

    private void ProcessOutputLine(string line, SyncResult result)
    {
        OnLogReceived(line, LogLevel.Info);

        // 精确解析同步完成的输出行
        // 格式: "组织同步完成，共处理 123 条记录。"
        if (line.Contains("组织同步完成，共处理") && line.Contains("条记录"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"共处理\s+(\d+)\s+条记录");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                result.OrganizationCount = count;
                OnProgressChanged(50, $"组织同步完成: {count} 条");
            }
        }
        // 格式: "用户同步完成，共处理 456 条记录。"
        else if (line.Contains("用户同步完成，共处理") && line.Contains("条记录"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"共处理\s+(\d+)\s+条记录");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                result.UserCount = count;
                OnProgressChanged(90, $"用户同步完成: {count} 条");
            }
        }
        // 其他进度提示
        else if (line.Contains("开始同步组织结构"))
        {
            OnProgressChanged(10, "正在同步组织结构...");
        }
        else if (line.Contains("开始同步用户信息"))
        {
            OnProgressChanged(60, "正在同步用户信息...");
        }
        else if (line.Contains("失败") || line.Contains("错误") || line.Contains("异常"))
        {
            result.ErrorCount++;
        }

        // 计算总进度
        var totalItems = result.OrganizationCount + result.UserCount;
        if (totalItems > 0)
        {
            var progress = Math.Min(95, 50 + (result.UserCount > 0 ? 40 : 0));
            OnProgressChanged(progress, $"已处理: 组织 {result.OrganizationCount} / 用户 {result.UserCount}");
        }
    }

    private void OnProgressChanged(int percentage, string message)
    {
        ProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            Percentage = percentage,
            Message = message,
            Timestamp = DateTime.Now
        });
    }

    private void OnLogReceived(string message, LogLevel level)
    {
        LogReceived?.Invoke(this, new LogMessageEventArgs
        {
            Message = message,
            Level = level,
            Timestamp = DateTime.Now
        });
    }

    private void OnSyncCompleted(SyncResult result)
    {
        SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
        {
            Result = result
        });
    }
}

/// <summary>
/// 同步结果
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsFullSync { get; set; }
    public int OrganizationCount { get; set; }
    public int UserCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalEstimated { get; set; } = 100; // 估算总数
}

/// <summary>
/// 同步进度事件参数
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public int Percentage { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 日志消息事件参数
/// </summary>
public class LogMessageEventArgs : EventArgs
{
    public string Message { get; set; } = "";
    public LogLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 同步完成事件参数
/// </summary>
public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = new();
}

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
