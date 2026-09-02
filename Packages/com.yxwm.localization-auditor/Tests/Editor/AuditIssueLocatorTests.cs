using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证报告定位的路径解析、资源解析、失败状态和只读行为。
    public sealed class AuditIssueLocatorTests
    {
        private static readonly string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task19_" +
            Guid.NewGuid().ToString("N");
        private static readonly string PrefabPath = RootDirectory + "/Root.prefab";
        private static readonly string OriginalPrefabPath =
            RootDirectory + "/Original.prefab";
        private static readonly string ScenePath = RootDirectory + "/Locator.unity";
        private static readonly bool HadFixtureParent =
            AssetDatabase.IsValidFolder("Assets/LocalizationAuditorTestFixtures");
        private readonly List<Scene> _createdScenes = new List<Scene>();
        private string _initialPrefabStageAssetPath;

        [SetUp]
        public void SetUp()
        {
            _initialPrefabStageAssetPath =
                PrefabStageUtility.GetCurrentPrefabStage()?.assetPath;
            CleanupFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentStage != null &&
                IsFixturePath(currentStage.assetPath) &&
                !string.Equals(
                    currentStage.assetPath,
                    _initialPrefabStageAssetPath,
                    StringComparison.Ordinal))
            {
                StageUtility.GoBackToPreviousStage();
            }

            CleanupFixtures();
        }

        [Test]
        public void NormalizeAssetPathAcceptsBackslashesAndRejectsInvalidPaths()
        {
            Assert.That(
                AuditIssueLocator.NormalizeAssetPath("Assets\\Folder\\Item.prefab"),
                Is.EqualTo("Assets/Folder/Item.prefab"));
            Assert.That(
                AuditIssueLocator.ValidateAssetPath(null).Status,
                Is.EqualTo(AuditLocationResultStatus.InvalidLocation));
            Assert.That(
                AuditIssueLocator.ValidateAssetPath("Packages/Item.prefab").Status,
                Is.EqualTo(AuditLocationResultStatus.InvalidLocation));
        }

        [Test]
        public void EmptyLocationFailsWithoutThrowing()
        {
            AuditLocationResult result = null;
            Assert.DoesNotThrow(() =>
                result = AuditIssueLocator.Locate(new AuditIssueLocation()));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(AuditLocationResultStatus.InvalidLocation));
            Assert.That(result.Message, Does.Contain("AssetPath"));
        }

        [Test]
        public void PrefabRootAndChildComponentAreLocatedWithoutChangingBytes()
        {
            CreatePrefabFixture(PrefabPath);
            var before = File.ReadAllBytes(PrefabPath);

            var rootResult = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root"));
            var componentResult = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Child",
                componentType: typeof(Transform).FullName,
                propertyPath: "m_LocalPosition.x"));

            Assert.That(rootResult.Succeeded, Is.True, rootResult.Message);
            Assert.That(rootResult.Target, Is.TypeOf<GameObject>());
            Assert.That(componentResult.Succeeded, Is.True, componentResult.Message);
            Assert.That(componentResult.Target, Is.TypeOf<Transform>());
            Assert.That(Selection.activeObject, Is.SameAs(componentResult.Target));
            Assert.That(File.ReadAllBytes(PrefabPath), Is.EqualTo(before));
        }

        [Test]
        public void LoadedSceneObjectIsLocatedWithoutChangingBytes()
        {
            CreateSceneFixture(keepLoaded: true);
            var before = File.ReadAllBytes(ScenePath);

            var result = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: ScenePath,
                objectPath: "SceneRoot/Child",
                componentType: typeof(Transform).FullName,
                propertyPath: "m_LocalPosition.x"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Target, Is.TypeOf<Transform>());
            Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(before));
        }

        [Test]
        public void MissingObjectComponentAndPropertyReturnReadableFailures()
        {
            CreatePrefabFixture(PrefabPath);

            var missingObject = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Missing"));
            var missingComponent = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Child",
                componentType: "Missing.Component"));
            var missingProperty = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Child",
                componentType: typeof(Transform).FullName,
                propertyPath: "MissingProperty"));

            Assert.That(missingObject.Status, Is.EqualTo(AuditLocationResultStatus.ObjectNotFound));
            Assert.That(missingComponent.Status, Is.EqualTo(AuditLocationResultStatus.ComponentNotFound));
            Assert.That(missingProperty.Status, Is.EqualTo(AuditLocationResultStatus.PropertyNotFound));
            Assert.That(missingObject.Message, Is.Not.Empty);
            Assert.That(missingComponent.Message, Is.Not.Empty);
            Assert.That(missingProperty.Message, Is.Not.Empty);
        }

        [Test]
        public void UnloadedSceneFailsExplicitlyAndDoesNotThrow()
        {
            CreateSceneFixture(keepLoaded: false);

            AuditLocationResult result = null;
            Assert.DoesNotThrow(() =>
                result = AuditIssueLocator.Locate(new AuditIssueLocation(
                    assetPath: ScenePath,
                    objectPath: "SceneRoot")));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(AuditLocationResultStatus.SceneNotLoaded));
            Assert.That(result.Message, Does.Contain("loaded"));
        }

        [Test]
        public void MissingAssetReturnsFailureInsteadOfThrowing()
        {
            AuditLocationResult result = null;
            Assert.DoesNotThrow(() =>
                result = AuditIssueLocator.Locate(new AuditIssueLocation(
                    assetPath: RootDirectory + "/Missing.prefab",
                    objectPath: "Root")));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(AuditLocationResultStatus.AssetNotFound));
        }

        [Test]
        public void WindowStateDisplaysLocatorFailureWithoutThrowing()
        {
            var state = new LocalizationAuditorWindowState();
            var result = AuditIssueLocator.Locate(new AuditIssueLocation());

            Assert.DoesNotThrow(() => state.SetLocationStatus(result));
            Assert.That(state.StatusMessage, Does.Contain("Locate failed"));
            Assert.That(state.StatusMessage, Does.Contain("AssetPath"));
        }

        [Test]
        public void SuccessfulPrefabLocateKeepsTargetPrefabStageOpen()
        {
            CreatePrefabFixture(PrefabPath);

            var result = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root"));

            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(currentStage, Is.Not.Null);
            Assert.That(currentStage.assetPath, Is.EqualTo(PrefabPath));
        }

        [Test]
        public void FailedPrefabLocateRestoresPreviousPrefabStage()
        {
            CreatePrefabFixture(PrefabPath);
            CreatePrefabFixture(OriginalPrefabPath);
            var originalStage = PrefabStageUtility.OpenPrefab(OriginalPrefabPath);
            Assert.That(originalStage, Is.Not.Null);

            var result = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Missing"));

            var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(result.Status, Is.EqualTo(AuditLocationResultStatus.ObjectNotFound));
            Assert.That(currentStage, Is.Not.Null);
            Assert.That(currentStage.assetPath, Is.EqualTo(OriginalPrefabPath));
        }

        [Test]
        public void FailedPrefabLocateRestoresPreviousSceneStage()
        {
            CreatePrefabFixture(PrefabPath);
            CreateSceneFixture(keepLoaded: true);
            var originalScene = SceneManager.GetActiveScene();
            Assert.That(originalScene.path, Is.EqualTo(ScenePath));

            var result = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Missing"));

            Assert.That(result.Status, Is.EqualTo(AuditLocationResultStatus.ObjectNotFound));
            Assert.That(PrefabStageUtility.GetCurrentPrefabStage(), Is.Null);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
        }

        [Test]
        public void LocateReusesExistingTargetPrefabStageForSuccessAndFailure()
        {
            CreatePrefabFixture(PrefabPath);
            var originalStage = PrefabStageUtility.OpenPrefab(PrefabPath);
            Assert.That(originalStage, Is.Not.Null);

            var success = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root"));
            var successStage = PrefabStageUtility.GetCurrentPrefabStage();
            var failure = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root/Missing"));
            var failureStage = PrefabStageUtility.GetCurrentPrefabStage();

            Assert.That(success.Succeeded, Is.True, success.Message);
            Assert.That(failure.Status, Is.EqualTo(AuditLocationResultStatus.ObjectNotFound));
            Assert.That(successStage, Is.SameAs(originalStage));
            Assert.That(failureStage, Is.SameAs(originalStage));
        }

        [Test]
        public void RepeatedPrefabLocateDoesNotCreateAdditionalStages()
        {
            CreatePrefabFixture(PrefabPath);

            var first = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root"));
            var firstStage = PrefabStageUtility.GetCurrentPrefabStage();
            var second = AuditIssueLocator.Locate(new AuditIssueLocation(
                assetPath: PrefabPath,
                objectPath: "Root"));
            var secondStage = PrefabStageUtility.GetCurrentPrefabStage();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(secondStage, Is.SameAs(firstStage));
            Assert.That(secondStage.assetPath, Is.EqualTo(PrefabPath));
        }

        [Test]
        public void LocateButtonEligibilityRejectsInvalidAssetOrObjectPath()
        {
            CreatePrefabFixture(PrefabPath);

            var method = typeof(AuditIssueLocator).GetMethod(
                "CanLocate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var canLocate = new Func<AuditIssueLocation, bool>(location =>
                (bool)method.Invoke(null, new object[] { location }));
            Assert.That(
                canLocate(new AuditIssueLocation(
                    assetPath: RootDirectory + "/Missing.prefab",
                    objectPath: "Root")),
                Is.False);
            Assert.That(
                canLocate(new AuditIssueLocation(
                    assetPath: PrefabPath,
                    objectPath: " ")),
                Is.False);
            Assert.That(
                canLocate(new AuditIssueLocation(
                    assetPath: PrefabPath,
                    objectPath: "Root")),
                Is.True);
        }

        private void CreatePrefabFixture(string prefabPath)
        {
            EnsureFolder(RootDirectory);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            Assert.That(PrefabUtility.SaveAsPrefabAsset(root, prefabPath), Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
        }

        private void CreateSceneFixture(bool keepLoaded)
        {
            EnsureFolder(RootDirectory);
            EnsureSceneAnchor();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            _createdScenes.Add(scene);
            var root = new GameObject("SceneRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            Assert.That(EditorSceneManager.SaveScene(scene, ScenePath), Is.True);
            AssetDatabase.Refresh();
            if (!keepLoaded)
            {
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                _createdScenes.Remove(scene);
            }
        }

        private void CleanupFixtures()
        {
            for (var index = _createdScenes.Count - 1; index >= 0; index--)
            {
                var scene = _createdScenes[index];
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            NewSceneMode.Single);
                    }
                    else
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            _createdScenes.Clear();
            if (AssetDatabase.IsValidFolder(RootDirectory))
            {
                AssetDatabase.DeleteAsset(RootDirectory);
                AssetDatabase.Refresh();
            }

            if (!HadFixtureParent &&
                AssetDatabase.IsValidFolder("Assets/LocalizationAuditorTestFixtures"))
            {
                var remainingAssets = AssetDatabase.FindAssets(
                    string.Empty,
                    new[] { "Assets/LocalizationAuditorTestFixtures" });
                if (remainingAssets.Length == 0)
                {
                    AssetDatabase.DeleteAsset("Assets/LocalizationAuditorTestFixtures");
                    AssetDatabase.Refresh();
                }
            }
        }

        private void EnsureSceneAnchor()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene =>
                    scene.IsValid() &&
                    scene.isLoaded &&
                    !string.IsNullOrEmpty(scene.path) &&
                    !scene.isDirty))
            {
                return;
            }

            var anchor = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var anchorPath = RootDirectory + "/Anchor.unity";
            Assert.That(EditorSceneManager.SaveScene(anchor, anchorPath), Is.True);
            AssetDatabase.Refresh();
            _createdScenes.Add(anchor);
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

        private static bool IsFixturePath(string assetPath)
        {
            return string.Equals(assetPath, PrefabPath, StringComparison.Ordinal) ||
                   string.Equals(
                       assetPath,
                       OriginalPrefabPath,
                       StringComparison.Ordinal);
        }
    }
}
