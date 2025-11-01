using System.Net.Http;

namespace SyncEkpToCasdoor.Web.Services;

public class ForwardingCookieHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardingCookieHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx != null && ctx.Request.Headers.TryGetValue("Cookie", out var cookie))
        {
            // 将当前请求的 Cookie 转发给后端 API
            if (!request.Headers.Contains("Cookie"))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie.ToString());
            }
        }
        return base.SendAsync(request, cancellationToken);
    }
}
