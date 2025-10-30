using System;

namespace SyncEkpToCasdoor.UI.Models;

/// <summary>
/// 组织信息
/// </summary>
public class OrganizationInfo
{
    /// <summary>
    /// 组织 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 组织名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 父组织 ID
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 组织类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedTime { get; set; }

    /// <summary>
    /// 成员数量
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// 数据来源 (EKP/Casdoor)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 同步状态
    /// </summary>
    public string SyncStatus { get; set; } = "未知";

    /// <summary>
    /// 扩展信息
    /// </summary>
    public string? Description { get; set; }
}
