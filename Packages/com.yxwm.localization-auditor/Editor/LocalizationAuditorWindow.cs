using UnityEditor;
using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    // 第一版窗口只提供刷新、运行和摘要，不承载报告列表。
    public sealed class LocalizationAuditorWindow : EditorWindow
    {
        private LocalizationAuditorWindowState _state;

        [MenuItem("Window/Localization Auditor")]
        private static void Open()
        {
            GetWindow<LocalizationAuditorWindow>("Localization Auditor");
        }

        private void OnEnable()
        {
            _state = new LocalizationAuditorWindowState();
        }

        private void OnGUI()
        {
            if (_state == null)
            {
                _state = new LocalizationAuditorWindowState();
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
            EditorGUILayout.LabelField("Errors / Error", _state.ErrorCount.ToString());
            EditorGUILayout.LabelField("Warnings / Warning", _state.WarningCount.ToString());
            EditorGUILayout.LabelField("Not verified / NotVerified", _state.NotVerifiedCount.ToString());
            EditorGUILayout.LabelField("Diagnostics / Diagnostic", _state.DiagnosticCount.ToString());
        }
    }
}
