using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 用唯一 fixture 验证 LocalizedString 引用失效检查、定位和只读行为。
    public sealed class LocalizedStringReferenceRuleTests
    {
        private static readonly string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task16_" +
            Guid.NewGuid().ToString("N");
        private static readonly string AnchorPath = RootDirectory + "/Anchor.unity";
        private static readonly string ScenePath = RootDirectory + "/References.unity";
        private static readonly string PrefabPath = RootDirectory + "/References.prefab";
        private const string CollectionName = "Task16 Strings";
        private const string SceneTemplateSettingsPath =
            "ProjectSettings/SceneTemplateSettings.json";
        private static readonly byte[] OriginalSceneTemplateSettings =
            ReadSceneTemplateSettings();
        private static readonly HashSet<ulong> CreatedSceneHandles =
            new HashSet<ulong>();
        private static bool RemoveAddressablesOnCleanup;

        [SetUp]
        public void SetUp()
        {
            PrepareSceneEnvironment();
            CleanupFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            PrepareSceneEnvironment();
            CleanupFixtures();
        }

        [Test]
        public void ReportsInvalidSceneReferencesAndIgnoresValidAndEmptyReferences()
        {
            // 使用标准 IDisposable 生命周期，确保本测试创建的 Locale 和 Addressables 注册及时释放。
            using (var fixture = CreateLocalizationFixture())
            {
                CreateSceneFixture(fixture);

                var before = File.ReadAllBytes(ScenePath);
                var issues = EvaluateRule(ScenePath);

                Assert.That(issues.Length, Is.EqualTo(4));
                Assert.That(
                    issues.Select(issue => issue.Location.ObjectPath),
                    Is.EqualTo(new[]
                    {
                        "SceneRoot/A_InvalidTable",
                        "SceneRoot/B_InvalidEntryName",
                        "SceneRoot/C_InvalidEntryId",
                        "SceneRoot/D_IncompleteEntry"
                    }));

                var invalidTable = issues[0];
                Assert.That(invalidTable.RuleId, Is.EqualTo(LocalizedStringReferenceRule.RuleId));
                Assert.That(invalidTable.Severity, Is.EqualTo(AuditSeverity.Error));
                Assert.That(invalidTable.Message, Is.EqualTo(
                    "LocalizedString table reference 'Missing Strings' does not resolve to a String Table collection."));
                Assert.That(invalidTable.Location.AssetPath, Is.EqualTo(ScenePath));
                Assert.That(invalidTable.Location.TableName, Is.EqualTo("Missing Strings"));
                Assert.That(invalidTable.Location.Key, Is.EqualTo("MISSING_TABLE"));
                Assert.That(invalidTable.FixSuggestion, Is.EqualTo(
                    "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry."));

                var invalidName = issues[1];
                Assert.That(invalidName.Message, Is.EqualTo(
                    "LocalizedString entry name 'MISSING_NAME' does not exist in String Table collection 'Task16 Strings'."));
                Assert.That(invalidName.Location.TableName, Is.EqualTo(CollectionName));
                Assert.That(invalidName.Location.Key, Is.EqualTo("MISSING_NAME"));

                var invalidId = issues[2];
                Assert.That(invalidId.Message, Is.EqualTo(
                    "LocalizedString entry id '9999' does not exist in table reference 'GUID:" +
                    GetCollectionGuid(fixture) +
                    "' (resolved collection 'Task16 Strings')."));
                Assert.That(
                    invalidId.Location.TableName,
                    Is.EqualTo("GUID:" + GetCollectionGuid(fixture)));
                Assert.That(invalidId.Location.Key, Is.EqualTo("9999"));

                var incomplete = issues[3];
                Assert.That(incomplete.Message, Is.EqualTo(
                    "LocalizedString entry reference is empty while table reference 'Task16 Strings' is configured."));
                Assert.That(incomplete.Location.TableName, Is.EqualTo(CollectionName));
                Assert.That(incomplete.Location.Key, Is.Empty);

                Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(before));
            }
        }

        [Test]
        public void MatchesGuidTableAndEntryIdAndReportsInvalidPrefabReference()
        {
            // 使用标准 IDisposable 生命周期，确保本测试创建的 Locale 和 Addressables 注册及时释放。
            using (var fixture = CreateLocalizationFixture())
            {
                CreatePrefabFixture(fixture);

                var sharedDataPath = AssetDatabase.GetAssetPath(
                    fixture.StringTableCollection.SharedData);
                var collectionGuid = AssetDatabase.AssetPathToGUID(sharedDataPath);
                var before = File.ReadAllBytes(PrefabPath);

                var issues = EvaluateRule(PrefabPath);

                Assert.That(issues.Length, Is.EqualTo(1));
                var issue = issues.Single();
                Assert.That(issue.Message, Is.EqualTo(
                    "LocalizedString entry id '9999' does not exist in table reference 'GUID:" +
                    collectionGuid +
                    "' (resolved collection 'Task16 Strings')."));
                Assert.That(issue.Location.AssetPath, Is.EqualTo(PrefabPath));
                Assert.That(issue.Location.ObjectPath, Does.EndWith("/InvalidId"));
                Assert.That(issue.Location.ComponentType, Is.EqualTo(
                    typeof(LocalizeStringEvent).FullName));
                Assert.That(
                    issue.Location.PropertyPath,
                    Is.EqualTo("m_StringReference.m_TableReference"));
                Assert.That(
                    issue.Location.TableName,
                    Is.EqualTo("GUID:" + collectionGuid));
                Assert.That(issue.Location.Key, Is.EqualTo("9999"));
                Assert.That(issue.Message, Does.Contain("GUID:" + collectionGuid));
                Assert.That(File.ReadAllBytes(PrefabPath), Is.EqualTo(before));
            }
        }

        [Test]
        public void SortsIssuesByAssetObjectComponentPropertyAndReference()
        {
            // 使用标准 IDisposable 生命周期，确保本测试创建的 Locale 和 Addressables 注册及时释放。
            using (var fixture = CreateLocalizationFixture())
            {
                CreateSceneFixture(fixture);
                CreatePrefabFixture(fixture);

                var issues = EvaluateRule(ScenePath, PrefabPath);
                var keys = issues.Select(issue =>
                    issue.Location.AssetPath + "|" +
                    issue.Location.ObjectPath + "|" +
                    issue.Location.ComponentType + "|" +
                    issue.Location.PropertyPath + "|" +
                    issue.Location.TableName + "|" +
                    issue.Location.Key);

                Assert.That(keys, Is.EqualTo(keys.OrderBy(key => key, StringComparer.Ordinal)));
                Assert.That(
                    issues.GroupBy(issue =>
                        issue.Location.AssetPath + "|" +
                        issue.Location.ObjectPath + "|" +
                        issue.Location.ComponentType + "|" +
                        issue.Location.PropertyPath)
                        .All(group => group.Count() == 1),
                    Is.True);
            }
        }

        [Test]
        public void HonorsCancellationBeforeScanningTargets()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    EvaluateRule(ScenePath, cancellationToken: cancellation.Token));
            }
        }

        private static LocalizationFixture CreateLocalizationFixture()
        {
            var hadAddressablesSetup =
                AddressableAssetSettingsDefaultObject.SettingsExists ||
                AssetDatabase.IsValidFolder(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            RemoveAddressablesOnCleanup = !hadAddressablesSetup;

            var fixture = LocalizationFixtureFactory.Create(
                RootDirectory,
                CollectionName,
                "en");
            fixture.AddEntry("VALID_NAME", "en", "Valid");
            fixture.AddEntry("VALID_ID", "en", "Valid Id");
            return fixture;
        }

        private static AuditIssue[] EvaluateRule(
            params string[] assetPaths)
        {
            return EvaluateRule(
                assetPaths,
                CancellationToken.None);
        }

        private static AuditIssue[] EvaluateRule(
            string assetPath,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return EvaluateRule(new[] { assetPath }, cancellationToken);
        }

        private static AuditIssue[] EvaluateRule(
            IEnumerable<string> assetPaths,
            CancellationToken cancellationToken)
        {
            var rule = new LocalizedStringReferenceRule();
            return rule.Evaluate(
                    new AuditContext(new AuditRequest(assetPaths)),
                    cancellationToken)
                .ToArray();
        }

        private static void CreateSceneFixture(LocalizationFixture fixture)
        {
            EnsureFolder(RootDirectory);
            EnsureSavedFixtureAnchor();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var handle = scene.handle.GetRawData();
            CreatedSceneHandles.Add(handle);
            try
            {
                var root = new GameObject("SceneRoot");
                SceneManager.MoveGameObjectToScene(root, scene);
                CreateReferenceObject(
                    root,
                    "A_InvalidTable",
                    "Missing Strings",
                    "MISSING_TABLE");
                CreateReferenceObject(
                    root,
                    "B_InvalidEntryName",
                    CollectionName,
                    "MISSING_NAME");
                CreateReferenceObject(
                    root,
                    "C_InvalidEntryId",
                    fixture,
                    9999L);
                CreateReferenceObject(
                    root,
                    "D_IncompleteEntry",
                    CollectionName,
                    string.Empty);
                CreateReferenceObject(
                    root,
                    "E_Empty",
                    null,
                    null);
                CreateReferenceObject(
                    root,
                    "E2_EmptyTable",
                    null,
                    "IGNORED_WITH_EMPTY_TABLE");
                CreateReferenceObject(
                    root,
                    "F_Valid",
                    CollectionName,
                    "VALID_NAME");

                Assert.That(EditorSceneManager.SaveScene(scene, ScenePath), Is.True);
                AssetDatabase.Refresh();
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                    Is.Not.Null);
                CloseCreatedScene(scene);
                scene = default(Scene);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    CloseCreatedScene(scene);
                }
            }
        }

        private static void CreatePrefabFixture(LocalizationFixture fixture)
        {
            EnsureFolder(RootDirectory);
            EnsureSavedFixtureAnchor();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var handle = scene.handle.GetRawData();
            CreatedSceneHandles.Add(handle);
            try
            {
                var root = new GameObject("PrefabRoot");
                SceneManager.MoveGameObjectToScene(root, scene);
                var valid = new GameObject("ValidGuidId");
                valid.transform.SetParent(root.transform);
                var validReference = valid.AddComponent<LocalizeStringEvent>();
                var sharedDataPath = AssetDatabase.GetAssetPath(
                    fixture.StringTableCollection.SharedData);
                var collectionGuid = Guid.Parse(
                    AssetDatabase.AssetPathToGUID(sharedDataPath));
                validReference.StringReference.SetReference(
                    collectionGuid,
                    fixture.GetTable("en").GetEntry("VALID_ID").KeyId);

                var invalid = new GameObject("InvalidId");
                invalid.transform.SetParent(root.transform);
                var invalidReference = invalid.AddComponent<LocalizeStringEvent>();
                invalidReference.StringReference.SetReference(collectionGuid, 9999L);

                var empty = new GameObject("Empty");
                empty.transform.SetParent(root.transform);
                empty.AddComponent<LocalizeStringEvent>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                    Is.Not.Null);
                CloseCreatedScene(scene);
                scene = default(Scene);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    CloseCreatedScene(scene);
                }
            }
        }

        private static string GetCollectionGuid(LocalizationFixture fixture)
        {
            return AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(fixture.StringTableCollection.SharedData));
        }

        private static void CreateReferenceObject(
            GameObject root,
            string name,
            string table,
            string key)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            var reference = child.AddComponent<LocalizeStringEvent>();
            if (table != null || key != null)
            {
                reference.StringReference.SetReference(table, key);
            }
        }

        private static void CreateReferenceObject(
            GameObject root,
            string name,
            LocalizationFixture fixture,
            long keyId)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            var reference = child.AddComponent<LocalizeStringEvent>();
            var sharedDataPath = AssetDatabase.GetAssetPath(
                fixture.StringTableCollection.SharedData);
            reference.StringReference.SetReference(
                Guid.Parse(AssetDatabase.AssetPathToGUID(sharedDataPath)),
                keyId);
        }

        private static void EnsureSavedFixtureAnchor()
        {
            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded)
                .ToList();
            if (loadedScenes.Any(scene => scene.isDirty))
            {
                throw new InvalidOperationException(
                    "Task16 cannot create a fixture while a loaded Scene is dirty.");
            }

            var untitledScenes = loadedScenes
                .Where(scene => string.IsNullOrEmpty(scene.path))
                .ToList();
            if (untitledScenes.Count == 0)
            {
                return;
            }

            if (untitledScenes.Count != 1 ||
                !IsSafeUntitledPlaceholder(untitledScenes[0]))
            {
                throw new InvalidOperationException(
                    "Task16 cannot create a fixture while an unsafe untitled Scene is loaded.");
            }

            var anchor = untitledScenes[0];
            Assert.That(EditorSceneManager.SaveScene(anchor, AnchorPath), Is.True);
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                Is.Not.Null);
            CreatedSceneHandles.Add(anchor.handle.GetRawData());
        }

        private static bool IsSafeUntitledPlaceholder(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty)
            {
                return false;
            }

            var roots = scene.GetRootGameObjects();
            if (roots.Length == 0)
            {
                return true;
            }

            if (roots.Length != 2)
            {
                return false;
            }

            var cameras = roots.Where(root => root.name == "Main Camera").ToArray();
            var lights = roots.Where(root => root.name == "Directional Light").ToArray();
            return cameras.Length == 1 &&
                lights.Length == 1 &&
                cameras[0].GetComponent<Camera>() != null &&
                lights[0].GetComponent<Light>() != null;
        }

        private static void CloseCreatedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (SceneManager.sceneCount == 1)
            {
                var replacement = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Assert.That(replacement.IsValid() && replacement.isLoaded, Is.True);
            }
            else
            {
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
            }

            CreatedSceneHandles.Remove(scene.handle.GetRawData());
        }

        private static void PrepareSceneEnvironment()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    (!CreatedSceneHandles.Contains(scene.handle.GetRawData()) &&
                     !string.Equals(scene.path, AnchorPath, StringComparison.Ordinal) &&
                     !string.Equals(scene.path, ScenePath, StringComparison.Ordinal)))
                {
                    continue;
                }

                CloseCreatedScene(scene);
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Task16 will not discard a dirty non-fixture Scene.");
                }
            }
        }

        private static void CleanupFixtures()
        {
            PrepareSceneEnvironment();
            RemoveFixtureLocales();
            DeleteAssetIfPresent(ScenePath);
            DeleteAssetIfPresent(PrefabPath);
            DeleteAssetIfPresent(AnchorPath);
            if (AssetDatabase.IsValidFolder(RootDirectory))
            {
                Assert.That(AssetDatabase.DeleteAsset(RootDirectory), Is.True);
            }

            AssetDatabase.Refresh();
            Assert.That(AssetDatabase.IsValidFolder(RootDirectory), Is.False);
            if (RemoveAddressablesOnCleanup)
            {
                EditorBuildSettings.RemoveConfigObject(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
                EditorBuildSettings.RemoveConfigObject(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName);
                AssetDatabase.DeleteAsset(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
                ResetAddressableSettingsCache();
                RemoveAddressablesOnCleanup = false;
            }

            RestoreSceneTemplateSettings();
        }

        private static void RemoveFixtureLocales()
        {
            var locales = LocalizationEditorSettings
                .GetLocales()
                .Where(locale =>
                    AssetDatabase.GetAssetPath(locale).StartsWith(
                        RootDirectory + "/",
                        StringComparison.Ordinal))
                .ToArray();
            foreach (var locale in locales)
            {
                LocalizationEditorSettings.RemoveLocale(locale);
                DeleteAssetIfPresent(AssetDatabase.GetAssetPath(locale));
            }
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null)
            {
                return;
            }

            Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True);
        }

        private static void EnsureFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static void ResetAddressableSettingsCache()
        {
            var field = typeof(AddressableAssetSettingsDefaultObject).GetField(
                "s_DefaultSettingsObject",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        private static byte[] ReadSceneTemplateSettings()
        {
            return File.Exists(SceneTemplateSettingsPath)
                ? File.ReadAllBytes(SceneTemplateSettingsPath)
                : null;
        }

        private static void RestoreSceneTemplateSettings()
        {
            if (OriginalSceneTemplateSettings == null)
            {
                if (File.Exists(SceneTemplateSettingsPath))
                {
                    File.Delete(SceneTemplateSettingsPath);
                }

                return;
            }

            if (!File.Exists(SceneTemplateSettingsPath) ||
                !OriginalSceneTemplateSettings.SequenceEqual(
                    File.ReadAllBytes(SceneTemplateSettingsPath)))
            {
                File.WriteAllBytes(
                    SceneTemplateSettingsPath,
                    OriginalSceneTemplateSettings);
            }
        }
    }

}
