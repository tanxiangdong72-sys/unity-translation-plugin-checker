namespace Yxwm.LocalizationAuditor
{
    public enum AuditSeverity
    {
        Error = 0,
        Warning = 1,
        NotVerified = 2
    }

    public enum AuditIssueState
    {
        Open = 0,
        Ignored = 1
    }

    public enum AuditRunStatus
    {
        Completed = 0,
        Cancelled = 1,
        Failed = 2
    }
}
