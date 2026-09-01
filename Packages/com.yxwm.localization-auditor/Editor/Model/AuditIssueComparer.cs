using System;
using System.Collections.Generic;

namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditIssueComparer : IComparer<AuditIssue>
    {
        public static AuditIssueComparer Instance { get; } = new AuditIssueComparer();

        private AuditIssueComparer()
        {
        }

        public int Compare(AuditIssue left, AuditIssue right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var comparison = left.Severity.CompareTo(right.Severity);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.State.CompareTo(right.State);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.RuleId, right.RuleId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareLocation(left.Location, right.Location);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.Message, right.Message);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.FixSuggestion, right.FixSuggestion);
            if (comparison != 0)
            {
                return comparison;
            }

            return CompareCodePoints(left.MissingCodePoints, right.MissingCodePoints);
        }

        private static int CompareLocation(
            AuditIssueLocation left,
            AuditIssueLocation right)
        {
            var comparison = CompareOrdinal(left.AssetPath, right.AssetPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.ObjectPath, right.ObjectPath);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.LocaleCode, right.LocaleCode);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.TableName, right.TableName);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareOrdinal(left.Key, right.Key);
            if (comparison != 0)
            {
                return comparison;
            }

            return CompareOrdinal(left.FontAssetPath, right.FontAssetPath);
        }

        private static int CompareCodePoints(
            IReadOnlyList<int> left,
            IReadOnlyList<int> right)
        {
            var length = Math.Min(left.Count, right.Count);
            for (var index = 0; index < length; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Count.CompareTo(right.Count);
        }

        private static int CompareOrdinal(string left, string right)
        {
            return StringComparer.Ordinal.Compare(left, right);
        }
    }
}
