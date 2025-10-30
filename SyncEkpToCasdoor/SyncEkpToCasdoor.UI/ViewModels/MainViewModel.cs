using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncEkpToCasdoor.UI.Models;
using SyncEkpToCasdoor.UI.Services;

namespace SyncEkpToCasdoor.UI.ViewModels;

/// <summary>
/// 主窗口 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ConfigurationStorageService _configService;
    private readonly ConnectionTestService _testService;

    [ObservableProperty]
    private SyncConfiguration _configuration;

    [ObservableProperty]
    private SyncExecutionViewModel _syncExecutionViewModel;

    [ObservableProperty]
    private DataViewViewModel _dataViewViewModel;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    public MainViewModel()
    {
        _configService = new ConfigurationStorageService();
        _testService = new ConnectionTestService();
        
        // 加载已保存的配置
        _configuration = _configService.LoadConfiguration();
        
        // 初始化同步执行 ViewModel，并共享配置
        _syncExecutionViewModel = new SyncExecutionViewModel();
        _syncExecutionViewModel.Configuration = _configuration;
        
        // 初始化数据查看 ViewModel，并共享配置
        _dataViewViewModel = new DataViewViewModel();
        _dataViewViewModel.Configuration = _configuration;
        
        AddLog("应用程序已启动");
        if (_configService.ConfigurationExists())
        {
            AddLog("已加载保存的配置");
        }
        else
        {
            AddLog("未找到已保存的配置，使用默认值");
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        try
        {
            _configService.SaveConfiguration(Configuration);
            StatusMessage = "配置已保存";
            AddLog("配置已保存到本地");
            
            // 显示成功提示
            await Task.Delay(2000);
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试 EKP 数据库连接
    /// </summary>
    [RelayCommand]
    private async Task TestEkpConnectionAsync()
    {
        if (IsTesting) return;
        
        IsTesting = true;
        StatusMessage = "正在测试 EKP 数据库连接...";
        AddLog("开始测试 EKP 数据库连接");

        try
        {
            var result = await _testService.TestEkpDatabaseAsync(Configuration);
            
            if (result.Success)
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接成功", MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog($"✓ EKP 数据库连接成功 ({result.Duration.TotalMilliseconds}ms)");
            }
            else
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"✗ EKP 数据库连接失败: {result.Message}");
            }
            
            StatusMessage = result.Success ? "EKP 连接成功" : "EKP 连接失败";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"测试过程发生异常:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 测试异常: {ex.Message}");
            StatusMessage = "测试失败";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 测试 Casdoor API 连接
    /// </summary>
    [RelayCommand]
    private async Task TestCasdoorApiAsync()
    {
        if (IsTesting) return;
        
        IsTesting = true;
        StatusMessage = "正在测试 Casdoor API 连接...";
        AddLog("开始测试 Casdoor API 连接");

        try
        {
            var result = await _testService.TestCasdoorApiAsync(Configuration);
            
            if (result.Success)
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接成功", MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog($"✓ Casdoor API 连接成功 ({result.Duration.TotalMilliseconds}ms)");
            }
            else
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"✗ Casdoor API 连接失败: {result.Message}");
            }
            
            StatusMessage = result.Success ? "Casdoor API 连接成功" : "Casdoor API 连接失败";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"测试过程发生异常:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 测试异常: {ex.Message}");
            StatusMessage = "测试失败";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 测试 Casdoor 数据库连接
    /// </summary>
    [RelayCommand]
    private async Task TestCasdoorDbAsync()
    {
        if (IsTesting) return;
        
        IsTesting = true;
        StatusMessage = "正在测试 Casdoor 数据库连接...";
        AddLog("开始测试 Casdoor 数据库连接");

        try
        {
            var result = await _testService.TestCasdoorDatabaseAsync(Configuration);
            
            if (result.Success)
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接成功", MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog($"✓ Casdoor 数据库连接成功 ({result.Duration.TotalMilliseconds}ms)");
            }
            else
            {
                MessageBox.Show(result.Message + "\n\n" + result.Details, 
                    "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"✗ Casdoor 数据库连接失败: {result.Message}");
            }
            
            StatusMessage = result.Success ? "Casdoor 数据库连接成功" : "Casdoor 数据库连接失败";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"测试过程发生异常:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 测试异常: {ex.Message}");
            StatusMessage = "测试失败";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 测试所有连接
    /// </summary>
    [RelayCommand]
    private async Task TestAllConnectionsAsync()
    {
        if (IsTesting) return;
        
        AddLog("========== 开始全面连接测试 ==========");
        
        await TestEkpConnectionAsync();
        await Task.Delay(500);
        
        await TestCasdoorApiAsync();
        await Task.Delay(500);
        
        await TestCasdoorDbAsync();
        
        AddLog("========== 连接测试完成 ==========");
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    [RelayCommand]
    private void ValidateConfiguration()
    {
        AddLog("开始验证配置...");
        
        var result = _testService.ValidateConfiguration(Configuration);
        
        if (result.IsValid)
        {
            MessageBox.Show("配置验证通过！\n\n" + 
                (result.Warnings.Count > 0 ? "警告:\n" + string.Join("\n", result.Warnings) : "所有必需项已填写"), 
                "验证成功", MessageBoxButton.OK, MessageBoxImage.Information);
            AddLog("✓ 配置验证通过");
        }
        else
        {
            var message = "配置验证失败!\n\n错误:\n" + string.Join("\n", result.Errors);
            if (result.Warnings.Count > 0)
                message += "\n\n警告:\n" + string.Join("\n", result.Warnings);
            
            MessageBox.Show(message, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            AddLog($"✗ 配置验证失败: {result.Errors.Count} 个错误");
        }
    }

    /// <summary>
    /// 检查并创建 EKP 视图
    /// </summary>
    [RelayCommand]
    private async Task SetupEkpViewsAsync()
    {
        if (IsTesting) return;
        
        IsTesting = true;
        StatusMessage = "正在检查 EKP 视图...";
        AddLog("开始检查并创建 EKP 数据库视图");

        try
        {
            var viewSetupService = new EkpViewSetupService();
            var result = await viewSetupService.EnsureViewsExistAsync(Configuration);
            
            if (result.Success)
            {
                MessageBox.Show(result.Details, "视图设置成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog($"✓ EKP 视图设置成功");
            }
            else
            {
                MessageBox.Show($"{result.Message}\n\n{result.Details}", 
                    "视图设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"✗ EKP 视图设置失败: {result.Message}");
            }
            
            StatusMessage = result.Success ? "EKP 视图准备就绪" : "视图设置失败";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"视图设置过程发生异常:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 视图设置异常: {ex.Message}");
            StatusMessage = "设置失败";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 导出视图创建脚本
    /// </summary>
    [RelayCommand]
    private void ExportViewScript()
    {
        try
        {
            var viewSetupService = new EkpViewSetupService();
            var viewName = string.IsNullOrWhiteSpace(Configuration.UserGroupView) 
                ? "vw_user_group_membership" 
                : Configuration.UserGroupView;
            
            var script = viewSetupService.GetViewCreationScript(viewName);
            var filename = $"create_ekp_views_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
            var filepath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
            
            System.IO.File.WriteAllText(filepath, script, System.Text.Encoding.UTF8);
            
            MessageBox.Show($"视图创建脚本已导出到:\n{filepath}\n\n您可以手动在 SQL Server Management Studio 中执行此脚本。", 
                "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            AddLog($"✓ 视图脚本已导出: {filename}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出脚本失败:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 导出脚本失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取数据预览
    /// </summary>
    [RelayCommand]
    private async Task GetDataPreviewAsync()
    {
        if (IsTesting) return;
        
        IsTesting = true;
        StatusMessage = "正在获取数据预览...";
        AddLog("开始获取 EKP 数据预览");

        try
        {
            var preview = await _testService.GetEkpDataPreviewAsync(Configuration);
            
            var message = $"组织统计:\n" +
                         $"  总数: {preview.TotalOrganizations}\n" +
                         $"  根组织: {preview.RootOrganizations}\n\n" +
                         $"用户统计:\n" +
                         $"  总数: {preview.TotalUsers}\n\n" +
                         $"示例组织:\n";
            
            foreach (var org in preview.SampleOrganizations)
            {
                message += $"  - {org.DisplayName} ({org.Extra})\n";
            }
            
            MessageBox.Show(message, "数据预览", MessageBoxButton.OK, MessageBoxImage.Information);
            AddLog($"✓ 数据预览获取成功: {preview.TotalOrganizations} 个组织, {preview.TotalUsers} 个用户");
            StatusMessage = "数据预览获取成功";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"获取数据预览失败:\n{ex.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"✗ 获取数据预览失败: {ex.Message}");
            StatusMessage = "获取失败";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 清除日志
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        LogMessages.Clear();
        AddLog("日志已清除");
    }

    /// <summary>
    /// 添加日志
    /// </summary>
    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogMessages.Insert(0, $"[{timestamp}] {message}");
        
        // 限制日志条数
        while (LogMessages.Count > 1000)
        {
            LogMessages.RemoveAt(LogMessages.Count - 1);
        }
    }
}
