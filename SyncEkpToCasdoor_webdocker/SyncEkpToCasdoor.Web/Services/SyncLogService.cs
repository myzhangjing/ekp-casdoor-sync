using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncEkpToCasdoor.Web.Models;

namespace SyncEkpToCasdoor.Web.Services
{
    /// <summary>
    /// 同步日志服务 - 负责记录和查询同步日志
    /// </summary>
    public interface ISyncLogService
    {
        /// <summary>
        /// 创建新的同步日志
        /// </summary>
        Models.SyncLog CreateLog(string syncType, List<string> companyIds, string triggeredBy = "System");

        /// <summary>
        /// 添加日志条目
        /// </summary>
        void AddEntry(Models.SyncLog log, string level, string step, string message, string? companyId = null, string? details = null);

        /// <summary>
        /// 完成同步日志（成功）
        /// </summary>
        Task CompleteLogAsync(Models.SyncLog log, Models.SyncStatistics statistics);

        /// <summary>
        /// 完成同步日志（失败）
        /// </summary>
        Task FailLogAsync(Models.SyncLog log, string errorMessage, Models.SyncStatistics? statistics = null);

        /// <summary>
        /// 获取最近的同步日志
        /// </summary>
        Task<List<Models.SyncLog>> GetRecentLogsAsync(int count = 50);

        /// <summary>
        /// 根据ID获取日志详情
        /// </summary>
        Task<Models.SyncLog?> GetLogByIdAsync(string logId);

        /// <summary>
        /// 获取统计信息（最近24小时）
        /// </summary>
        Task<SyncLogSummary> GetSummaryAsync();

        /// <summary>
        /// 清理旧日志（保留最近N天）
        /// </summary>
        Task CleanupOldLogsAsync(int retentionDays = 30);
    }

    public class SyncLogService : ISyncLogService
    {
        private readonly ILogger<SyncLogService> _logger;
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private static readonly object _fileLock = new object();

        public SyncLogService(ILogger<SyncLogService> logger)
        {
            _logger = logger;
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "sync_logs");
            _logFilePath = Path.Combine(_logDirectory, "sync_history.json");

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                _logger.LogInformation("创建同步日志目录: {Directory}", _logDirectory);
            }
        }

        public Models.SyncLog CreateLog(string syncType, List<string> companyIds, string triggeredBy = "System")
        {
            var log = new Models.SyncLog
            {
                Id = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}",
                StartTime = DateTime.Now,
                SyncType = syncType,
                CompanyIds = new List<string>(companyIds),
                TriggeredBy = triggeredBy,
                Status = "Running"
            };

            AddEntry(log, "Info", "Start", $"开始{syncType}同步，目标公司数: {companyIds.Count}", details: string.Join(", ", companyIds));

            _logger.LogInformation("创建同步日志: {LogId}, 类型: {Type}, 公司: {Companies}", 
                log.Id, syncType, string.Join(", ", companyIds));

            return log;
        }

        public void AddEntry(Models.SyncLog log, string level, string step, string message, string? companyId = null, string? details = null)
        {
            var entry = new Models.SyncLogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Step = step,
                Message = message,
                CompanyId = companyId,
                Details = details
            };

            log.Entries.Add(entry);

            // 同时记录到系统日志
            var logMessage = $"[SyncLog {log.Id}] [{step}] {message}";
            if (!string.IsNullOrEmpty(companyId))
            {
                logMessage += $" (公司: {companyId})";
            }

            switch (level)
            {
                case "Error":
                    _logger.LogError(logMessage);
                    break;
                case "Warning":
                    _logger.LogWarning(logMessage);
                    break;
                default:
                    _logger.LogInformation(logMessage);
                    break;
            }
        }

        public async Task CompleteLogAsync(Models.SyncLog log, Models.SyncStatistics statistics)
        {
            log.EndTime = DateTime.Now;
            log.DurationMs = (long)(log.EndTime.Value - log.StartTime).TotalMilliseconds;
            log.Statistics = statistics;
            log.Status = statistics.FailedCompanies > 0 ? "PartialSuccess" : "Success";

            AddEntry(log, "Info", "Complete", 
                $"同步完成，耗时: {log.DurationMs}ms, 成功: {statistics.SuccessfulCompanies}/{statistics.TotalCompanies} 公司");

            await SaveLogAsync(log);

            _logger.LogInformation("同步日志完成: {LogId}, 状态: {Status}, 耗时: {Duration}ms", 
                log.Id, log.Status, log.DurationMs);
        }

        public async Task FailLogAsync(Models.SyncLog log, string errorMessage, Models.SyncStatistics? statistics = null)
        {
            log.EndTime = DateTime.Now;
            log.DurationMs = (long)(log.EndTime.Value - log.StartTime).TotalMilliseconds;
            log.Status = "Failed";
            log.ErrorMessage = errorMessage;
            
            if (statistics != null)
            {
                log.Statistics = statistics;
            }

            AddEntry(log, "Error", "Error", $"同步失败: {errorMessage}");

            await SaveLogAsync(log);

            _logger.LogError("同步日志失败: {LogId}, 错误: {Error}", log.Id, errorMessage);
        }

        public async Task<List<Models.SyncLog>> GetRecentLogsAsync(int count = 50)
        {
            try
            {
                var logs = await LoadAllLogsAsync();
                return logs
                    .OrderByDescending(l => l.StartTime)
                    .Take(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取同步日志失败");
                return new List<Models.SyncLog>();
            }
        }

        public async Task<Models.SyncLog?> GetLogByIdAsync(string logId)
        {
            try
            {
                var logs = await LoadAllLogsAsync();
                return logs.FirstOrDefault(l => l.Id == logId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取同步日志详情失败: {LogId}", logId);
                return null;
            }
        }

        public async Task<SyncLogSummary> GetSummaryAsync()
        {
            try
            {
                var logs = await LoadAllLogsAsync();
                var last24Hours = DateTime.Now.AddHours(-24);
                var recentLogs = logs.Where(l => l.StartTime >= last24Hours).ToList();

                return new SyncLogSummary
                {
                    TotalSyncs = recentLogs.Count,
                    SuccessfulSyncs = recentLogs.Count(l => l.Status == "Success"),
                    FailedSyncs = recentLogs.Count(l => l.Status == "Failed"),
                    PartialSuccessSyncs = recentLogs.Count(l => l.Status == "PartialSuccess"),
                    TotalUsers = recentLogs.Sum(l => l.Statistics.TotalUsers),
                    TotalDepartments = recentLogs.Sum(l => l.Statistics.TotalDepartments),
                    LastSyncTime = logs.OrderByDescending(l => l.StartTime).FirstOrDefault()?.StartTime,
                    AverageDurationMs = recentLogs.Any() ? (long)recentLogs.Average(l => l.DurationMs) : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取同步日志统计失败");
                return new SyncLogSummary();
            }
        }

        public async Task CleanupOldLogsAsync(int retentionDays = 30)
        {
            try
            {
                var logs = await LoadAllLogsAsync();
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var recentLogs = logs.Where(l => l.StartTime >= cutoffDate).ToList();

                var removedCount = logs.Count - recentLogs.Count;
                if (removedCount > 0)
                {
                    await SaveAllLogsAsync(recentLogs);
                    _logger.LogInformation("清理旧日志完成，删除 {Count} 条记录", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧日志失败");
            }
        }

        private async Task SaveLogAsync(Models.SyncLog log)
        {
            try
            {
                var logs = await LoadAllLogsAsync();
                
                // 更新或添加
                var existingIndex = logs.FindIndex(l => l.Id == log.Id);
                if (existingIndex >= 0)
                {
                    logs[existingIndex] = log;
                }
                else
                {
                    logs.Add(log);
                }

                await SaveAllLogsAsync(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存同步日志失败: {LogId}", log.Id);
                throw;
            }
        }

        private async Task<List<Models.SyncLog>> LoadAllLogsAsync()
        {
            if (!File.Exists(_logFilePath))
            {
                return new List<Models.SyncLog>();
            }

            try
            {
                string json;
                lock (_fileLock)
                {
                    json = File.ReadAllText(_logFilePath);
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<Models.SyncLog>();
                }

                return JsonSerializer.Deserialize<List<Models.SyncLog>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Models.SyncLog>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "反序列化同步日志失败");
                // 备份损坏的文件
                var backupPath = $"{_logFilePath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(_logFilePath, backupPath, true);
                return new List<Models.SyncLog>();
            }
        }

        private async Task SaveAllLogsAsync(List<Models.SyncLog> logs)
        {
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            lock (_fileLock)
            {
                File.WriteAllText(_logFilePath, json);
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 同步日志摘要
    /// </summary>
    public class SyncLogSummary
    {
        public int TotalSyncs { get; set; }
        public int SuccessfulSyncs { get; set; }
        public int FailedSyncs { get; set; }
        public int PartialSuccessSyncs { get; set; }
        public int TotalUsers { get; set; }
        public int TotalDepartments { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public long AverageDurationMs { get; set; }
    }
}
