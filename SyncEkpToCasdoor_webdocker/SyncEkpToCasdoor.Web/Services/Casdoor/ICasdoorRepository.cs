using System;
using System.Collections.Generic;

namespace SyncEkpToCasdoor.Web.Services;

public interface ICasdoorRepository : IDisposable
{
    void UpsertGroup(EkpGroup g);
    void LoadCasdoorGroupMapping();
    void UpsertUser(EkpUser u, string owner, bool forceOwner, List<string>? groupIds);
    (string Owner, string Name)? ResolveUserKey(string owner, string userId);
    void RefreshEnforcer(string owner, string name);
    void PurgeExceptOwner(string owner);
    void ExportGroupHierarchy(string filePath);
}

public sealed class EkpGroup
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Type { get; set; } = "department"; // company | department
    public string DeptId { get; set; } = string.Empty; // 存到 Casdoor key 字段
    public bool IsEnabled { get; set; } = true;
}

public sealed class EkpUser
{
    public string Id { get; set; } = string.Empty;            // EKP 登录名
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Gender { get; set; } = string.Empty;       // Male | Female | ""
    public string Language { get; set; } = "zh";
    public string Type { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string PasswordMd5 { get; set; } = string.Empty;  // 明确约定为 MD5
}
