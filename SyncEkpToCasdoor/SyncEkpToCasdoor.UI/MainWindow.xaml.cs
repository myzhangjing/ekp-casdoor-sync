using System.Windows;
using SyncEkpToCasdoor.UI.ViewModels;

namespace SyncEkpToCasdoor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 绑定密码框（PasswordBox 不支持绑定，需要手动处理）
        var viewModel = (MainViewModel)DataContext;
        
        EkpPasswordBox.PasswordChanged += (s, e) => 
        {
            viewModel.Configuration.EkpPassword = EkpPasswordBox.Password;
        };
        
        CasdoorSecretBox.PasswordChanged += (s, e) =>
        {
            viewModel.Configuration.CasdoorClientSecret = CasdoorSecretBox.Password;
        };
        
        CasdoorDbPasswordBox.PasswordChanged += (s, e) =>
        {
            viewModel.Configuration.CasdoorDbPassword = CasdoorDbPasswordBox.Password;
        };
        
        // 加载时设置密码框的值
        Loaded += (s, e) =>
        {
            EkpPasswordBox.Password = viewModel.Configuration.EkpPassword;
            CasdoorSecretBox.Password = viewModel.Configuration.CasdoorClientSecret;
            CasdoorDbPasswordBox.Password = viewModel.Configuration.CasdoorDbPassword;
            
            // 显示当前登录用户
            UpdateUserInfo();
        };
    }

    /// <summary>
    /// 更新用户信息显示
    /// </summary>
    private void UpdateUserInfo()
    {
        var displayName = Application.Current.Properties["UserDisplayName"] as string;
        var userName = Application.Current.Properties["UserName"] as string;

        if (!string.IsNullOrEmpty(displayName))
        {
            UserDisplayNameText.Text = displayName;
        }
        else if (!string.IsNullOrEmpty(userName))
        {
            UserDisplayNameText.Text = userName;
        }
        else
        {
            UserDisplayNameText.Text = "已登录";
        }
    }
}