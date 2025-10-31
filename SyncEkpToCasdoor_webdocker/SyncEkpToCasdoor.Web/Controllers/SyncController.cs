using Microsoft.AspNetCore.Mvc;
using SyncEkpToCasdoor.Web.Services;

namespace SyncEkpToCasdoor.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [HttpGet("test-connections")]
    public async Task<IActionResult> TestConnections()
    {
        try
        {
            var result = await _syncService.TestConnectionsAsync();
            var success = result.EkpConnected && result.CasdoorConnected;
            var details = new List<string>();
            
            if (result.EkpConnected)
            {
                details.Add($"EKP: OK - {result.EkpUsersCount} users, {result.EkpOrgsCount} orgs");
            }
            else
            {
                details.Add($"EKP: Failed - {result.EkpMessage}");
            }
            
            if (result.CasdoorConnected)
            {
                details.Add($"Casdoor: OK - {result.CasdoorUsersCount} users, {result.CasdoorGroupsCount} groups");
            }
            else
            {
                details.Add($"Casdoor: Failed - {result.CasdoorMessage}");
            }
            
            return Ok(new 
            { 
                success = success,
                message = success ? "All connections OK" : "Some connections failed",
                details = details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewSync()
    {
        try
        {
            var result = await _syncService.PreviewSyncAsync();
            var details = new List<string>
            {
                $"Organizations: {result.OrgsToCreate} to create, {result.OrgsToUpdate} to update",
                $"Users: {result.UsersToCreate} to create, {result.UsersToUpdate} to update"
            };
            
            return Ok(new 
            { 
                success = result.Success,
                message = result.Message,
                details = details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览同步失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("full-sync")]
    public async Task<IActionResult> FullSync()
    {
        try
        {
            var result = await _syncService.SyncAllAsync();
            var details = new List<string>
            {
                $"Organizations processed: {result.OrganizationsProcessed}",
                $"Users processed: {result.UsersProcessed}",
                $"Duration: {(result.EndTime - result.StartTime).TotalSeconds:F1} seconds"
            };
            
            if (result.Errors.Any())
            {
                details.Add($"Errors: {result.Errors.Count}");
            }
            
            return Ok(new 
            { 
                success = result.Success,
                message = result.Message,
                details = details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完整同步失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = await _syncService.GetSyncStateAsync();
            return Ok(new
            {
                IsRunning = status.IsRunning,
                LastSyncTime = status.LastFullSync ?? status.LastIncrementalSync,
                SyncStartTime = status.IsRunning ? DateTime.Now : (DateTime?)null,
                LastSyncType = status.LastSyncType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取状态失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int count = 50)
    {
        try
        {
            var logs = await _syncService.GetSyncLogsAsync(count);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志失败");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
