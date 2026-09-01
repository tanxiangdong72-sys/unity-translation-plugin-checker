using System;
using System.Collections.Generic;

namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditIssue
    {
        public string RuleId { get; }
        public AuditSeverity Severity { get; }
        public AuditIssueState State { get; }
        public string Message { get; }
        public string FixSuggestion { get; }
        public AuditIssueLocation Location { get; }
        public IReadOnlyList<int> MissingCodePoints { get; }

        public AuditIssue(
            string ruleId,
            AuditSeverity severity,
            string message,
            string fixSuggestion = null,
            AuditIssueLocation location = null,
            IEnumerable<int> missingCodePoints = null,
            AuditIssueState state = AuditIssueState.Open)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                throw new ArgumentException("Rule id is required.", nameof(ruleId));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            if (!Enum.IsDefined(typeof(AuditSeverity), severity))
            {
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown audit severity.");
            }

            if (!Enum.IsDefined(typeof(AuditIssueState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown issue state.");
            }

            RuleId = ruleId;
            Severity = severity;
            State = state;
            Message = message;
            FixSuggestion = fixSuggestion ?? string.Empty;
            Location = location ?? AuditIssueLocation.Empty;
            MissingCodePoints = SnapshotCodePoints(missingCodePoints);
        }

        private static IReadOnlyList<int> SnapshotCodePoints(IEnumerable<int> codePoints)
        {
            var distinctCodePoints = new HashSet<int>();
            if (codePoints != null)
            {
                foreach (var codePoint in codePoints)
                {
                    if (codePoint < 0 || codePoint > 0x10FFFF)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(codePoints),
                            codePoint,
                            "Unicode code points must be between U+0000 and U+10FFFF.");
                    }

                    distinctCodePoints.Add(codePoint);
                }
            }

            var sortedCodePoints = new List<int>(distinctCodePoints);
            sortedCodePoints.Sort();
            return Array.AsReadOnly(sortedCodePoints.ToArray());
        }
    }
}
