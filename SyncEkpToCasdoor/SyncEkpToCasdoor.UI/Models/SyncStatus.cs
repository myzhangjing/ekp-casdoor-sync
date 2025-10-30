using System;

namespace SyncEkpToCasdoor.UI.Models;

/// <summary>
/// 同步状态
/// </summary>
public class SyncStatus
{
    public bool IsRunning { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalOrganizations { get; set; }
    public int ProcessedOrganizations { get; set; }
    public int TotalUsers { get; set; }
    public int ProcessedUsers { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string CurrentOperation { get; set; } = "";
    public double Progress => TotalOrganizations + TotalUsers > 0 
        ? (double)(ProcessedOrganizations + ProcessedUsers) / (TotalOrganizations + TotalUsers) * 100 
        : 0;

    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : IsRunning && StartTime.HasValue 
            ? DateTime.Now - StartTime.Value 
            : null;
}

/// <summary>
/// 连接测试结果
/// </summary>
public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string Summary => IsValid 
        ? "验证通过" 
        : $"发现 {Errors.Count} 个错误, {Warnings.Count} 个警告";
}
