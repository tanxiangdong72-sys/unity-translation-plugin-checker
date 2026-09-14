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
        [SerializeField] private AuditFontProfile _fontProfile;
        private Vector2 _fontScrollPosition;

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
            // 批处理模式没有可交互的 Editor GUI 上下文，反射烟测也应安全返回。
            if (Application.isBatchMode)
            {
                return;
            }

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

            var fontConfigurationValid = DrawFontConfiguration();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh / 刷新目标"))
                {
                    _state.RefreshTargets();
                    ResetFilters();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(_state.Targets.Count == 0 || !fontConfigurationValid))
                {
                    if (GUILayout.Button("Run Audit / 运行审计"))
                    {
                        _state.SetFontMappings(_fontProfile != null ? _fontProfile.BuildMappings() : null);
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

        private bool DrawFontConfiguration()
        {
            _fontProfile = (AuditFontProfile)EditorGUILayout.ObjectField(
                "Font profile / 字体配置", _fontProfile, typeof(AuditFontProfile), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create profile / 新建配置"))
                {
                    var path = EditorUtility.SaveFilePanelInProject(
                        "Save font profile", "AuditFontProfile", "asset", "选择字体配置保存位置");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // 不覆盖已有资源；创建只发生在用户明确选择保存位置之后。
                        var profile = CreateInstance<AuditFontProfile>();
                        AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(path));
                        _fontProfile = profile;
                    }
                }
                using (new EditorGUI.DisabledScope(_fontProfile == null))
                {
                    if (GUILayout.Button("Save profile / 保存配置"))
                        AssetDatabase.SaveAssetIfDirty(_fontProfile);
                }
            }
            EditorGUILayout.HelpBox(
                "Locale Code 必须与项目语言代码一致（例如 en、zh-Hans）；Font 选择 TMP Font Asset。未配置的语言仍为 NotVerified。修改后请重新扫描。", MessageType.Info);
            if (_fontProfile == null) return true;
            // SerializedObject 提供 Undo、脏标记和持久化，避免手动写入资源文件。
            using (var serialized = new SerializedObject(_fontProfile))
            {
                serialized.Update();
                _fontScrollPosition = EditorGUILayout.BeginScrollView(_fontScrollPosition, GUILayout.MaxHeight(180));
                EditorGUILayout.PropertyField(serialized.FindProperty("_entries"),
                    new GUIContent("Locale fonts / 语言字体列表"), true);
                EditorGUILayout.EndScrollView();
                serialized.ApplyModifiedProperties();
            }
            try { _fontProfile.BuildMappings(); return true; }
            catch (System.InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return false;
            }
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

        private void DrawIssue(AuditIssue issue)
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

            using (new EditorGUI.DisabledScope(!AuditIssueLocator.CanLocate(location)))
            {
                if (GUILayout.Button("Locate / 定位"))
                {
                    var result = AuditIssueLocator.Locate(location);
                    _state.SetLocationStatus(result);
                    Repaint();
                }
            }

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
