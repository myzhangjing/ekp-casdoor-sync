using System;

namespace SyncEkpToCasdoor.UI.Models;

/// <summary>
/// 用户信息
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 所属组织 ID
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// 所属组织名称
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// 用户类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否管理员
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedTime { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 数据来源 (EKP/Casdoor)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 同步状态
    /// </summary>
    public string SyncStatus { get; set; } = "未知";

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// 扩展信息
    /// </summary>
    public string? Description { get; set; }
}
