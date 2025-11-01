using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SyncEkpToCasdoor.UI.Services
{
    /// <summary>
    /// 自定义 URI Scheme 注册服务
    /// 用于支持 myapp://callback 的 OAuth2 回调
    /// </summary>
    public static class UriSchemeRegistrar
    {
        private const string APP_PROTOCOL = "ekpsync";
        private const string APP_NAME = "EKP-Casdoor Sync Tool";

        /// <summary>
        /// 注册自定义 URI Scheme
        /// </summary>
        /// <returns>是否注册成功</returns>
        public static bool RegisterUriScheme()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debug.WriteLine("自定义 URI Scheme 仅支持 Windows 平台");
                return false;
            }

            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    Debug.WriteLine("无法获取当前执行文件路径");
                    return false;
                }

                // 注册到 HKEY_CURRENT_USER（不需要管理员权限）
                using (var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Classes\{APP_PROTOCOL}"))
                {
                    if (key == null)
                    {
                        Debug.WriteLine("无法创建注册表项");
                        return false;
                    }

                    key.SetValue("", $"URL:{APP_NAME}");
                    key.SetValue("URL Protocol", "");

                    using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
                    {
                        defaultIcon?.SetValue("", $"\"{exePath}\",0");
                    }

                    using (var command = key.CreateSubKey(@"shell\open\command"))
                    {
                        // 命令格式：传递完整URL作为参数
                        command?.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                Debug.WriteLine($"成功注册自定义 URI Scheme: {APP_PROTOCOL}://");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"注册自定义 URI Scheme 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查 URI Scheme 是否已注册
        /// </summary>
        public static bool IsUriSchemeRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Classes\{APP_PROTOCOL}"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 注销 URI Scheme
        /// </summary>
        public static bool UnregisterUriScheme()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"SOFTWARE\Classes\{APP_PROTOCOL}", false);
                Debug.WriteLine($"成功注销自定义 URI Scheme: {APP_PROTOCOL}://");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"注销自定义 URI Scheme 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取回调 URI
        /// </summary>
        public static string GetCallbackUri()
        {
            return $"{APP_PROTOCOL}://callback";
        }

        /// <summary>
        /// 解析回调 URI 中的参数
        /// </summary>
        public static bool TryParseCallbackUri(string uri, out string? code, out string? state, out string? error)
        {
            code = null;
            state = null;
            error = null;

            if (string.IsNullOrEmpty(uri))
                return false;

            try
            {
                var uriObj = new Uri(uri);
                
                // 检查 scheme
                if (!uriObj.Scheme.Equals(APP_PROTOCOL, StringComparison.OrdinalIgnoreCase))
                    return false;

                // 解析查询参数
                var query = uriObj.Query;
                if (string.IsNullOrEmpty(query))
                    return false;

                var parameters = System.Web.HttpUtility.ParseQueryString(query);
                code = parameters["code"];
                state = parameters["state"];
                error = parameters["error"];

                return !string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析回调 URI 失败: {ex.Message}");
                return false;
            }
        }
    }
}
