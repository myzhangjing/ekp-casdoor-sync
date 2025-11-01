using System;
using System.Collections.Generic;

namespace SyncEkpToCasdoor.Web.Models
{
    /// <summary>
    /// 同步日志模型
    /// </summary>
    public class SyncLog
    {
        /// <summary>
        /// 日志ID（唯一标识）
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 同步开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 同步结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 同步耗时（毫秒）
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// 同步类型：Scheduled(定时) | Manual(手动) | OnDemand(按需)
        /// </summary>
        public string SyncType { get; set; } = "Manual";

        /// <summary>
        /// 同步状态：Running | Success | Failed | PartialSuccess
        /// </summary>
        public string Status { get; set; } = "Running";

        /// <summary>
        /// 目标公司ID列表
        /// </summary>
        public List<string> CompanyIds { get; set; } = new();

        /// <summary>
        /// 同步结果统计
        /// </summary>
        public SyncStatistics Statistics { get; set; } = new();

        /// <summary>
        /// 错误信息（如有）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 详细日志条目
        /// </summary>
        public List<SyncLogEntry> Entries { get; set; } = new();

        /// <summary>
        /// 触发者（定时任务为 "System"，手动为用户名）
        /// </summary>
        public string TriggeredBy { get; set; } = "System";
    }

    /// <summary>
    /// 同步统计信息
    /// </summary>
    public class SyncStatistics
    {
        /// <summary>
        /// 同步的公司数量
        /// </summary>
        public int TotalCompanies { get; set; }

        /// <summary>
        /// 同步成功的公司数量
        /// </summary>
        public int SuccessfulCompanies { get; set; }

        /// <summary>
        /// 同步失败的公司数量
        /// </summary>
        public int FailedCompanies { get; set; }

        /// <summary>
        /// 同步的部门数量
        /// </summary>
        public int TotalDepartments { get; set; }

        /// <summary>
        /// 同步的用户数量
        /// </summary>
        public int TotalUsers { get; set; }

        /// <summary>
        /// 新增的组织数量
        /// </summary>
        public int NewOrganizations { get; set; }

        /// <summary>
        /// 更新的组织数量
        /// </summary>
        public int UpdatedOrganizations { get; set; }

        /// <summary>
        /// 新增的用户数量
        /// </summary>
        public int NewUsers { get; set; }

        /// <summary>
        /// 更新的用户数量
        /// </summary>
        public int UpdatedUsers { get; set; }

        /// <summary>
        /// 跳过的记录数量
        /// </summary>
        public int SkippedRecords { get; set; }
    }

    /// <summary>
    /// 同步日志条目（详细步骤）
    /// </summary>
    public class SyncLogEntry
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 日志级别：Info | Warning | Error
        /// </summary>
        public string Level { get; set; } = "Info";

        /// <summary>
        /// 公司ID（如果适用）
        /// </summary>
        public string? CompanyId { get; set; }

        /// <summary>
        /// 同步步骤：Start | SyncDepartments | SyncUsers | Complete | Error
        /// </summary>
        public string Step { get; set; } = "";

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// 详细信息（可选）
        /// </summary>
        public string? Details { get; set; }
    }
}
