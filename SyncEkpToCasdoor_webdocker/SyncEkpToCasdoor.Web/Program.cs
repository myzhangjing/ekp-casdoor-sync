using SyncEkpToCasdoor.Web.Components;
using SyncEkpToCasdoor.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 从配置文件读取应用配置
var appSettings = builder.Configuration.GetSection("AppSettings");
var domain = appSettings["Domain"] ?? "localhost:5233";
var protocol = appSettings["Protocol"] ?? "http";
var adminPassword = appSettings["AdminPassword"] ?? "sosy3080@sohu.com";

// 从配置文件读取 Casdoor 配置
var casdoorConfig = builder.Configuration.GetSection("CasdoorAuth");
var clientId = casdoorConfig["ClientId"] ?? throw new Exception("ClientId 未配置");
var clientSecret = casdoorConfig["ClientSecret"] ?? throw new Exception("ClientSecret 未配置");

// 构建 RedirectUri: 优先使用配置文件中的值,否则使用域名配置
var redirectUri = casdoorConfig["RedirectUri"];
if (string.IsNullOrWhiteSpace(redirectUri))
{
    redirectUri = $"{protocol}://{domain}/callback";
}

Console.WriteLine($"=== 应用配置 ===");
Console.WriteLine($"域名: {domain}");
Console.WriteLine($"协议: {protocol}");
Console.WriteLine($"RedirectUri: {redirectUri}");
Console.WriteLine($"管理员密码: {adminPassword}");
Console.WriteLine($"===============");

var scope = casdoorConfig["Scope"] ?? "read";
var authEndpoint = casdoorConfig["AuthorizationEndpoint"] ?? "http://sso.fzcsps.com/login/oauth/authorize";
var tokenEndpoint = casdoorConfig["TokenEndpoint"] ?? "http://sso.fzcsps.com/api/login/oauth/access_token";
var userInfoEndpoint = casdoorConfig["UserInfoEndpoint"] ?? "http://sso.fzcsps.com/api/userinfo";

// 手动配置 OAuth 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Casdoor";
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    // 修复跨域回调的 Cookie 问题
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // 因为使用 HTTP

    // 动态校验已登录用户是否仍在允许名单中（AllowedUsers 修改后立刻生效）
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = context =>
        {
            try
            {
                var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var section = configuration.GetSection("AppSettings");
                var allowedUsersStr = section["AllowedUsers"];

                if (!string.IsNullOrWhiteSpace(allowedUsersStr))
                {
                    var allowedUsers = allowedUsersStr
                        .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrEmpty(u))
                        .ToList();

                    if (allowedUsers.Any())
                    {
                        var name = context.Principal?.FindFirstValue(ClaimTypes.Name);
                        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                        var nameId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                        var isAllowed = allowedUsers.Any(allowed =>
                            string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(allowed, email, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(allowed, nameId, StringComparison.OrdinalIgnoreCase));

                        if (!isAllowed)
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogWarning("当前用户不在允许列表中，拒绝访问并清除登录状态: name={Name}, email={Email}, id={Id}", name, email, nameId);
                            context.RejectPrincipal();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Cookie 验证过程中发生错误");
                // 发生错误时，不影响现有会话，但记录日志
            }

            return Task.CompletedTask;
        }
    };
})
.AddOAuth("Casdoor", options =>
{
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.CallbackPath = new PathString("/callback");
    
    options.AuthorizationEndpoint = authEndpoint;
    options.TokenEndpoint = tokenEndpoint;
    options.UserInformationEndpoint = userInfoEndpoint;
    
    options.Scope.Add(scope);
    options.SaveTokens = true;
    
    // 修复 Correlation Cookie 问题
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
    
    // 处理用户信息回调
    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            try
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("==== OAuth 回调开始 ====");
                logger.LogInformation("AccessToken: {Token}", context.AccessToken?.Substring(0, Math.Min(20, context.AccessToken?.Length ?? 0)));
                
                // 使用 Casdoor 的 get-account 接口获取完整用户信息
                var userInfoUrl = $"{casdoorConfig["Authority"]}/api/get-account";
                var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                logger.LogInformation("请求用户信息: {Url}", userInfoUrl);
                var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                
                var contentString = await response.Content.ReadAsStringAsync();
                logger.LogInformation("用户信息响应状态: {StatusCode}", response.StatusCode);
                logger.LogInformation("用户信息响应内容: {Content}", contentString);
                
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("获取用户信息失败: {StatusCode}, 内容: {Content}", response.StatusCode, contentString);
                    throw new Exception($"无法从 Casdoor 获取用户信息: {response.StatusCode}");
                }
                
                var userJson = System.Text.Json.JsonDocument.Parse(contentString).RootElement;
                
                // 检查响应格式并提取用户信息
                System.Text.Json.JsonElement userElement;
                if (userJson.TryGetProperty("data", out var dataElement))
                {
                    userElement = dataElement;
                    logger.LogInformation("使用 data 字段");
                }
                else if (userJson.TryGetProperty("data2", out var data2Element))
                {
                    userElement = data2Element;
                    logger.LogInformation("使用 data2 字段");
                }
                else
                {
                    userElement = userJson;
                    logger.LogInformation("直接使用根元素");
                }
                
                // 1. 提取用户基本信息
                string? owner = userElement.TryGetProperty("owner", out var ownerElement) ? ownerElement.GetString() : null;
                string? userName = userElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                string? userId = userElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                string? displayName = userElement.TryGetProperty("displayName", out var displayElement) ? displayElement.GetString() : null;
                
                logger.LogInformation("解析用户信息: owner={Owner}, name={Name}, displayName={DisplayName}", 
                    owner ?? "null", userName ?? "null", displayName ?? "null");
                
                // 2. 检查用户访问权限（如果配置了AllowedUsers）
                var allowedUsersStr = appSettings["AllowedUsers"];
                if (!string.IsNullOrWhiteSpace(allowedUsersStr))
                {
                    var allowedUsers = allowedUsersStr.Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(u => u.Trim())
                                                      .Where(u => !string.IsNullOrEmpty(u))
                                                      .ToList();
                    
                    if (allowedUsers.Any())
                    {
                        var userEmail = userElement.TryGetProperty("email", out var e) ? e.GetString() : null;
                        var isAllowed = allowedUsers.Any(allowed => 
                            allowed.Equals(userName, StringComparison.OrdinalIgnoreCase) ||
                            allowed.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
                            allowed.Equals(userEmail, StringComparison.OrdinalIgnoreCase) ||
                            allowed.Equals(displayName, StringComparison.OrdinalIgnoreCase));
                        
                        if (!isAllowed)
                        {
                            logger.LogWarning("用户 {UserName} 未在允许访问列表中", userName ?? displayName);
                            context.Fail("您没有权限访问此系统。请联系管理员。");
                            return;
                        }
                        
                        logger.LogInformation("用户 {UserName} 在允许访问列表中，允许登录", userName ?? displayName);
                    }
                }
                
                // 3. 记录登录信息
                logger.LogInformation("用户 {UserName} (组织: {Owner}) 登录成功", userName ?? displayName, owner ?? "unknown");
                
                // 4. 添加用户声明 (Name 是必需的)
                var finalUserName = !string.IsNullOrEmpty(displayName) ? displayName : 
                                   (!string.IsNullOrEmpty(userName) ? userName : 
                                   (!string.IsNullOrEmpty(userId) ? userId : "unknown"));
                
                logger.LogInformation("最终用户名: {FinalUserName}", finalUserName);
                
                context.Identity?.AddClaim(new Claim(ClaimTypes.Name, finalUserName));
                context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId ?? ""));
                context.Identity?.AddClaim(new Claim("owner", owner ?? "unknown"));
                
                logger.LogInformation("Claims 已添加, Identity.IsAuthenticated: {IsAuth}", context.Identity?.IsAuthenticated);
                
                // 5. 添加其他有用的claims
                if (!string.IsNullOrEmpty(userName))
                {
                    context.Identity?.AddClaim(new Claim("username", userName));
                }
                if (userElement.TryGetProperty("email", out var emailElement) && !string.IsNullOrEmpty(emailElement.GetString()))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, emailElement.GetString()!));
                }
                
                logger.LogInformation("==== OAuth 回调完成 ====");
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "处理用户信息时发生错误");
                throw;
            }
        }
    };
});

// 配置 Data Protection (解决 Cookie 和 OAuth state 持久化问题)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
    .SetApplicationName("SyncEkpToCasdoor");

// 添加授权
builder.Services.AddAuthorization();

// 添加 Blazor Server 的认证状态支持
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 添加控制器支持
builder.Services.AddControllers();

// 注册同步服务
builder.Services.AddSingleton<ISyncService, SyncService>();

// 注册配置服务
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// 注册同步日志服务
builder.Services.AddSingleton<ISyncLogService, SyncLogService>();

// 注册定时同步后台服务
builder.Services.AddHostedService<ScheduledSyncService>();

// 添加日志
builder.Services.AddLogging();

// 添加 HttpContextAccessor (用于特权登录)
builder.Services.AddHttpContextAccessor();

// 添加 HttpClient 工厂（带 Cookie 转发的命名客户端）
builder.Services.AddTransient<ForwardingCookieHandler>();
builder.Services.AddHttpClient("WithCookies", (sp, client) =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = httpContextAccessor.HttpContext?.Request;
    var baseUri = request != null ? $"{request.Scheme}://{request.Host}" : "http://localhost:9000";
    client.BaseAddress = new Uri(baseUri);
}).AddHttpMessageHandler<ForwardingCookieHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// 添加认证和授权中间件
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseStaticFiles();

// 映射控制器路由 (用于 /challenge, /logout, /callback)
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
