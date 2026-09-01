using System;

namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditProgress
    {
        public int CompletedRuleCount { get; }
        public int TotalRuleCount { get; }
        public string CurrentRuleId { get; }
        public bool IsComplete => string.IsNullOrEmpty(CurrentRuleId);

        internal AuditProgress(
            int completedRuleCount,
            int totalRuleCount,
            string currentRuleId)
        {
            if (completedRuleCount < 0 || completedRuleCount > totalRuleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(completedRuleCount));
            }

            CompletedRuleCount = completedRuleCount;
            TotalRuleCount = totalRuleCount;
            CurrentRuleId = currentRuleId ?? string.Empty;
        }
    }
}
