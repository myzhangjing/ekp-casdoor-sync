using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyncEkpToCasdoor.UI.Services;

/// <summary>
/// 配置存储服务 - 负责配置的加密保存和读取
/// </summary>
public class ConfigurationStorageService
{
    private readonly string _configFilePath;
    private readonly byte[] _entropy;

    public ConfigurationStorageService(string configFilePath = "sync_config.json")
    {
        _configFilePath = configFilePath;
        // 使用机器特定的熵值增加安全性
        _entropy = Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName);
    }

    /// <summary>
    /// 保存配置（敏感信息加密）
    /// </summary>
    public void SaveConfiguration(Models.SyncConfiguration config)
    {
        var configDto = new
        {
            // EKP 数据库
            EkpServer = config.EkpServer,
            EkpPort = config.EkpPort,
            EkpDatabase = config.EkpDatabase,
            EkpUsername = config.EkpUsername,
            EkpPassword = ProtectString(config.EkpPassword),

            // Casdoor API
            CasdoorEndpoint = config.CasdoorEndpoint,
            CasdoorClientId = config.CasdoorClientId,
            CasdoorClientSecret = ProtectString(config.CasdoorClientSecret),
            CasdoorOwner = config.CasdoorOwner,

            // Casdoor DB
            CasdoorDbHost = config.CasdoorDbHost,
            CasdoorDbPort = config.CasdoorDbPort,
            CasdoorDbUser = config.CasdoorDbUser,
            CasdoorDbPassword = ProtectString(config.CasdoorDbPassword),
            CasdoorDbName = config.CasdoorDbName,

            // 同步规则
            SyncOrganizations = config.SyncOrganizations,
            SyncUsers = config.SyncUsers,
            SyncPasswords = config.SyncPasswords,
            OrgTypeFilter = config.OrgTypeFilter,
            UserGroupView = config.UserGroupView,

            // 调度
            EnableSchedule = config.EnableSchedule,
            ScheduleMode = config.ScheduleMode.ToString(),
            DailyTime = config.DailyTime.ToString(),
            IntervalHours = config.IntervalHours,

            // 其他
            SyncStateFile = config.SyncStateFile,
            LogDirectory = config.LogDirectory,
            LogRetentionDays = config.LogRetentionDays
        };

        var json = JsonSerializer.Serialize(configDto, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        File.WriteAllText(_configFilePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// 加载配置（自动解密敏感信息）
    /// </summary>
    public Models.SyncConfiguration LoadConfiguration()
    {
        if (!File.Exists(_configFilePath))
        {
            return new Models.SyncConfiguration();
        }

        var json = File.ReadAllText(_configFilePath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var config = new Models.SyncConfiguration();

        // EKP 数据库
        if (root.TryGetProperty("EkpServer", out var prop)) config.EkpServer = prop.GetString() ?? "";
        if (root.TryGetProperty("EkpPort", out prop)) config.EkpPort = prop.GetString() ?? "1433";
        if (root.TryGetProperty("EkpDatabase", out prop)) config.EkpDatabase = prop.GetString() ?? "";
        if (root.TryGetProperty("EkpUsername", out prop)) config.EkpUsername = prop.GetString() ?? "";
        if (root.TryGetProperty("EkpPassword", out prop)) 
            config.EkpPassword = UnprotectString(prop.GetString() ?? "");

        // Casdoor API
        if (root.TryGetProperty("CasdoorEndpoint", out prop)) config.CasdoorEndpoint = prop.GetString() ?? "";
        if (root.TryGetProperty("CasdoorClientId", out prop)) config.CasdoorClientId = prop.GetString() ?? "";
        if (root.TryGetProperty("CasdoorClientSecret", out prop)) 
            config.CasdoorClientSecret = UnprotectString(prop.GetString() ?? "");
        if (root.TryGetProperty("CasdoorOwner", out prop)) config.CasdoorOwner = prop.GetString() ?? "";

        // Casdoor DB
        if (root.TryGetProperty("CasdoorDbHost", out prop)) config.CasdoorDbHost = prop.GetString() ?? "";
        if (root.TryGetProperty("CasdoorDbPort", out prop)) config.CasdoorDbPort = prop.GetString() ?? "3306";
        if (root.TryGetProperty("CasdoorDbUser", out prop)) config.CasdoorDbUser = prop.GetString() ?? "";
        if (root.TryGetProperty("CasdoorDbPassword", out prop)) 
            config.CasdoorDbPassword = UnprotectString(prop.GetString() ?? "");
        if (root.TryGetProperty("CasdoorDbName", out prop)) config.CasdoorDbName = prop.GetString() ?? "";

        // 同步规则
        if (root.TryGetProperty("SyncOrganizations", out prop)) config.SyncOrganizations = prop.GetBoolean();
        if (root.TryGetProperty("SyncUsers", out prop)) config.SyncUsers = prop.GetBoolean();
        if (root.TryGetProperty("SyncPasswords", out prop)) config.SyncPasswords = prop.GetBoolean();
        if (root.TryGetProperty("OrgTypeFilter", out prop)) config.OrgTypeFilter = prop.GetString() ?? "1,2";
        if (root.TryGetProperty("UserGroupView", out prop)) config.UserGroupView = prop.GetString() ?? "";

        // 调度
        if (root.TryGetProperty("EnableSchedule", out prop)) config.EnableSchedule = prop.GetBoolean();
        if (root.TryGetProperty("ScheduleMode", out prop) && 
            Enum.TryParse<Models.ScheduleMode>(prop.GetString(), out var mode))
            config.ScheduleMode = mode;
        if (root.TryGetProperty("DailyTime", out prop) && TimeSpan.TryParse(prop.GetString(), out var time))
            config.DailyTime = time;
        if (root.TryGetProperty("IntervalHours", out prop)) config.IntervalHours = prop.GetInt32();

        // 其他
        if (root.TryGetProperty("SyncStateFile", out prop)) config.SyncStateFile = prop.GetString() ?? "";
        if (root.TryGetProperty("LogDirectory", out prop)) config.LogDirectory = prop.GetString() ?? "";
        if (root.TryGetProperty("LogRetentionDays", out prop)) config.LogRetentionDays = prop.GetInt32();

        return config;
    }

    /// <summary>
    /// 加密字符串（使用 Windows DPAPI）
    /// </summary>
    private string ProtectString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return "";

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// 解密字符串
    /// </summary>
    private string UnprotectString(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return "";

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedText);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // 如果解密失败，返回空字符串（可能是配置损坏或换机器）
            return "";
        }
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    public bool ConfigurationExists() => File.Exists(_configFilePath);
}
