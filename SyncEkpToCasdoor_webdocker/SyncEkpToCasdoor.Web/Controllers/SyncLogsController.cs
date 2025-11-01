using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyncEkpToCasdoor.Web.Models;
using SyncEkpToCasdoor.Web.Services;

namespace SyncEkpToCasdoor.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncLogsController : ControllerBase
    {
        private readonly ISyncLogService _syncLogService;
        private readonly ILogger<SyncLogsController> _logger;

        public SyncLogsController(
            ISyncLogService syncLogService,
            ILogger<SyncLogsController> logger)
        {
            _syncLogService = syncLogService;
            _logger = logger;
        }

        /// <summary>
        /// 获取最近的同步日志列表
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 50)
        {
            try
            {
                var logs = await _syncLogService.GetRecentLogsAsync(count);
                return Ok(new { success = true, data = logs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取同步日志列表失败");
                return StatusCode(500, new { success = false, message = "获取日志失败" });
            }
        }

        /// <summary>
        /// 获取指定日志的详细信息
        /// </summary>
        [HttpGet("{logId}")]
        public async Task<IActionResult> GetLogDetails(string logId)
        {
            try
            {
                var log = await _syncLogService.GetLogByIdAsync(logId);
                if (log == null)
                {
                    return NotFound(new { success = false, message = "日志不存在" });
                }

                return Ok(new { success = true, data = log });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取同步日志详情失败: {LogId}", logId);
                return StatusCode(500, new { success = false, message = "获取日志详情失败" });
            }
        }

        /// <summary>
        /// 获取同步统计摘要（最近24小时）
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var summary = await _syncLogService.GetSummaryAsync();
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取同步统计失败");
                return StatusCode(500, new { success = false, message = "获取统计信息失败" });
            }
        }

        /// <summary>
        /// 清理旧日志（仅管理员）
        /// </summary>
        [HttpPost("cleanup")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CleanupOldLogs([FromQuery] int retentionDays = 30)
        {
            try
            {
                await _syncLogService.CleanupOldLogsAsync(retentionDays);
                return Ok(new { success = true, message = $"已清理{retentionDays}天前的日志" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧日志失败");
                return StatusCode(500, new { success = false, message = "清理日志失败" });
            }
        }
    }
}
