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
        };
    }
}