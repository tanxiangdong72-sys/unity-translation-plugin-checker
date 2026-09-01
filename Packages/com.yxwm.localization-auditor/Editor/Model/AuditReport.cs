using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    // 报告是一次扫描的不可变结果快照，供 UI、测试和后续导出复用。
    public sealed class AuditReport
    {
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset FinishedAtUtc { get; }
        public AuditRunStatus Status { get; }
        public int ScannedAssetCount { get; }
        public IReadOnlyList<AuditIssue> Issues { get; }
        public IReadOnlyList<AuditDiagnostic> Diagnostics { get; }
        public TimeSpan Duration => FinishedAtUtc - StartedAtUtc;

        // 忽略的问题保留在 Issues 中，但不计入活动问题数量。
        public int ErrorCount => CountIssues(AuditSeverity.Error, AuditIssueState.Open);
        public int WarningCount => CountIssues(AuditSeverity.Warning, AuditIssueState.Open);
        public int NotVerifiedCount => CountIssues(AuditSeverity.NotVerified, AuditIssueState.Open);
        public int IgnoredCount => Issues.Count(issue => issue.State == AuditIssueState.Ignored);

        public AuditReport(
            DateTimeOffset startedAtUtc,
            DateTimeOffset finishedAtUtc,
            AuditRunStatus status,
            int scannedAssetCount,
            IEnumerable<AuditIssue> issues = null,
            IEnumerable<AuditDiagnostic> diagnostics = null)
        {
            if (finishedAtUtc < startedAtUtc)
            {
                throw new ArgumentException(
                    "Finished time cannot be earlier than started time.",
                    nameof(finishedAtUtc));
            }

            if (!Enum.IsDefined(typeof(AuditRunStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown run status.");
            }

            if (scannedAssetCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scannedAssetCount),
                    scannedAssetCount,
                    "Scanned asset count cannot be negative.");
            }

            StartedAtUtc = startedAtUtc;
            FinishedAtUtc = finishedAtUtc;
            Status = status;
            ScannedAssetCount = scannedAssetCount;
            Issues = SnapshotIssues(issues);
            Diagnostics = SnapshotDiagnostics(diagnostics);
        }

        private int CountIssues(AuditSeverity severity, AuditIssueState state)
        {
            return Issues.Count(issue =>
                issue.Severity == severity &&
                issue.State == state);
        }

        private static IReadOnlyList<AuditIssue> SnapshotIssues(
            IEnumerable<AuditIssue> issues)
        {
            // 复制并排序问题集合，避免规则完成顺序影响报告。
            var snapshot = issues == null
                ? new List<AuditIssue>()
                : new List<AuditIssue>(issues);

            if (snapshot.Any(issue => issue == null))
            {
                throw new ArgumentException(
                    "Reports cannot contain null issues.",
                    nameof(issues));
            }

            snapshot.Sort(AuditIssueComparer.Instance);
            return new ReadOnlyCollection<AuditIssue>(snapshot);
        }

        private static IReadOnlyList<AuditDiagnostic> SnapshotDiagnostics(
            IEnumerable<AuditDiagnostic> diagnostics)
        {
            // 诊断同样复制并排序，使日志和 UI 展示可重复。
            var snapshot = diagnostics == null
                ? new List<AuditDiagnostic>()
                : new List<AuditDiagnostic>(diagnostics);

            if (snapshot.Any(diagnostic => diagnostic == null))
            {
                throw new ArgumentException(
                    "Reports cannot contain null diagnostics.",
                    nameof(diagnostics));
            }

            snapshot.Sort(CompareDiagnostics);
            return new ReadOnlyCollection<AuditDiagnostic>(snapshot);
        }

        private static int CompareDiagnostics(
            AuditDiagnostic left,
            AuditDiagnostic right)
        {
            var comparison = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.ExceptionType, right.ExceptionType);
            if (comparison != 0)
            {
                return comparison;
            }

            return StringComparer.Ordinal.Compare(left.Message, right.Message);
        }
    }
}
