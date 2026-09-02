using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    internal enum AuditIssueStateFilter
    {
        Open = 0,
        Ignored = 1,
        All = 2
    }

    // 报告列表筛选条件，null severity 表示全部严重级别。
    internal readonly struct AuditIssueFilter : IEquatable<AuditIssueFilter>
    {
        public static AuditIssueFilter All =>
            new AuditIssueFilter(null, AuditIssueStateFilter.All);
        public static AuditIssueFilter Open =>
            new AuditIssueFilter(null, AuditIssueStateFilter.Open);

        public AuditSeverity? Severity { get; }
        public AuditIssueStateFilter State { get; }

        public AuditIssueFilter(
            AuditSeverity? severity,
            AuditIssueStateFilter state)
        {
            if (severity.HasValue &&
                !Enum.IsDefined(typeof(AuditSeverity), severity.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }

            if (!Enum.IsDefined(typeof(AuditIssueStateFilter), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            Severity = severity;
            State = state;
        }

        public bool Equals(AuditIssueFilter other)
        {
            return Severity == other.Severity && State == other.State;
        }

        public override bool Equals(object obj)
        {
            return obj is AuditIssueFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int?)Severity ?? -1) * 397 + (int)State;
            }
        }
    }

    internal sealed class AuditReportSummary
    {
        public int TotalIssueCount { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public int NotVerifiedCount { get; }
        public int IgnoredCount { get; }
        public int DiagnosticCount { get; }

        public AuditReportSummary(AuditReport report)
        {
            TotalIssueCount = report?.Issues.Count ?? 0;
            ErrorCount = report?.ErrorCount ?? 0;
            WarningCount = report?.WarningCount ?? 0;
            NotVerifiedCount = report?.NotVerifiedCount ?? 0;
            IgnoredCount = report?.IgnoredCount ?? 0;
            DiagnosticCount = report?.Diagnostics.Count ?? 0;
        }
    }

    // 为窗口提供可重复的报告筛选和摘要，不修改原始 AuditReport。
    internal sealed class AuditReportViewState
    {
        private readonly AuditReport _report;
        private readonly IReadOnlyList<AuditIssue> _defaultIssues;

        public AuditReportSummary Summary { get; }
        public AuditIssueFilter Filter { get; }
        public int FilteredIssueCount => _defaultIssues.Count;

        public AuditReportViewState(
            AuditReport report,
            AuditIssueFilter? filter = null)
        {
            _report = report;
            Filter = filter ?? AuditIssueFilter.Open;
            Summary = new AuditReportSummary(report);
            _defaultIssues = GetIssues(Filter);
        }

        public IReadOnlyList<AuditIssue> GetIssues(AuditIssueFilter filter)
        {
            if (_report == null)
            {
                return new ReadOnlyCollection<AuditIssue>(
                    new List<AuditIssue>());
            }

            var issues = _report.Issues
                .Where(issue => !filter.Severity.HasValue ||
                    issue.Severity == filter.Severity.Value)
                .Where(issue => filter.State == AuditIssueStateFilter.All ||
                    filter.State == AuditIssueStateFilter.Open &&
                    issue.State == AuditIssueState.Open ||
                    filter.State == AuditIssueStateFilter.Ignored &&
                    issue.State == AuditIssueState.Ignored)
                .ToList();
            return new ReadOnlyCollection<AuditIssue>(issues);
        }

        public static string GetStatusMessage(
            AuditReport report,
            AuditIssueFilter? filter = null)
        {
            if (report == null)
            {
                return "No audit has been run.";
            }

            var view = new AuditReportViewState(report, filter);
            if (view.Summary.TotalIssueCount == 0)
            {
                return "No issues found in the last audit.";
            }

            if (view.FilteredIssueCount == 0)
            {
                return "No issues match the current filters.";
            }

            return string.Empty;
        }
    }
}
