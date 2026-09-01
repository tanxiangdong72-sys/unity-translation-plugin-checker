namespace Yxwm.LocalizationAuditor
{
    // 严重级别描述分析结论；问题是否被忽略由 AuditIssueState 单独表达。
    public enum AuditSeverity
    {
        Error = 0,
        Warning = 1,
        NotVerified = 2
    }

    // 忽略状态与严重级别分离，便于以后恢复问题或重新统计。
    public enum AuditIssueState
    {
        Open = 0,
        Ignored = 1
    }

    // 表示一次完整扫描的最终状态。
    public enum AuditRunStatus
    {
        Completed = 0,
        Cancelled = 1,
        Failed = 2
    }
}
