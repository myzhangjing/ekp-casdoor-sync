using SyncEkpToCasdoor.Web.Components;
using SyncEkpToCasdoor.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 从配置文件读取 Casdoor 配置
var casdoorConfig = builder.Configuration.GetSection("CasdoorAuth");
var clientId = casdoorConfig["ClientId"] ?? throw new Exception("ClientId 未配置");
var clientSecret = casdoorConfig["ClientSecret"] ?? throw new Exception("ClientSecret 未配置");
var redirectUri = casdoorConfig["RedirectUri"] ?? "http://localhost:5233/callback";
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
    
    // 处理用户信息回调,验证 owner 必须是 built-in
    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            try
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                
                // 获取用户信息 - 使用 Casdoor 的 /api/get-account 接口
                var userInfoUrl = $"{casdoorConfig["Authority"]}/api/get-account";
                var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                
                var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                
                System.Text.Json.JsonElement userJson;
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var responseJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);
                    
                    // Casdoor 的 /api/get-account 返回格式: { "data": ..., "data2": { ...用户信息... } }
                    if (responseJson.TryGetProperty("data2", out var data2Element))
                    {
                        userJson = data2Element;
                        logger.LogInformation("成功从 Casdoor 获取用户信息: owner={Owner}, name={Name}", 
                            userJson.TryGetProperty("owner", out var o) ? o.GetString() : "N/A",
                            userJson.TryGetProperty("name", out var n) ? n.GetString() : "N/A");
                    }
                    else
                    {
                        throw new Exception("Casdoor 响应格式错误: 缺少 data2 字段");
                    }
                }
                else
                {
                    throw new Exception($"无法从 Casdoor 获取用户信息: {response.StatusCode}");
                }
                
                // 1. 提取用户基本信息
                string? owner = userJson.TryGetProperty("owner", out var ownerElement) ? ownerElement.GetString() : null;
                string? userName = userJson.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                string? userId = userJson.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                string? displayName = userJson.TryGetProperty("displayName", out var displayElement) ? displayElement.GetString() : null;
                
                // 2. 验证组织 (必须是 built-in)
                var allowedOwner = casdoorConfig["AllowedOwner"] ?? "built-in";
                if (string.IsNullOrEmpty(owner))
                {
                    throw new Exception("无法获取用户的组织信息");
                }
                
                if (owner != allowedOwner)
                {
                    logger.LogWarning("拒绝登录: 用户 {UserName} 属于组织 '{Owner}', 仅允许 '{AllowedOwner}' 组织", userName, owner, allowedOwner);
                    throw new Exception($"仅允许 '{allowedOwner}' 组织的用户登录");
                }
                
                logger.LogInformation("用户 {UserName} (组织: {Owner}) 通过验证", userName, owner);
                
                // 3. 添加用户声明 (Name 是必需的)
                var finalUserName = !string.IsNullOrEmpty(displayName) ? displayName : 
                                   (!string.IsNullOrEmpty(userName) ? userName : 
                                   (!string.IsNullOrEmpty(userId) ? userId : "unknown"));
                
                context.Identity?.AddClaim(new Claim(ClaimTypes.Name, finalUserName));
                context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId ?? ""));
                context.Identity?.AddClaim(new Claim("owner", owner));
                
                // 4. 添加其他有用的claims
                if (!string.IsNullOrEmpty(userName))
                {
                    context.Identity?.AddClaim(new Claim("username", userName));
                }
                if (userJson.TryGetProperty("email", out var emailElement) && !string.IsNullOrEmpty(emailElement.GetString()))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, emailElement.GetString()!));
                }
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

// 添加授权
builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 添加控制器支持
builder.Services.AddControllers();

// 注册同步服务
builder.Services.AddSingleton<ISyncService, SyncService>();

// 注册定时同步后台服务
builder.Services.AddHostedService<ScheduledSyncService>();

// 添加日志
builder.Services.AddLogging();

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

app.MapStaticAssets();

// 映射控制器路由 (用于 /challenge, /logout, /callback)
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
