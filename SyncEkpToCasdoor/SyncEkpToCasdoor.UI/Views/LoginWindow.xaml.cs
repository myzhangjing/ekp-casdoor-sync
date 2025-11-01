using System.Windows;
using Microsoft.Web.WebView2.Core;
using SyncEkpToCasdoor.UI.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace SyncEkpToCasdoor.UI.Views
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly HttpClient _httpClient = new();

        public LoginWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoginWindow 构造函数开始");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("InitializeComponent 完成");
                InitializeAsync();
                System.Diagnostics.Debug.WriteLine("InitializeAsync 调用完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoginWindow 构造函数异常: {ex}");
                MessageBox.Show($"创建登录窗口失败：{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async void InitializeAsync()
        {
            try
            {
                // 显示加载提示
                LoadingPanel.Visibility = Visibility.Visible;

                // 初始化 WebView2
                await LoginWebView.EnsureCoreWebView2Async(null);

                // 监听导航事件
                LoginWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                LoginWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // 构建登录 URL - 使用 localhost 回调
                var redirectUri = Uri.EscapeDataString("http://localhost:9527/callback");
                var state = Guid.NewGuid().ToString("N");
                var loginUrl = $"{CasdoorConfig.Endpoint}/login/oauth/authorize" +
                              $"?client_id={CasdoorConfig.ClientId}" +
                              $"&response_type=code" +
                              $"&redirect_uri={redirectUri}" +
                              $"&scope=read" +
                              $"&state={state}";

                // 加载登录页面
                LoginWebView.Source = new Uri(loginUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化登录页面失败：{ex.Message}\n\n详细信息：{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 不要立即关闭，给用户看错误信息的机会
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // 检查是否是回调 URL
            if (e.Uri.StartsWith("http://localhost:9527/callback"))
            {
                // 取消导航
                e.Cancel = true;

                // 解析授权码
                var uri = new Uri(e.Uri);
                var queryParams = ParseQueryString(uri.Query);
                var code = queryParams.ContainsKey("code") ? queryParams["code"] : null;
                var state = queryParams.ContainsKey("state") ? queryParams["state"] : null;

                if (!string.IsNullOrEmpty(code))
                {
                    // 处理授权码
                    _ = HandleAuthorizationCodeAsync(code, state);
                }
                else
                {
                    MessageBox.Show("未获取到授权码", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DialogResult = false;
                    Close();
                }
            }
        }

        private Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return result;

            query = query.TrimStart('?');
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }
            return result;
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // 隐藏加载提示
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        private async Task HandleAuthorizationCodeAsync(string code, string? state)
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;

                // 交换 access token
                var tokenUrl = $"{CasdoorConfig.Endpoint}/api/login/oauth/access_token";
                var requestData = new
                {
                    grant_type = "authorization_code",
                    client_id = CasdoorConfig.ClientId,
                    client_secret = CasdoorConfig.ClientSecret,
                    code = code,
                    redirect_uri = "http://localhost:9527/callback"
                };

                var response = await _httpClient.PostAsJsonAsync(tokenUrl, requestData);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                    var accessToken = tokenResponse.GetProperty("access_token").GetString();

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // 保存 token
                        Application.Current.Properties["AccessToken"] = accessToken;

                        // 获取用户信息
                        await GetUserInfoAsync(accessToken);

                        // 登录成功
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("获取访问令牌失败", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        DialogResult = false;
                        Close();
                    }
                }
                else
                {
                    MessageBox.Show($"登录失败：{responseText}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理登录失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private async Task GetUserInfoAsync(string accessToken)
        {
            try
            {
                var userInfoUrl = $"{CasdoorConfig.Endpoint}/api/userinfo";
                var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var userInfo = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var displayName = userInfo.GetProperty("displayName").GetString();
                    var userName = userInfo.GetProperty("name").GetString();

                    Application.Current.Properties["UserDisplayName"] = displayName;
                    Application.Current.Properties["UserName"] = userName;
                }
            }
            catch
            {
                // 获取用户信息失败不影响登录
            }
        }

        /// <summary>
        /// 设置登录成功结果
        /// </summary>
        public void SetLoginSuccess()
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 设置登录失败结果
        /// </summary>
        public void SetLoginFailure()
        {
            DialogResult = false;
        }
    }
}
