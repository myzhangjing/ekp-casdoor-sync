using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncEkpToCasdoor.UI.Models;

/// <summary>
/// 组织树形节点
/// </summary>
public partial class OrganizationTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string? _parentId;

    [ObservableProperty]
    private int _memberCount;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string _syncStatus = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ObservableCollection<OrganizationTreeNode> _children = new();

    /// <summary>
    /// 原始组织信息
    /// </summary>
    public OrganizationInfo? OriginalOrganization { get; set; }

    /// <summary>
    /// 层级
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 从组织信息创建树节点
    /// </summary>
    public static OrganizationTreeNode FromOrganization(OrganizationInfo org)
    {
        return new OrganizationTreeNode
        {
            Id = org.Id,
            Name = org.Name,
            DisplayName = org.DisplayName,
            Type = org.Type,
            ParentId = org.ParentId,
            MemberCount = org.MemberCount,
            IsEnabled = org.IsEnabled,
            Source = org.Source,
            SyncStatus = org.SyncStatus,
            OriginalOrganization = org
        };
    }
}
