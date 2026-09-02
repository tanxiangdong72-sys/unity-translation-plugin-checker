using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    internal enum LocalizationAuditorWindowRunState
    {
        NotRun = 0,
        Completed = 1,
        Cancelled = 2,
        Failed = 3
    }

    // 保存窗口需要的最小状态，避免测试依赖 GUI 绘制。
    internal sealed class LocalizationAuditorWindowState
    {
        public static readonly IReadOnlyList<string> DefaultRuleIds =
            new ReadOnlyCollection<string>(new[]
            {
                EmptyTranslationRule.RuleId,
                LocalizedStringReferenceRule.RuleId,
                StringTableCompletenessRule.RuleId,
                TmpFontCoverageRule.RuleId
            });

        private readonly Func<IEnumerable<string>, AuditTargetDiscoveryResult> _discoverTargets;
        private readonly Func<AuditRequest, AuditReport> _runAudit;
        private IReadOnlyList<AuditTarget> _targets =
            new ReadOnlyCollection<AuditTarget>(new List<AuditTarget>());

        public LocalizationAuditorWindowState(
            Func<IEnumerable<string>, AuditTargetDiscoveryResult> discoverTargets = null,
            Func<AuditRequest, AuditReport> runAudit = null)
        {
            _discoverTargets = discoverTargets ?? AuditTargetDiscovery.Discover;
            _runAudit = runAudit ?? RunWithDefaultRules;
            StatusMessage = "No audit targets. Refresh targets to begin.";
        }

        public IReadOnlyList<AuditTarget> Targets => _targets;
        public AuditRequest LastRequest { get; private set; }
        public AuditReport LastReport { get; private set; }
        public LocalizationAuditorWindowRunState RunState { get; private set; }
        public string StatusMessage { get; private set; }
        public int SceneTargetCount =>
            _targets.Count(target => target.Kind == AuditTargetKind.Scene);
        public int PrefabTargetCount =>
            _targets.Count(target => target.Kind == AuditTargetKind.Prefab);
        public int ErrorCount => LastReport?.ErrorCount ?? 0;
        public int WarningCount => LastReport?.WarningCount ?? 0;
        public int NotVerifiedCount => LastReport?.NotVerifiedCount ?? 0;
        public int DiagnosticCount => LastReport?.Diagnostics.Count ?? 0;

        public void RefreshTargets()
        {
            RefreshTargets(new[] { "Assets" });
        }

        public void RefreshTargets(IEnumerable<string> requestedPaths)
        {
            try
            {
                var result = _discoverTargets(requestedPaths);
                _targets = new ReadOnlyCollection<AuditTarget>(
                    (result?.Targets ?? Array.Empty<AuditTarget>()).ToList());
                LastRequest = null;
                LastReport = null;
                RunState = LocalizationAuditorWindowRunState.NotRun;
                var diagnosticCount = result?.Diagnostics.Count ?? 0;
                StatusMessage = _targets.Count == 0
                    ? "No audit targets found."
                    : "Found " + _targets.Count + " audit target(s)." +
                      (diagnosticCount == 0
                          ? string.Empty
                          : " Discovery diagnostics: " + diagnosticCount + ".");
            }
            catch (Exception exception)
            {
                _targets = new ReadOnlyCollection<AuditTarget>(
                    new List<AuditTarget>());
                LastRequest = null;
                LastReport = null;
                SetFailure("Refresh failed: " + exception.Message);
            }
        }

        public void RunAudit()
        {
            if (_targets.Count == 0)
            {
                StatusMessage = "No audit targets. Refresh targets to begin.";
                return;
            }

            try
            {
                var request = new AuditRequest(
                    _targets.Select(target => target.AssetPath),
                    DefaultRuleIds,
                    new Dictionary<string, string>(StringComparer.Ordinal));
                LastRequest = request;
                LastReport = _runAudit(request);
                if (LastReport == null)
                {
                    SetFailure("Run failed: the audit runner returned no report.");
                    return;
                }

                RunState = MapRunState(LastReport.Status);
                StatusMessage = LastReport.Status == AuditRunStatus.Completed
                    ? "Audit completed."
                    : LastReport.Status == AuditRunStatus.Cancelled
                        ? "Audit cancelled."
                        : "Audit failed.";
            }
            catch (Exception exception)
            {
                LastReport = null;
                SetFailure("Run failed: " + exception.Message);
            }
        }

        private static AuditReport RunWithDefaultRules(AuditRequest request)
        {
            return new AuditRunner(CreateDefaultRules()).Run(request);
        }

        private static IReadOnlyList<IAuditRule> CreateDefaultRules()
        {
            return new IAuditRule[]
            {
                new StringTableCompletenessRule(),
                new EmptyTranslationRule(),
                new TmpFontCoverageRule(),
                new LocalizedStringReferenceRule()
            };
        }

        private static LocalizationAuditorWindowRunState MapRunState(
            AuditRunStatus status)
        {
            switch (status)
            {
                case AuditRunStatus.Completed:
                    return LocalizationAuditorWindowRunState.Completed;
                case AuditRunStatus.Cancelled:
                    return LocalizationAuditorWindowRunState.Cancelled;
                case AuditRunStatus.Failed:
                    return LocalizationAuditorWindowRunState.Failed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        private void SetFailure(string message)
        {
            RunState = LocalizationAuditorWindowRunState.Failed;
            StatusMessage = message;
        }
    }
}
