using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证 Scene 只读打开、Setup 恢复、脏场景拦截和不保存修改。
    public sealed class SceneReadOnlyScopeTests
    {
        private const string FixtureRootDirectory =
            "Assets/LocalizationAuditorTestFixtures/";
        private const string RootDirectory =
            FixtureRootDirectory + "Task14";
        private const string FixtureBaselinePath =
            RootDirectory + "/Baseline.unity";
        private const string FixtureAnchorPath =
            RootDirectory + "/Anchor.unity";
        private static readonly string SceneTemplateSettingsPath =
            "ProjectSettings/SceneTemplateSettings.json";
        private static readonly bool HadSceneTemplateSettings =
            File.Exists(SceneTemplateSettingsPath);
        private static readonly string OriginalSceneTemplateSettings =
            HadSceneTemplateSettings
                ? File.ReadAllText(SceneTemplateSettingsPath)
                : null;
        private SceneSetup[] _originalSetup;

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            try
            {
                CleanupRootDirectory();
            }
            finally
            {
                RestoreSceneTemplateSettings();
            }
        }

        [SetUp]
        public void SetUp()
        {
            CloseKnownFixtureScenes();
            CleanupRootDirectory();
            EnsureUntitledScenesAreSafe();
            EnsureNoDirtyNonFixtureScenes();
            _originalSetup = EditorSceneManager.GetSceneManagerSetup();
            CreateFixtureBaseline();
        }

        [TearDown]
        public void TearDown()
        {
            Exception cleanupError = null;
            try
            {
                try
                {
                    RestoreSceneManagerSetup(_originalSetup);
                }
                catch (Exception exception)
                {
                    cleanupError = exception;
                }
            }
            finally
            {
                try
                {
                    CleanupRootDirectory();
                }
                catch (Exception exception)
                {
                    if (cleanupError == null)
                    {
                        cleanupError = exception;
                    }
                }
                finally
                {
                    try
                    {
                        RestoreSceneTemplateSettings();
                    }
                    catch (Exception exception)
                    {
                        if (cleanupError == null)
                        {
                            cleanupError = exception;
                        }
                    }
                }
            }

            if (cleanupError != null)
            {
                throw cleanupError;
            }
        }

        [Test]
        public void OpenAdditivelyAndDisposeRestoresOriginalSceneSetup()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target");
            var before = EditorSceneManager.GetSceneManagerSetup();

            using (var scope = SceneReadOnlyScope.Open(targetPath))
            {
                Assert.That(scope.Scene.IsValid(), Is.True);
                Assert.That(scope.Scene.path, Is.EqualTo(targetPath));
                Assert.That(scope.Scene.isLoaded, Is.True);
                Assert.That(scope.AssetPath, Is.EqualTo(targetPath));
                Assert.That(
                    SceneManager.sceneCount,
                    Is.EqualTo(before.Length + 1));
            }

            AssertSceneSetupEqual(
                before,
                EditorSceneManager.GetSceneManagerSetup());
        }

        [Test]
        public void OpenRejectsDirtyLoadedScenesBeforeOpeningTarget()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target");
            var originalScene = SceneManager.GetSceneByPath(originalPath);
            Assert.That(EditorSceneManager.SetActiveScene(originalScene), Is.True);
            var dirtyObject = new GameObject("Unsaved");
            Assert.That(dirtyObject.scene.path, Is.EqualTo(originalPath));
            EditorSceneManager.MarkSceneDirty(originalScene);
            var before = EditorSceneManager.GetSceneManagerSetup();

            Assert.Throws<InvalidOperationException>(() =>
                SceneReadOnlyScope.Open(targetPath));

            AssertSceneSetupEqual(
                before,
                EditorSceneManager.GetSceneManagerSetup());
            Assert.That(SceneManager.GetSceneByPath(originalPath).isLoaded, Is.True);
            Assert.That(SceneManager.GetSceneByPath(targetPath).isLoaded, Is.False);
        }

        [Test]
        public void DisposeDoesNotSaveChangesMadeToLoadedScene()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target");

            using (var scope = SceneReadOnlyScope.Open(targetPath))
            {
                EditorSceneManager.SetActiveScene(scope.Scene);
                var createdObject = new GameObject("UnsavedInTarget");
                Assert.That(createdObject.scene, Is.EqualTo(scope.Scene));
                EditorSceneManager.MarkSceneDirty(scope.Scene);
            }

            var reloaded = EditorSceneManager.OpenScene(
                targetPath,
                OpenSceneMode.Additive);
            try
            {
                Assert.That(
                    reloaded.GetRootGameObjects()
                        .Any(root => root.name == "UnsavedInTarget"),
                    Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(reloaded, true);
            }
        }

        [Test]
        public void DisposeRestoresOriginalSetupWhenCallerThrows()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target");
            var before = EditorSceneManager.GetSceneManagerSetup();

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var scope = SceneReadOnlyScope.Open(targetPath))
                {
                    throw new InvalidOperationException("Expected test exception.");
                }
            });

            AssertSceneSetupEqual(
                before,
                EditorSceneManager.GetSceneManagerSetup());
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target");
            var scope = SceneReadOnlyScope.Open(targetPath);

            Assert.DoesNotThrow(() =>
            {
                scope.Dispose();
                scope.Dispose();
            });

            Assert.That(scope.Scene.IsValid(), Is.False);
        }

        [Test]
        public void OpenReusesAlreadyLoadedTargetWithoutClosingIt()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("Target", keepLoaded: true);
            var targetScene = SceneManager.GetSceneByPath(targetPath);
            var before = EditorSceneManager.GetSceneManagerSetup();

            using (var scope = SceneReadOnlyScope.Open(targetPath))
            {
                Assert.That(scope.Scene.path, Is.EqualTo(targetPath));
                Assert.That(scope.Scene, Is.EqualTo(targetScene));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(before.Length));
            }

            Assert.That(SceneManager.sceneCount, Is.EqualTo(before.Length));
            Assert.That(SceneManager.GetSceneByPath(targetPath).isLoaded, Is.True);
            AssertSceneSetupEqual(before, EditorSceneManager.GetSceneManagerSetup());
        }

        [Test]
        public void OpenAcceptsBackslashNormalization()
        {
            var originalPath = CreateScene("Original", keepLoaded: true);
            var targetPath = CreateScene("BackslashTarget");
            var backslashPath = targetPath.Replace('/', '\\');

            using (var scope = SceneReadOnlyScope.Open(backslashPath))
            {
                Assert.That(
                    SceneManager.GetSceneByPath(originalPath).isLoaded,
                    Is.True);
                Assert.That(scope.AssetPath, Is.EqualTo(targetPath));
                Assert.That(scope.Scene.path, Is.EqualTo(targetPath));
            }
        }

        [Test]
        public void FixtureCreationPreservesOriginalLoadedScenes()
        {
            var originalScenes = _originalSetup
                .Where(setup => setup.isLoaded)
                .Select(setup => setup.path)
                .ToArray();

            Assert.That(
                originalScenes.All(path =>
                    string.IsNullOrEmpty(path) ||
                    SceneManager.GetSceneByPath(path).isLoaded),
                Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Packages/Outside/NotScene.unity")]
        [TestCase("Assets/LocalizationAuditorTestFixtures/Task14/Missing.unity")]
        [TestCase("Assets/LocalizationAuditorTestFixtures/Task14/NotScene.asset")]
        public void OpenRejectsInvalidPaths(string assetPath)
        {
            if (string.Equals(
                    assetPath,
                    "Assets/LocalizationAuditorTestFixtures/Task14/NotScene.asset",
                    StringComparison.Ordinal))
            {
                EnsureFolder(RootDirectory);
                AssetDatabase.CreateAsset(
                    new TextAsset("test"),
                    assetPath);
                AssetDatabase.Refresh();
            }

            Assert.Throws<ArgumentException>(() =>
                SceneReadOnlyScope.Open(assetPath));
        }

        private static string CreateScene(string name, bool keepLoaded = false)
        {
            EnsureFolder(RootDirectory);
            var path = RootDirectory + "/" + name + ".unity";
            Assert.That(
                HasSavedCleanLoadedScene(),
                Is.True,
                "A saved clean baseline must be loaded before creating additive fixture Scenes.");
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);

            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            if (!keepLoaded)
            {
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
            }

            AssetDatabase.Refresh();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            return path;
        }

        private static void CreateFixtureBaseline()
        {
            EnsureFolder(RootDirectory);
            EnsureUntitledScenesAreSafe();
            EnsureNoDirtyNonFixtureScenes();

            Scene baseline;
            if (HasSavedCleanLoadedScene())
            {
                baseline = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            }
            else
            {
                var untitled = FindLoadedUntitledScene();
                if (untitled.IsValid())
                {
                    baseline = untitled;
                }
                else
                {
                    baseline = EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }

            Assert.That(
                EditorSceneManager.SaveScene(baseline, FixtureBaselinePath),
                Is.True);
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(FixtureBaselinePath),
                Is.Not.Null);
            Assert.That(baseline.isLoaded, Is.True);
            Assert.That(baseline.isDirty, Is.False);
        }

        private static bool HasSavedCleanLoadedScene()
        {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene =>
                    scene.IsValid() &&
                    scene.isLoaded &&
                    !string.IsNullOrEmpty(scene.path) &&
                    !scene.isDirty);
        }

        private static Scene FindLoadedUntitledScene()
        {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene =>
                    scene.IsValid() &&
                    scene.isLoaded &&
                    string.IsNullOrEmpty(scene.path));
        }

        private static void AssertSceneSetupEqual(
            SceneSetup[] expected,
            SceneSetup[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].path, Is.EqualTo(expected[index].path));
                Assert.That(
                    actual[index].isLoaded,
                    Is.EqualTo(expected[index].isLoaded));
                Assert.That(
                    actual[index].isActive,
                    Is.EqualTo(expected[index].isActive));
            }
        }

        private static void RestoreSceneManagerSetup(SceneSetup[] setup)
        {
            if (setup == null ||
                setup.Length == 0 ||
                setup.All(item => string.IsNullOrEmpty(item.path)))
            {
                return;
            }

            EditorSceneManager.RestoreSceneManagerSetup(setup);
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

        private static void CleanupRootDirectory()
        {
            CloseFixtureScenesByPath();
            if (AssetDatabase.IsValidFolder(RootDirectory) &&
                !AssetDatabase.DeleteAsset(RootDirectory))
            {
                throw new InvalidOperationException(
                    "Unity could not delete fixture folder '" + RootDirectory + "'.");
            }

            AssetDatabase.Refresh();
        }

        private static void CloseKnownFixtureScenes()
        {
            EnsureUntitledScenesAreSafe();
            EnsureNoDirtyNonFixtureScenes();

            var anchor = EnsureKnownFixtureAnchor();
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!IsKnownFixtureScene(scene) ||
                    IsSameScene(scene, anchor))
                {
                    continue;
                }

                if (!EditorSceneManager.CloseScene(scene, true))
                {
                    throw new InvalidOperationException(
                        "Unity could not close prior fixture Scene at '" +
                        scene.path + "'.");
                }
            }
        }

        private static void CloseFixtureScenesByPath()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                if (!string.Equals(scene.path, RootDirectory + "/Baseline.unity", StringComparison.Ordinal) &&
                    !scene.path.StartsWith(RootDirectory + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SceneManager.sceneCount == 1)
                {
                    var replacement = EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                    if (!replacement.IsValid() || !replacement.isLoaded)
                    {
                        throw new InvalidOperationException(
                            "Unity could not replace last fixture Scene at '" +
                            scene.path + "'.");
                    }

                    continue;
                }

                if (!EditorSceneManager.CloseScene(scene, true))
                {
                    throw new InvalidOperationException(
                        "Unity could not close fixture Scene at '" + scene.path + "'.");
                }
            }
        }

        private static Scene EnsureKnownFixtureAnchor()
        {
            if (!HasLoadedKnownFixtureScene() ||
                HasLoadedNonFixtureScene())
            {
                return default(Scene);
            }

            var existingAnchor = SceneManager.GetSceneByPath(FixtureAnchorPath);
            if (existingAnchor.IsValid() &&
                existingAnchor.isLoaded &&
                !existingAnchor.isDirty)
            {
                return existingAnchor;
            }

            EnsureFolder(RootDirectory);
            var anchor = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                HasSavedCleanLoadedScene()
                    ? NewSceneMode.Additive
                    : NewSceneMode.Single);
            if (!anchor.IsValid() || !anchor.isLoaded)
            {
                throw new InvalidOperationException(
                    "Unity could not create a fixture anchor Scene.");
            }

            if (!EditorSceneManager.SaveScene(anchor, FixtureAnchorPath))
            {
                throw new InvalidOperationException(
                    "Unity could not save fixture anchor Scene at '" +
                    FixtureAnchorPath + "'.");
            }

            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FixtureAnchorPath) == null)
            {
                throw new InvalidOperationException(
                    "Unity did not import fixture anchor Scene at '" +
                    FixtureAnchorPath + "'.");
            }

            return anchor;
        }

        private static bool HasLoadedKnownFixtureScene()
        {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(IsKnownFixtureScene);
        }

        private static bool HasLoadedNonFixtureScene()
        {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene =>
                    scene.IsValid() &&
                    scene.isLoaded &&
                    !IsKnownFixtureScene(scene));
        }

        private static bool IsKnownFixtureScene(Scene scene)
        {
            return scene.IsValid() &&
                scene.isLoaded &&
                !string.IsNullOrEmpty(scene.path) &&
                scene.path.StartsWith(
                    FixtureRootDirectory,
                    StringComparison.Ordinal);
        }

        private static bool IsSameScene(Scene left, Scene right)
        {
            return left.IsValid() &&
                right.IsValid() &&
                left.handle == right.handle;
        }

        private static void EnsureNoDirtyNonFixtureScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() &&
                    scene.isLoaded &&
                    !string.IsNullOrEmpty(scene.path) &&
                    !IsKnownFixtureScene(scene) &&
                    scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Task14 setup will not discard a dirty non-fixture Scene at '" +
                        scene.path + "'.");
                }
            }
        }

        private static void EnsureUntitledScenesAreSafe()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() &&
                    scene.isLoaded &&
                    string.IsNullOrEmpty(scene.path) &&
                    scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Task14 setup will not discard a dirty untitled Scene.");
                }
            }
        }

        private static void RestoreSceneTemplateSettings()
        {
            if (HadSceneTemplateSettings)
            {
                File.WriteAllText(
                    SceneTemplateSettingsPath,
                    OriginalSceneTemplateSettings ?? string.Empty);
                return;
            }

            if (File.Exists(SceneTemplateSettingsPath))
            {
                File.Delete(SceneTemplateSettingsPath);
            }
        }
    }
}
