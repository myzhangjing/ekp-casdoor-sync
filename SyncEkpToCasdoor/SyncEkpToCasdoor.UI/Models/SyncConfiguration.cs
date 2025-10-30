using System;
using System.ComponentModel;

namespace SyncEkpToCasdoor.UI.Models;

/// <summary>
/// 同步配置模型
/// </summary>
public class SyncConfiguration : INotifyPropertyChanged
{
    // EKP 数据库配置
    private string _ekpServer = "172.16.10.110";
    private string _ekpPort = "1433";
    private string _ekpDatabase = "ekp";
    private string _ekpUsername = "sa";
    private string _ekpPassword = "";

    // Casdoor API 配置
    private string _casdoorEndpoint = "http://sso.fzcsps.com";
    private string _casdoorClientId = "";
    private string _casdoorClientSecret = "";
    private string _casdoorOwner = "fzswjtOrganization";

    // Casdoor 数据库配置（可选，用于故障排查）
    private string _casdoorDbHost = "172.16.10.110";
    private string _casdoorDbPort = "3306";
    private string _casdoorDbUser = "root";
    private string _casdoorDbPassword = "";
    private string _casdoorDbName = "casdoor";

    // 同步规则
    private bool _syncOrganizations = true;
    private bool _syncUsers = true;
    private bool _syncPasswords = true;
    private string _orgTypeFilter = "1,2"; // 1=公司, 2=部门
    private string _userGroupView = "vw_user_group_membership";

    // 调度配置
    private bool _enableSchedule = false;
    private ScheduleMode _scheduleMode = ScheduleMode.Daily;
    private TimeSpan _dailyTime = new TimeSpan(2, 0, 0); // 凌晨2点
    private int _intervalHours = 1;

    // 其他配置
    private string _syncStateFile = "sync_state.json";
    private string _logDirectory = "logs";
    private int _logRetentionDays = 30;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region EKP 数据库配置

    public string EkpServer
    {
        get => _ekpServer;
        set { _ekpServer = value; OnPropertyChanged(nameof(EkpServer)); OnPropertyChanged(nameof(EkpConnectionString)); }
    }

    public string EkpPort
    {
        get => _ekpPort;
        set { _ekpPort = value; OnPropertyChanged(nameof(EkpPort)); OnPropertyChanged(nameof(EkpConnectionString)); }
    }

    public string EkpDatabase
    {
        get => _ekpDatabase;
        set { _ekpDatabase = value; OnPropertyChanged(nameof(EkpDatabase)); OnPropertyChanged(nameof(EkpConnectionString)); }
    }

    public string EkpUsername
    {
        get => _ekpUsername;
        set { _ekpUsername = value; OnPropertyChanged(nameof(EkpUsername)); OnPropertyChanged(nameof(EkpConnectionString)); }
    }

    public string EkpPassword
    {
        get => _ekpPassword;
        set { _ekpPassword = value; OnPropertyChanged(nameof(EkpPassword)); OnPropertyChanged(nameof(EkpConnectionString)); }
    }

    public string EkpConnectionString => 
        $"Server={EkpServer},{EkpPort};Database={EkpDatabase};User Id={EkpUsername};Password={EkpPassword};TrustServerCertificate=True;";

    #endregion

    #region Casdoor API 配置

    public string CasdoorEndpoint
    {
        get => _casdoorEndpoint;
        set { _casdoorEndpoint = value; OnPropertyChanged(nameof(CasdoorEndpoint)); }
    }

    public string CasdoorClientId
    {
        get => _casdoorClientId;
        set { _casdoorClientId = value; OnPropertyChanged(nameof(CasdoorClientId)); }
    }

    public string CasdoorClientSecret
    {
        get => _casdoorClientSecret;
        set { _casdoorClientSecret = value; OnPropertyChanged(nameof(CasdoorClientSecret)); }
    }

    public string CasdoorOwner
    {
        get => _casdoorOwner;
        set { _casdoorOwner = value; OnPropertyChanged(nameof(CasdoorOwner)); }
    }

    #endregion

    #region Casdoor 数据库配置

    public string CasdoorDbHost
    {
        get => _casdoorDbHost;
        set { _casdoorDbHost = value; OnPropertyChanged(nameof(CasdoorDbHost)); OnPropertyChanged(nameof(CasdoorDbConnectionString)); }
    }

    public string CasdoorDbPort
    {
        get => _casdoorDbPort;
        set { _casdoorDbPort = value; OnPropertyChanged(nameof(CasdoorDbPort)); OnPropertyChanged(nameof(CasdoorDbConnectionString)); }
    }

    public string CasdoorDbUser
    {
        get => _casdoorDbUser;
        set { _casdoorDbUser = value; OnPropertyChanged(nameof(CasdoorDbUser)); OnPropertyChanged(nameof(CasdoorDbConnectionString)); }
    }

    public string CasdoorDbPassword
    {
        get => _casdoorDbPassword;
        set { _casdoorDbPassword = value; OnPropertyChanged(nameof(CasdoorDbPassword)); OnPropertyChanged(nameof(CasdoorDbConnectionString)); }
    }

    public string CasdoorDbName
    {
        get => _casdoorDbName;
        set { _casdoorDbName = value; OnPropertyChanged(nameof(CasdoorDbName)); OnPropertyChanged(nameof(CasdoorDbConnectionString)); }
    }

    public string CasdoorDbConnectionString => 
        $"Server={CasdoorDbHost};Port={CasdoorDbPort};Database={CasdoorDbName};Uid={CasdoorDbUser};Pwd={CasdoorDbPassword};";

    #endregion

    #region 同步规则

    public bool SyncOrganizations
    {
        get => _syncOrganizations;
        set { _syncOrganizations = value; OnPropertyChanged(nameof(SyncOrganizations)); }
    }

    public bool SyncUsers
    {
        get => _syncUsers;
        set { _syncUsers = value; OnPropertyChanged(nameof(SyncUsers)); }
    }

    public bool SyncPasswords
    {
        get => _syncPasswords;
        set { _syncPasswords = value; OnPropertyChanged(nameof(SyncPasswords)); }
    }

    public string OrgTypeFilter
    {
        get => _orgTypeFilter;
        set { _orgTypeFilter = value; OnPropertyChanged(nameof(OrgTypeFilter)); }
    }

    public string UserGroupView
    {
        get => _userGroupView;
        set { _userGroupView = value; OnPropertyChanged(nameof(UserGroupView)); }
    }

    #endregion

    #region 调度配置

    public bool EnableSchedule
    {
        get => _enableSchedule;
        set { _enableSchedule = value; OnPropertyChanged(nameof(EnableSchedule)); }
    }

    public ScheduleMode ScheduleMode
    {
        get => _scheduleMode;
        set { _scheduleMode = value; OnPropertyChanged(nameof(ScheduleMode)); }
    }

    public TimeSpan DailyTime
    {
        get => _dailyTime;
        set { _dailyTime = value; OnPropertyChanged(nameof(DailyTime)); }
    }

    public int IntervalHours
    {
        get => _intervalHours;
        set { _intervalHours = value; OnPropertyChanged(nameof(IntervalHours)); }
    }

    #endregion

    #region 其他配置

    public string SyncStateFile
    {
        get => _syncStateFile;
        set { _syncStateFile = value; OnPropertyChanged(nameof(SyncStateFile)); }
    }

    public string LogDirectory
    {
        get => _logDirectory;
        set { _logDirectory = value; OnPropertyChanged(nameof(LogDirectory)); }
    }

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set { _logRetentionDays = value; OnPropertyChanged(nameof(LogRetentionDays)); }
    }

    #endregion

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 克隆配置对象
    /// </summary>
    public SyncConfiguration Clone()
    {
        return (SyncConfiguration)this.MemberwiseClone();
    }
}

/// <summary>
/// 调度模式
/// </summary>
public enum ScheduleMode
{
    [Description("每日定时")]
    Daily,

    [Description("间隔时间")]
    Interval,

    [Description("仅手动")]
    Manual
}
