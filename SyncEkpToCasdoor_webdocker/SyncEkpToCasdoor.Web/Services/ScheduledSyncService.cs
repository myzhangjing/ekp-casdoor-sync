using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SyncEkpToCasdoor.Web.Services
{
    /// <summary>
    /// 定时同步后台服务
    /// </summary>
    public class ScheduledSyncService : BackgroundService
    {
        private readonly ILogger<ScheduledSyncService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISyncService _syncService;
        private readonly ISyncLogService _syncLogService;
        private Timer? _timer;

        public ScheduledSyncService(
            ILogger<ScheduledSyncService> logger,
            IConfiguration configuration,
            ISyncService syncService,
            ISyncLogService syncLogService)
        {
            _logger = logger;
            _configuration = configuration;
            _syncService = syncService;
            _syncLogService = syncLogService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("定时同步服务启动");

            var enabled = _configuration.GetValue<bool>("ScheduledSync:Enabled", false);
            if (!enabled)
            {
                _logger.LogInformation("定时同步未启用");
                return Task.CompletedTask;
            }

            var intervalSeconds = _configuration.GetValue<int>("ScheduledSync:IntervalSeconds", 3600);
            _logger.LogInformation("定时同步已启用，间隔: {Interval} 秒", intervalSeconds);

            _timer = new Timer(
                callback: async _ => await DoWork(stoppingToken),
                state: null,
                dueTime: TimeSpan.FromSeconds(5), // 5秒后首次执行
                period: TimeSpan.FromSeconds(intervalSeconds));

            return Task.CompletedTask;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            Models.SyncLog? syncLog = null;
            var statistics = new Models.SyncStatistics();
            
            try
            {
                _logger.LogInformation("定时同步任务开始执行 - {Time}", DateTime.Now);

                var companyIds = _configuration.GetValue<string>("TargetCompanyIds", "");
                if (string.IsNullOrEmpty(companyIds))
                {
                    _logger.LogWarning("未配置 TargetCompanyIds，跳过同步");
                    return;
                }

                var companies = companyIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();
                
                // 创建同步日志
                syncLog = _syncLogService.CreateLog("Scheduled", companies, "System");
                statistics.TotalCompanies = companies.Count;

                _logger.LogInformation("准备同步 {Count} 个公司", companies.Count);
                _syncLogService.AddEntry(syncLog, "Info", "Prepare", $"准备同步 {companies.Count} 个公司");

                foreach (var companyId in companies)
                {
                    try
                    {
                        _logger.LogInformation("开始同步公司: {CompanyId}", companyId);
                        _syncLogService.AddEntry(syncLog, "Info", "SyncCompany", $"开始同步公司", companyId);
                        
                        await _syncService.SyncCompanyAsync(companyId, cancellationToken);
                        
                        statistics.SuccessfulCompanies++;
                        _logger.LogInformation("公司 {CompanyId} 同步完成", companyId);
                        _syncLogService.AddEntry(syncLog, "Info", "SyncCompany", $"公司同步完成", companyId);
                    }
                    catch (Exception ex)
                    {
                        statistics.FailedCompanies++;
                        _logger.LogError(ex, "同步公司 {CompanyId} 失败", companyId);
                        _syncLogService.AddEntry(syncLog, "Error", "SyncCompany", 
                            $"公司同步失败: {ex.Message}", companyId, ex.StackTrace);
                    }
                }

                _logger.LogInformation("定时同步任务完成 - {Time}", DateTime.Now);
                
                // 完成日志
                if (syncLog != null)
                {
                    await _syncLogService.CompleteLogAsync(syncLog, statistics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时同步任务执行失败");
                
                // 记录失败日志
                if (syncLog != null)
                {
                    await _syncLogService.FailLogAsync(syncLog, ex.Message, statistics);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("定时同步服务停止");
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}
