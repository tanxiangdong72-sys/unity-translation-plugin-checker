using System;
using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Yxwm.LocalizationAuditor
{
    // 与 SeedCalc 编辑器实测相同的回归断言；仅创建独立资源并在 finally 清理。
    public sealed class LocatorCompatibilityRegressionTests
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        [Test]
        public void PrefabLocationKeepsLiveTargetAndRestoresStagesWithoutChangingBytes()
        {
            const string folder = "Assets/__LocatorCompatibilityRegression20260908";
            var previousSelection = Selection.activeObject;
            var log = new System.Text.StringBuilder();
            bool created = false;
            GameObject source = null;
            try
            {
                Require(PrefabStageUtility.GetCurrentPrefabStage() == null,
                    "Please leave Prefab Stage before running this isolated regression.");
                Require(!AssetDatabase.IsValidFolder(folder), "Test folder already exists; refusing to overwrite.");
                AssetDatabase.CreateFolder("Assets", "__LocatorCompatibilityRegression20260908");
                created = true;
                source = new GameObject("Root");
                var child = new GameObject("Child");
                child.transform.SetParent(source.transform);
                var path = folder + "/Root.prefab";
                PrefabUtility.SaveAsPrefabAsset(source, path);
                UnityEngine.Object.DestroyImmediate(source);
                source = null;
                var before = File.ReadAllBytes(path);
                var location = new AuditIssueLocation(assetPath: path, objectPath: "Root/Child",
                    componentType: typeof(Transform).FullName, propertyPath: "m_LocalPosition");
                var success = AuditIssueLocator.Locate(location);
                Require(success.Succeeded && success.Target != null, "Child/component/property resolution failed.");
                Require(Selection.activeObject == success.Target, "Selection was not updated.");
                log.AppendLine("PASS child/component/property and selection");
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                Require(stage != null && success.Target is Transform, "No live prefab stage target.");
                Require(((Transform)success.Target).gameObject.scene == stage.scene, "Target is not in the open stage.");
                log.AppendLine("PASS live stage target");
                var repeated = AuditIssueLocator.Locate(location);
                Require(repeated.Succeeded && repeated.Target == success.Target &&
                    PrefabStageUtility.GetCurrentPrefabStage() == stage, "Stage was not reused.");
                log.AppendLine("PASS successful stage reuse");

                StageUtility.GoBackToPreviousStage();
                var failed = AuditIssueLocator.Locate(new AuditIssueLocation(assetPath: path, objectPath: "Root/Missing"));
                Require(!failed.Succeeded && PrefabStageUtility.GetCurrentPrefabStage() == null, "Failure did not restore main stage.");
                log.AppendLine("PASS failed location restores main stage");

                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var second = folder + "/Other.prefab";
                AssetDatabase.CopyAsset(path, second);
                failed = AuditIssueLocator.Locate(new AuditIssueLocation(assetPath: second, objectPath: "Root/Missing"));
                var restored = PrefabStageUtility.GetCurrentPrefabStage();
                Require(!failed.Succeeded && restored != null &&
                    AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == path,
                    "Failure did not restore a prefab stage.");
#if UNITY_2020_1_OR_NEWER
                Require(restored.assetPath == path, "Wrong restored stage.");
#else
                Require(restored.prefabAssetPath == path, "Wrong restored stage.");
#endif
                log.AppendLine("PASS failed location restores original prefab stage");
                Require(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path)),
                    "Prefab content changed.");
                log.AppendLine("PASS prefab bytes unchanged");
                log.AppendLine("RESULT PASS Unity " + Application.unityVersion);
            }
            catch (Exception e) { log.AppendLine("RESULT FAIL " + e); throw; }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (created)
                {
                    if (PrefabStageUtility.GetCurrentPrefabStage() != null) StageUtility.GoToMainStage();
                    AssetDatabase.DeleteAsset(folder);
                }
                Selection.activeObject = previousSelection;
                TestContext.WriteLine(log.ToString());
            }
        }
    }
}
