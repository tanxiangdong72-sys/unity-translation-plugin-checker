using System;

namespace Yxwm.LocalizationAuditor
{
    // 进度对象描述当前规则和已完成规则数，供 Editor UI 安全展示。
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
