using System.Text.Json;

namespace SyncEkpToCasdoor.Web.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFile;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Production.json");
    }

    public async Task<string> GetAdminPasswordAsync()
    {
        var settings = await LoadConfigAsync();
        return settings.TryGetValue("AdminPassword", out var pwd) ? pwd : "sosy3080@sohu.com";
    }

    public async Task SetAdminPasswordAsync(string password)
    {
        await UpdateSettingAsync("AdminPassword", password);
    }

    public async Task<string> GetDomainAsync()
    {
        var settings = await LoadConfigAsync();
        return settings.TryGetValue("Domain", out var domain) ? domain : "syncas.fzcsps.com";
    }

    public async Task SetDomainAsync(string domain)
    {
        await UpdateSettingAsync("Domain", domain);
    }

    public async Task<string> GetProtocolAsync()
    {
        var settings = await LoadConfigAsync();
        return settings.TryGetValue("Protocol", out var protocol) ? protocol : "http";
    }

    public async Task SetProtocolAsync(string protocol)
    {
        await UpdateSettingAsync("Protocol", protocol);
    }

    public async Task<string> GetAllowedUsersAsync()
    {
        var settings = await LoadConfigAsync();
        return settings.TryGetValue("AllowedUsers", out var users) ? users : "";
    }

    public async Task SetAllowedUsersAsync(string users)
    {
        await UpdateSettingAsync("AllowedUsers", users);
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        return await LoadConfigAsync();
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        try
        {
            var json = await File.ReadAllTextAsync(_configFile);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "AppSettings")
                    {
                        writer.WritePropertyName("AppSettings");
                        writer.WriteStartObject();
                        foreach (var kvp in settings)
                        {
                            writer.WriteString(kvp.Key, kvp.Value);
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            await File.WriteAllTextAsync(_configFile, updatedJson);

            _logger.LogInformation("配置已保存到文件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
            throw;
        }
    }

    private async Task<Dictionary<string, string>> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configFile))
            {
                return new Dictionary<string, string>();
            }

            var json = await File.ReadAllTextAsync(_configFile);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("AppSettings", out var appSettings))
            {
                var settings = new Dictionary<string, string>();
                foreach (var property in appSettings.EnumerateObject())
                {
                    settings[property.Name] = property.Value.GetString() ?? "";
                }
                return settings;
            }

            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取配置失败");
            return new Dictionary<string, string>();
        }
    }

    private async Task UpdateSettingAsync(string key, string value)
    {
        var settings = await LoadConfigAsync();
        settings[key] = value;
        await SaveSettingsAsync(settings);
    }
}
