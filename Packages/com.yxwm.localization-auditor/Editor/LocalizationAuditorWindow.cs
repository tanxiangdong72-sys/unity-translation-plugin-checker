using UnityEditor;
using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    // Editor-only 窗口负责交互和绘制，报告筛选保持在纯状态模型中。
    public sealed class LocalizationAuditorWindow : EditorWindow
    {
        private LocalizationAuditorWindowState _state;
        private int _severityFilterIndex;
        private int _issueStateFilterIndex;
        private Vector2 _issueScrollPosition;

        [MenuItem("Window/Localization Auditor")]
        private static void Open()
        {
            GetWindow<LocalizationAuditorWindow>("Localization Auditor");
        }

        private void OnEnable()
        {
            _state = new LocalizationAuditorWindowState();
            ResetFilters();
        }

        private void OnGUI()
        {
            if (_state == null)
            {
                _state = new LocalizationAuditorWindowState();
                ResetFilters();
            }

            EditorGUILayout.LabelField("Localization Auditor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Targets",
                _state.Targets.Count + " (" +
                _state.SceneTargetCount + " Scene, " +
                _state.PrefabTargetCount + " Prefab)");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh / 刷新目标"))
                {
                    _state.RefreshTargets();
                    ResetFilters();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(_state.Targets.Count == 0))
                {
                    if (GUILayout.Button("Run Audit / 运行审计"))
                    {
                        _state.RunAudit();
                        Repaint();
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Status / 状态", _state.StatusMessage);
            if (_state.LastReport == null)
            {
                EditorGUILayout.HelpBox(
                    "No audit has been run. Refresh targets, then run the audit.",
                    MessageType.Info);
                return;
            }

            var report = _state.LastReport;
            EditorGUILayout.LabelField("Last run / 最近运行", report.Status.ToString());
            EditorGUILayout.LabelField("Scanned assets / 扫描资源", report.ScannedAssetCount.ToString());
            var filter = DrawFilters();
            var view = new AuditReportViewState(report, filter);
            var summary = view.Summary;
            EditorGUILayout.LabelField(
                "Summary / 摘要",
                "Total " + summary.TotalIssueCount +
                " | Error " + summary.ErrorCount +
                " | Warning " + summary.WarningCount +
                " | NotVerified " + summary.NotVerifiedCount +
                " | Ignored " + summary.IgnoredCount +
                " | Diagnostic " + summary.DiagnosticCount);
            EditorGUILayout.LabelField(
                "Visible issues / 当前筛选",
                view.FilteredIssueCount.ToString());

            var statusMessage = AuditReportViewState.GetStatusMessage(report, filter);
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
                return;
            }

            _issueScrollPosition = EditorGUILayout.BeginScrollView(_issueScrollPosition);
            foreach (var issue in view.GetIssues(filter))
            {
                DrawIssue(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private AuditIssueFilter DrawFilters()
        {
            EditorGUILayout.LabelField("Filters / 筛选", EditorStyles.boldLabel);
            var severity = EditorGUILayout.Popup(
                "Severity / 严重级别",
                _severityFilterIndex,
                new[] { "All", "Error", "Warning", "NotVerified" });
            var state = EditorGUILayout.Popup(
                "Issue state / 问题状态",
                _issueStateFilterIndex,
                new[] { "Open", "Ignored", "All" });
            _severityFilterIndex = Mathf.Clamp(severity, 0, 3);
            _issueStateFilterIndex = Mathf.Clamp(state, 0, 2);

            AuditSeverity? severityFilter = null;
            if (_severityFilterIndex > 0)
            {
                severityFilter = (AuditSeverity)(_severityFilterIndex - 1);
            }

            return new AuditIssueFilter(
                severityFilter,
                (AuditIssueStateFilter)_issueStateFilterIndex);
        }

        private static void DrawIssue(AuditIssue issue)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                issue.Severity + " | " + issue.RuleId,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Message / 消息",
                issue.Message,
                EditorStyles.wordWrappedLabel);
            var location = issue.Location;
            var assetPath = string.IsNullOrEmpty(location.AssetPath)
                ? "(none)"
                : location.AssetPath;
            var objectPath = string.IsNullOrEmpty(location.ObjectPath)
                ? "(none)"
                : location.ObjectPath;
            var propertyPath = string.IsNullOrEmpty(location.PropertyPath)
                ? "(none)"
                : location.PropertyPath;
            EditorGUILayout.LabelField(
                "Asset / 资源",
                assetPath,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Object / 对象",
                objectPath,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Property / 属性",
                propertyPath,
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndVertical();
        }

        private void ResetFilters()
        {
            _severityFilterIndex = 0;
            _issueStateFilterIndex = (int)AuditIssueStateFilter.Open;
            _issueScrollPosition = Vector2.zero;
        }
    }
}
