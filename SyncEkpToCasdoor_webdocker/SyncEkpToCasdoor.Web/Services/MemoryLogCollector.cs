using System.Collections.Concurrent;
using SyncEkpToCasdoor.Web.Services;

namespace SyncEkpToCasdoor.Web.Services;

/// <summary>
/// 内存日志收集器 - 用于Web UI显示
/// </summary>
public class MemoryLogCollector
{
    private static readonly ConcurrentQueue<SyncLog> _logs = new();
    private static readonly int MaxLogs = 1000;
    
    public static void AddLog(string level, string message, string? details = null)
    {
        _logs.Enqueue(new SyncLog
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details
        });
        
        // 保持最多1000条
        while (_logs.Count > MaxLogs)
        {
            _logs.TryDequeue(out _);
        }
    }
    
    public static List<SyncLog> GetLogs(int count = 100)
    {
        return _logs.Reverse().Take(count).Reverse().ToList();
    }
    
    public static void Clear()
    {
        _logs.Clear();
    }
}
