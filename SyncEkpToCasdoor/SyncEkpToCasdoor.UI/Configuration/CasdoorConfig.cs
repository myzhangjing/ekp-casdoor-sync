namespace SyncEkpToCasdoor.UI.Configuration
{
    /// <summary>
    /// Casdoor 内置配置
    /// </summary>
    public static class CasdoorConfig
    {
        /// <summary>
        /// Casdoor 服务器地址
        /// </summary>
        public const string Endpoint = "http://sso.fzcsps.com";

        /// <summary>
        /// 应用 Client ID
        /// </summary>
        public const string ClientId = "aecd00a352e5c560ffe6";

        /// <summary>
        /// 应用 Client Secret
        /// </summary>
        public const string ClientSecret = "4402518b20dd191b8b48d6240bc786a4f847899a";

        /// <summary>
        /// 组织 Owner
        /// </summary>
        public const string DefaultOwner = "fzswjtOrganization";

        /// <summary>
        /// 是否启用强制登录
        /// </summary>
        public const bool RequireLogin = true;
    }
}
