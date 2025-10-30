using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncEkpToCasdoor.UI.Models;
using SyncEkpToCasdoor.UI.Services;

namespace SyncEkpToCasdoor.UI.ViewModels;

/// <summary>
/// 数据查看 ViewModel
/// </summary>
public partial class DataViewViewModel : ObservableObject
{
    private readonly DataViewService _dataViewService;
    private readonly ConfigurationStorageService _configService;

    [ObservableProperty]
    private SyncConfiguration _configuration;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedDataSource = "EKP";

    [ObservableProperty]
    private string _selectedView = "组织";

    [ObservableProperty]
    private ObservableCollection<OrganizationInfo> _organizations = new();

    [ObservableProperty]
    private ObservableCollection<UserInfo> _users = new();

    [ObservableProperty]
    private ObservableCollection<OrganizationTreeNode> _organizationTree = new();

    [ObservableProperty]
    private OrganizationInfo? _selectedOrganization;

    [ObservableProperty]
    private UserInfo? _selectedUser;

    [ObservableProperty]
    private int _totalOrganizations;

    [ObservableProperty]
    private int _totalUsers;

    [ObservableProperty]
    private int _ekpOnlyCount;

    [ObservableProperty]
    private int _casdoorOnlyCount;

    [ObservableProperty]
    private int _syncedCount;

    [ObservableProperty]
    private string _filterStatus = "全部";

    public List<string> DataSources { get; } = new() { "EKP", "Casdoor", "比对" };
    public List<string> ViewTypes { get; } = new() { "组织", "用户" };
    public List<string> FilterStatuses { get; } = new() { "全部", "已同步", "仅在 EKP", "仅在 Casdoor" };

    private List<OrganizationInfo> _allOrganizations = new();
    private List<UserInfo> _allUsers = new();

    public DataViewViewModel()
    {
        _dataViewService = new DataViewService();
        _configService = new ConfigurationStorageService();
        _configuration = _configService.LoadConfiguration();
    }

    /// <summary>
    /// 加载数据
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!ValidateConfiguration())
        {
            MessageBox.Show("配置不完整，请先在【配置管理】页面完成配置。",
                "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = "正在加载数据...";

        try
        {
            if (SelectedView == "组织")
            {
                await LoadOrganizationsAsync();
            }
            else
            {
                await LoadUsersAsync();
            }

            StatusMessage = "数据加载完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            
            // 记录详细错误到日志文件和控制台
            var errorMessage = $"加载数据失败:\n\n{ex.Message}\n\n详细信息:\n{ex}";
            LogError(ex, "LoadDataAsync");
            
            // 显示可复制的错误窗口
            try
            {
                var logPath = GetErrorLogPath();
                var errorWindow = new ErrorLogWindow(errorMessage, logPath);
                errorWindow.ShowDialog();
            }
            catch
            {
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOrganizationsAsync()
    {
        _allOrganizations.Clear();
        Organizations.Clear();

        if (SelectedDataSource == "EKP")
        {
            StatusMessage = "正在从 EKP 加载组织...";
            _allOrganizations = await _dataViewService.GetEkpOrganizationsAsync(Configuration);
        }
        else if (SelectedDataSource == "Casdoor")
        {
            StatusMessage = "正在从 Casdoor 加载组织...";
            _allOrganizations = await _dataViewService.GetCasdoorOrganizationsAsync(Configuration);
        }
        else // 比对
        {
            StatusMessage = "正在比对 EKP 和 Casdoor 的组织数据...";
            var (ekpOnly, casdoorOnly, both) = await _dataViewService.CompareOrganizationsAsync(Configuration);
            
            _allOrganizations = ekpOnly.Concat(casdoorOnly).Concat(both).ToList();
            
            EkpOnlyCount = ekpOnly.Count;
            CasdoorOnlyCount = casdoorOnly.Count;
            SyncedCount = both.Count;
        }

        TotalOrganizations = _allOrganizations.Count;
        ApplyFilter();
    BuildOrganizationTree();
    }

    private async Task LoadUsersAsync()
    {
        _allUsers.Clear();
        Users.Clear();

        var orgId = SelectedOrganization?.Id;

        if (SelectedDataSource == "EKP")
        {
            StatusMessage = orgId != null 
                ? $"正在从 EKP 加载组织【{SelectedOrganization?.Name}】的用户..."
                : "正在从 EKP 加载所有用户...";
            _allUsers = await _dataViewService.GetEkpUsersAsync(Configuration, orgId);
        }
        else if (SelectedDataSource == "Casdoor")
        {
            StatusMessage = orgId != null
                ? $"正在从 Casdoor 加载组织【{SelectedOrganization?.Name}】的用户..."
                : "正在从 Casdoor 加载所有用户...";
            _allUsers = await _dataViewService.GetCasdoorUsersAsync(Configuration, orgId);
        }
        else // 比对
        {
            StatusMessage = "正在比对 EKP 和 Casdoor 的用户数据...";
            var (ekpOnly, casdoorOnly, both) = await _dataViewService.CompareUsersAsync(Configuration, orgId);
            
            _allUsers = ekpOnly.Concat(casdoorOnly).Concat(both).ToList();
            
            EkpOnlyCount = ekpOnly.Count;
            CasdoorOnlyCount = casdoorOnly.Count;
            SyncedCount = both.Count;
        }

        TotalUsers = _allUsers.Count;
        ApplyFilter();
    }

    /// <summary>
    /// 刷新数据
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 清空 Casdoor 数据 (仅测试环境使用)
    /// </summary>
    [RelayCommand]
    private async Task ClearCasdoorDataAsync()
    {
        var result = MessageBox.Show(
            "警告：此操作将删除 Casdoor 中的所有组织和用户数据（admin 用户除外）！\n\n" +
            "此功能仅用于测试环境调试。确定要继续吗？",
            "确认清空数据",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // 二次确认
        var confirmResult = MessageBox.Show(
            "最后确认：真的要清空所有 Casdoor 数据吗？\n\n此操作不可撤销！",
            "最终确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Exclamation);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        if (!ValidateConfiguration())
        {
            MessageBox.Show("配置不完整，无法执行清空操作。",
                "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = "正在清空 Casdoor 数据...";

        try
        {
            var (deletedUsers, deletedOrgs) = await _dataViewService.ClearCasdoorDataAsync(Configuration);
            
            StatusMessage = "清空完成";
            MessageBox.Show(
                $"清空完成！\n\n删除用户数：{deletedUsers}\n删除组织数：{deletedOrgs}",
                "清空成功",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 刷新显示
            if (SelectedDataSource == "Casdoor" || SelectedDataSource == "比对")
            {
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"清空失败: {ex.Message}";
            MessageBox.Show(
                $"清空 Casdoor 数据失败:\n\n{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        ApplyFilter();
    }

    /// <summary>
    /// 切换视图
    /// </summary>
    [RelayCommand]
    private async Task SwitchViewAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 切换数据源
    /// </summary>
    [RelayCommand]
    private async Task SwitchDataSourceAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 查看组织用户
    /// </summary>
    [RelayCommand]
    private async Task ViewOrganizationUsersAsync()
    {
        if (SelectedOrganization == null) return;

        SelectedView = "用户";
        await LoadUsersAsync();
    }

    /// <summary>
    /// 导出数据
    /// </summary>
    [RelayCommand]
    private void ExportData()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"数据导出_{SelectedDataSource}_{SelectedView}_{timestamp}.csv";
            var filepath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);

            if (SelectedView == "组织")
            {
                ExportOrganizations(filepath);
            }
            else
            {
                ExportUsers(filepath);
            }

            MessageBox.Show($"数据已导出到:\n{filepath}",
                "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败:\n\n{ex.Message}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportOrganizations(string filepath)
    {
        var lines = new List<string>
        {
            "ID,名称,显示名称,类型,成员数量,是否启用,数据来源,同步状态"
        };

        foreach (var org in Organizations)
        {
            lines.Add($"\"{org.Id}\",\"{org.Name}\",\"{org.DisplayName}\",\"{org.Type}\"," +
                     $"{org.MemberCount},{org.IsEnabled},\"{org.Source}\",\"{org.SyncStatus}\"");
        }

        System.IO.File.WriteAllLines(filepath, lines, System.Text.Encoding.UTF8);
    }

    private void ExportUsers(string filepath)
    {
        var lines = new List<string>
        {
            "ID,用户名,显示名称,邮箱,手机,组织,是否启用,数据来源,同步状态"
        };

        foreach (var user in Users)
        {
            lines.Add($"\"{user.Id}\",\"{user.Name}\",\"{user.DisplayName}\",\"{user.Email}\"," +
                     $"\"{user.Phone}\",\"{user.OrganizationName}\",{user.IsEnabled}," +
                     $"\"{user.Source}\",\"{user.SyncStatus}\"");
        }

        System.IO.File.WriteAllLines(filepath, lines, System.Text.Encoding.UTF8);
    }

    private void ApplyFilter()
    {
        if (SelectedView == "组织")
        {
            var filtered = _allOrganizations.AsEnumerable();

            // 状态过滤
            if (FilterStatus != "全部")
            {
                filtered = filtered.Where(o => o.SyncStatus == FilterStatus);
            }

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(o =>
                    o.Name.ToLower().Contains(search) ||
                    o.DisplayName.ToLower().Contains(search) ||
                    (o.Type?.ToLower().Contains(search) ?? false));
            }

            Organizations.Clear();
            foreach (var org in filtered.OrderBy(o => o.Order).ThenBy(o => o.Name))
            {
                Organizations.Add(org);
            }
        }
        else
        {
            var filtered = _allUsers.AsEnumerable();

            // 状态过滤
            if (FilterStatus != "全部")
            {
                filtered = filtered.Where(u => u.SyncStatus == FilterStatus);
            }

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(u =>
                    u.Name.ToLower().Contains(search) ||
                    u.DisplayName.ToLower().Contains(search) ||
                    (u.Email?.ToLower().Contains(search) ?? false) ||
                    (u.Phone?.Contains(search) ?? false));
            }

            Users.Clear();
            foreach (var user in filtered.OrderBy(u => u.DisplayName))
            {
                Users.Add(user);
            }
        }

        StatusMessage = SelectedView == "组织" 
            ? $"显示 {Organizations.Count} / {TotalOrganizations} 个组织"
            : $"显示 {Users.Count} / {TotalUsers} 个用户";
    }

    partial void OnFilterStatusChanged(string value)
    {
        ApplyFilter();
    }

    private bool ValidateConfiguration()
    {
        return !string.IsNullOrWhiteSpace(Configuration.EkpServer) &&
               !string.IsNullOrWhiteSpace(Configuration.EkpDatabase) &&
               !string.IsNullOrWhiteSpace(Configuration.CasdoorEndpoint);
    }

    /// <summary>
    /// 构建组织树形结构
    /// </summary>
    private void BuildOrganizationTree()
    {
        OrganizationTree.Clear();

        if (_allOrganizations.Count == 0)
        {
            return;
        }

        // 创建所有节点的字典
        var nodeDict = new Dictionary<string, OrganizationTreeNode>();
        foreach (var org in _allOrganizations)
        {
            var node = OrganizationTreeNode.FromOrganization(org);
            nodeDict[org.Id] = node;
        }

        // 构建父子关系
        var rootNodes = new List<OrganizationTreeNode>();
        foreach (var node in nodeDict.Values)
        {
            if (string.IsNullOrWhiteSpace(node.ParentId))
            {
                // 根节点
                rootNodes.Add(node);
                node.Level = 0;
            }
            else if (nodeDict.TryGetValue(node.ParentId, out var parentNode))
            {
                // 有父节点
                parentNode.Children.Add(node);
                node.Level = parentNode.Level + 1;
            }
            else
            {
                // 父节点不存在,作为根节点
                rootNodes.Add(node);
                node.Level = 0;
            }
        }

        // 按名称排序并添加到树
        foreach (var node in rootNodes.OrderBy(n => n.Name))
        {
            SortChildren(node);
            OrganizationTree.Add(node);
        }
    }

    /// <summary>
    /// 递归排序子节点
    /// </summary>
    private void SortChildren(OrganizationTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        var sortedChildren = node.Children.OrderBy(n => n.Name).ToList();
        node.Children.Clear();
        foreach (var child in sortedChildren)
        {
            SortChildren(child);
            node.Children.Add(child);
        }
    }

    /// <summary>
    /// 记录错误到日志文件和控制台
    /// </summary>
    private void LogError(Exception exception, string methodName)
    {
        try
        {
            var logPath = GetErrorLogPath();
            var logEntry = $"""
                ========================================
                时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                方法: DataViewViewModel.{methodName}
                错误类型: {exception.GetType().FullName}
                错误消息: {exception.Message}
                堆栈跟踪:
                {exception.StackTrace}
                
                完整异常:
                {exception}
                ========================================
                
                """;

            // 输出到控制台
            Console.WriteLine("========== ViewModel 错误 ==========");
            Console.WriteLine(logEntry);
            Console.WriteLine("===================================");

            // 写入日志文件
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? "");
            System.IO.File.AppendAllText(logPath, logEntry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"日志记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取错误日志文件路径
    /// </summary>
    private string GetErrorLogPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var logDir = System.IO.Path.Combine(baseDir, "logs");
        return System.IO.Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd}.log");
    }
}
