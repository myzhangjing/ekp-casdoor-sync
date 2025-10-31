using System.Text.Json.Serialization;

namespace SyncEkpToCasdoor.Web.Models;

public class CasdoorUser
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
    
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";
    
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "";
    
    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; set; }
}
