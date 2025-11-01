namespace SyncEkpToCasdoor.Web.Models;

public class CasdoorSettings
{
    public string Authority { get; set; } = "http://sso.fzcsps.com";
    public string ClientId { get; set; } = "aecd00a352e5c560ffe6";
    public string ClientSecret { get; set; } = "";  // 需要从Casdoor配置中获取
    // 留空，实际运行时根据 AppSettings.Domain/Protocol 及 CallbackPath 自动生成
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "read";
    public string AllowedOwner { get; set; } = "fzswjtOrganization";
    public string TokenEndpoint { get; set; } = "http://sso.fzcsps.com/api/login/oauth/access_token";
    public string UserInfoEndpoint { get; set; } = "http://sso.fzcsps.com/api/userinfo";
}
