using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncEkpToCasdoor.UI.Services;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SyncEkpToCasdoor.UI.ViewModels
{
    /// <summary>
    /// 登录窗口 ViewModel
    /// </summary>
    public partial class LoginViewModel : ObservableObject
    {
        private readonly HttpListener? _httpListener;
        private readonly string _casdoorEndpoint;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly bool _useCustomUriScheme;
        private bool _isListening;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _showBrowserMessage;

        [ObservableProperty]
        private bool _showManualInput;

        [ObservableProperty]
        private bool _showLoginButton = true;

        [ObservableProperty]
        private bool _showManualInputToggle = true;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string? _authorizationUrl;

        [ObservableProperty]
        private string? _authorizationCode;

        public event EventHandler<string>? LoginSucceeded;

        /// <summary>
        /// 构造函数
        /// </summary>
        public LoginViewModel(string casdoorEndpoint, string clientId, string clientSecret, string? redirectUri = null)
        {
            _casdoorEndpoint = casdoorEndpoint;
            _clientId = clientId;
            _clientSecret = clientSecret;

            // 优先使用自定义 URI Scheme
            if (UriSchemeRegistrar.IsUriSchemeRegistered() || UriSchemeRegistrar.RegisterUriScheme())
            {
                _redirectUri = UriSchemeRegistrar.GetCallbackUri();
                _useCustomUriScheme = true;
                Debug.WriteLine($"使用自定义 URI Scheme: {_redirectUri}");
            }
            // 回退到提供的 redirectUri 或默认 localhost
            else if (!string.IsNullOrEmpty(redirectUri))
            {
                _redirectUri = redirectUri;
                _useCustomUriScheme = false;
                Debug.WriteLine($"使用提供的回调地址: {_redirectUri}");
            }
            else
            {
                _redirectUri = "http://localhost:9000/callback";
                _useCustomUriScheme = false;
                Debug.WriteLine($"使用默认 localhost 回调: {_redirectUri}");
            }

            // 构造授权 URL
            AuthorizationUrl = $"{_casdoorEndpoint}/login/oauth/authorize" +
                $"?client_id={_clientId}" +
                $"&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                $"&scope=read" +
                $"&state=casdoor";

            // 如果不使用自定义 URI Scheme，尝试启动 HTTP 监听器
            if (!_useCustomUriScheme && _redirectUri.StartsWith("http://localhost"))
            {
                try
                {
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add($"{_redirectUri}/");
                    Debug.WriteLine($"HttpListener 已准备就绪: {_redirectUri}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"无法启动 HttpListener: {ex.Message}");
                    // 如果无法启动监听器，用户需要手动输入授权码
                }
            }
        }

        /// <summary>
        /// 登录命令
        /// </summary>
        [RelayCommand]
        private async Task LoginAsync()
        {
            IsLoading = true;
            HasError = false;
            ShowLoginButton = false;
            ShowManualInputToggle = false;

            try
            {
                // 启动 HTTP 监听器（如果可用）
                if (_httpListener != null)
                {
                    _httpListener.Start();
                    _isListening = true;

                    // 在后台等待回调
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var context = await _httpListener.GetContextAsync();
                            var code = context.Request.QueryString["code"];
                            var state = context.Request.QueryString["state"];

                            // 返回成功页面给浏览器
                            var response = context.Response;
                            var responseString = "<html><body><h1>授权成功！</h1><p>您可以关闭此页面返回应用程序。</p></body></html>";
                            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            response.OutputStream.Close();

                            // 处理授权码
                            if (!string.IsNullOrEmpty(code))
                            {
                                await Application.Current.Dispatcher.InvokeAsync(async () =>
                                {
                                    await HandleAuthorizationCodeAsync(code);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"监听回调失败: {ex.Message}");
                        }
                        finally
                        {
                            _httpListener?.Stop();
                            _isListening = false;
                        }
                    });
                }

                // 打开浏览器
                Process.Start(new ProcessStartInfo
                {
                    FileName = AuthorizationUrl,
                    UseShellExecute = true
                });

                ShowBrowserMessage = true;

                // 30秒后如果还没有收到回调，显示手动输入选项
                await Task.Delay(30000);
                if (_isListening)
                {
                    ShowManualInputToggle = true;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"打开浏览器失败: {ex.Message}";
                ShowLoginButton = true;
                ShowManualInputToggle = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 显示手动输入命令
        /// </summary>
        [RelayCommand]
        private void ToggleManualInput()
        {
            ShowManualInput = true;
            ShowBrowserMessage = false;
            ShowManualInputToggle = false;

            // 停止监听
            if (_isListening && _httpListener != null)
            {
                _httpListener.Stop();
                _isListening = false;
            }
        }

        /// <summary>
        /// 使用授权码登录命令
        /// </summary>
        [RelayCommand]
        private async Task LoginWithCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(AuthorizationCode))
            {
                HasError = true;
                ErrorMessage = "请输入授权码";
                return;
            }

            await HandleAuthorizationCodeAsync(AuthorizationCode);
        }

        /// <summary>
        /// 处理授权码
        /// </summary>
        public async Task HandleAuthorizationCodeAsync(string code)
        {
            IsLoading = true;
            HasError = false;

            try
            {
                // 使用授权码换取访问令牌
                var tokenUrl = $"{_casdoorEndpoint}/api/login/oauth/access_token";
                using var httpClient = new HttpClient();

                var requestData = new
                {
                    grant_type = "authorization_code",
                    client_id = _clientId,
                    client_secret = _clientSecret,
                    code = code,
                    redirect_uri = _redirectUri
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(tokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody);
                    if (tokenResponse?.access_token != null)
                    {
                        // 登录成功
                        LoginSucceeded?.Invoke(this, tokenResponse.access_token);
                    }
                    else
                    {
                        throw new Exception("未能获取访问令牌");
                    }
                }
                else
                {
                    throw new Exception($"获取令牌失败: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"登录失败: {ex.Message}";
                ShowLoginButton = true;
                ShowManualInputToggle = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 令牌响应模型
        /// </summary>
        private class TokenResponse
        {
            public string? access_token { get; set; }
            public string? token_type { get; set; }
            public int expires_in { get; set; }
            public string? refresh_token { get; set; }
        }
    }
}
