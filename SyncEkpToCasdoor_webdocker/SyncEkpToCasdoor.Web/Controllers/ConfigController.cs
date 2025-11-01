using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyncEkpToCasdoor.Web.Services;
using System.Text.Json;

namespace SyncEkpToCasdoor.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
// 注意：类级别默认不限制，分别在 Action 上细化授权
public class ConfigController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfigurationService configService, IConfiguration configuration, ILogger<ConfigController> logger)
    {
        _configService = configService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("settings")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _configService.GetAllSettingsAsync();
            return Ok(new { success = true, data = settings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置失败");
            return StatusCode(500, new { success = false, message = $"获取配置失败: {ex.Message}" });
        }
    }

    [HttpPost("settings")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveSettingsRequest request)
    {
        try
        {
            _logger.LogInformation("收到配置保存请求");

            // 验证域名
            if (string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new { success = false, message = "域名不能为空" });
            }

            // 如果要修改密码,验证当前密码
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var currentStoredPassword = await _configService.GetAdminPasswordAsync();
                if (request.CurrentPassword != currentStoredPassword)
                {
                    return Unauthorized(new { success = false, message = "当前密码验证失败" });
                }
            }

            // 构建保存的设置
            var settings = new Dictionary<string, string>
            {
                ["Domain"] = request.Domain.Trim(),
                ["Protocol"] = request.Protocol,
                ["AllowedUsers"] = request.AllowedUsers?.Trim() ?? ""
            };

            // 如果有新密码则更新,否则保持原密码
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                settings["AdminPassword"] = request.NewPassword;
            }
            else
            {
                settings["AdminPassword"] = await _configService.GetAdminPasswordAsync();
            }

            await _configService.SaveSettingsAsync(settings);

            _logger.LogInformation("配置保存成功: Domain={Domain}, Protocol={Protocol}", 
                request.Domain, request.Protocol);

            return Ok(new { 
                success = true, 
                message = "配置保存成功！" + 
                    (!string.IsNullOrWhiteSpace(request.NewPassword) ? " 密码已更新。" : "") 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
            return StatusCode(500, new { success = false, message = $"保存配置失败: {ex.Message}" });
        }
    }

    [HttpGet("system")]
    [AllowAnonymous]
    public IActionResult GetSystemConfig()
    {
        try
        {
            var config = new
            {
                ekpConnection = _configuration["EkpConnection"] ?? "",
                casdoorEndpoint = _configuration["CasdoorAuth:Authority"] ?? "",
                casdoorOwner = _configuration["CasdoorAuth:AllowedOwner"] ?? "",
                casdoorClientId = _configuration["CasdoorAuth:ClientId"] ?? "",
                casdoorApplicationName = "ekp-sync-app",
                targetCompanyIds = _configuration["TargetCompanyIds"] ?? "",
                scheduledSyncEnabled = _configuration.GetValue<bool>("ScheduledSync:Enabled"),
                scheduledSyncInterval = _configuration.GetValue<int>("ScheduledSync:IntervalSeconds")
            };

            return Ok(new { success = true, data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统配置失败");
            return StatusCode(500, new { success = false, message = $"获取系统配置失败: {ex.Message}" });
        }
    }

    [HttpPost("system")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveSystemConfig([FromBody] SaveSystemConfigRequest request)
    {
        try
        {
            _logger.LogInformation("收到系统配置保存请求");

            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            
            if (!System.IO.File.Exists(configFile))
            {
                return NotFound(new { success = false, message = "配置文件不存在" });
            }

            var json = await System.IO.File.ReadAllTextAsync(configFile);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "EkpConnection" && !string.IsNullOrWhiteSpace(request.EkpConnection))
                    {
                        writer.WriteString("EkpConnection", request.EkpConnection);
                    }
                    else if (property.Name == "TargetCompanyIds" && !string.IsNullOrWhiteSpace(request.TargetCompanyIds))
                    {
                        writer.WriteString("TargetCompanyIds", request.TargetCompanyIds);
                    }
                    else if (property.Name == "CasdoorAuth")
                    {
                        writer.WritePropertyName("CasdoorAuth");
                        writer.WriteStartObject();
                        
                        foreach (var casdoorProp in property.Value.EnumerateObject())
                        {
                            if (casdoorProp.Name == "Authority" && !string.IsNullOrWhiteSpace(request.CasdoorEndpoint))
                            {
                                writer.WriteString("Authority", request.CasdoorEndpoint);
                            }
                            else if (casdoorProp.Name == "AllowedOwner" && !string.IsNullOrWhiteSpace(request.CasdoorOwner))
                            {
                                writer.WriteString("AllowedOwner", request.CasdoorOwner);
                            }
                            else if (casdoorProp.Name == "ClientId" && !string.IsNullOrWhiteSpace(request.CasdoorClientId))
                            {
                                writer.WriteString("ClientId", request.CasdoorClientId);
                            }
                            else if (casdoorProp.Name == "ClientSecret" && !string.IsNullOrWhiteSpace(request.CasdoorClientSecret))
                            {
                                writer.WriteString("ClientSecret", request.CasdoorClientSecret);
                            }
                            else
                            {
                                casdoorProp.WriteTo(writer);
                            }
                        }
                        
                        writer.WriteEndObject();
                    }
                    else if (property.Name == "ScheduledSync")
                    {
                        writer.WritePropertyName("ScheduledSync");
                        writer.WriteStartObject();
                        writer.WriteBoolean("Enabled", request.ScheduledSyncEnabled);
                        writer.WriteNumber("IntervalSeconds", request.ScheduledSyncInterval);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            await System.IO.File.WriteAllTextAsync(configFile, updatedJson);

            _logger.LogInformation("系统配置保存成功");

            return Ok(new { 
                success = true, 
                message = "系统配置保存成功！需要重启应用才能生效。" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存系统配置失败");
            return StatusCode(500, new { success = false, message = $"保存系统配置失败: {ex.Message}" });
        }
    }

    [HttpPost("restart")]
    [Authorize(Roles = "Administrator")]
    public IActionResult RestartApplication()
    {
        try
        {
            _logger.LogWarning("管理员请求重启应用");
            
            // 异步触发应用退出，让容器的 restart 策略自动重启
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // 延迟1秒，确保响应发送完成
                Environment.Exit(0);
            });
            
            return Ok(new { success = true, message = "应用正在重启，请等待约5-10秒后刷新页面..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启应用失败");
            return StatusCode(500, new { success = false, message = $"重启失败: {ex.Message}" });
        }
    }
}

public class SaveSettingsRequest
{
    public string Domain { get; set; } = "";
    public string Protocol { get; set; } = "http";
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string AllowedUsers { get; set; } = "";
}

public class SaveSystemConfigRequest
{
    public string EkpConnection { get; set; } = "";
    public string CasdoorEndpoint { get; set; } = "";
    public string CasdoorOwner { get; set; } = "";
    public string CasdoorClientId { get; set; } = "";
    public string CasdoorClientSecret { get; set; } = "";
    public string TargetCompanyIds { get; set; } = "";
    public bool ScheduledSyncEnabled { get; set; }
    public int ScheduledSyncInterval { get; set; } = 3600;
}
