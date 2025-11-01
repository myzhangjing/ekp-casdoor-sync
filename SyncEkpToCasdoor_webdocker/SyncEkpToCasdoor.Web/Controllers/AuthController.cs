using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Casdoor.AspNetCore;

namespace SyncEkpToCasdoor.Web.Controllers;

public class AuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("/challenge")]
    public new IActionResult Challenge()
    {
        // 使用 Casdoor SDK 的认证方案
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/"
        };
        return base.Challenge(properties, "Casdoor");
    }

    [HttpPost("/api/admin-login")]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest request)
    {
        try
        {
            _logger.LogInformation("收到Admin登录请求: {Username}", request.Username);
            
            // 验证输入
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "用户名和密码不能为空" });
            }

            // 从配置读取管理员密码
            var adminPassword = _configuration.GetSection("AppSettings")["AdminPassword"] ?? "sosy3080@sohu.com";
            
            _logger.LogInformation("验证密码: 输入='{Input}', 配置='{Config}'", request.Password, adminPassword);

            // 验证特权密码
            if (request.Password != adminPassword)
            {
                _logger.LogWarning("密码验证失败");
                return Unauthorized(new { success = false, message = "特权密码错误" });
            }

            _logger.LogInformation("密码验证成功，创建会话");

            // 创建身份验证票据
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.NameIdentifier, $"admin-{request.Username}"),
                new Claim("owner", "admin"),
                new Claim("username", request.Username),
                new Claim("login_type", "admin"),
                new Claim(ClaimTypes.Role, "Administrator")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("Admin登录成功: {Username}", request.Username);

            return Ok(new { success = true, message = "登录成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin登录失败");
            return StatusCode(500, new { success = false, message = $"登录失败: {ex.Message}" });
        }
    }

    [HttpGet("/logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            _logger.LogInformation("开始登出流程");
            
            // 登出本地会话
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            _logger.LogInformation("本地会话已清除,重定向到登录页");

            // 直接重定向到登录页,不调用Casdoor登出
            // 因为Casdoor的logout需要id_token_hint,而我们使用Cookie认证没有保存id_token
            return Redirect("/login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出失败");
            return Redirect("/login");
        }
    }
    
    [HttpPost("/api/logout")]
    public async Task<IActionResult> LogoutApi()
    {
        try
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true, message = "登出成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出失败");
            return StatusCode(500, new { success = false, message = $"登出失败: {ex.Message}" });
        }
    }
}

public class AdminLoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
