namespace SyncEkpToCasdoor.Web.Services;

public interface IConfigurationService
{
    Task<string> GetAdminPasswordAsync();
    Task SetAdminPasswordAsync(string password);
    
    Task<string> GetDomainAsync();
    Task SetDomainAsync(string domain);
    
    Task<string> GetProtocolAsync();
    Task SetProtocolAsync(string protocol);
    
    Task<string> GetAllowedUsersAsync();
    Task SetAllowedUsersAsync(string users);
    
    Task<Dictionary<string, string>> GetAllSettingsAsync();
    Task SaveSettingsAsync(Dictionary<string, string> settings);
}
