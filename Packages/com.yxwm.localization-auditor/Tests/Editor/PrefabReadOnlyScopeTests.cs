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
    // 验证 Prefab 只读加载、异常释放、重复释放和不保存修改。
    public sealed class PrefabReadOnlyScopeTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task13";
        private const string SceneTemplateSettingsPath =
            "ProjectSettings/SceneTemplateSettings.json";
        private static readonly byte[] OriginalSceneTemplateSettings =
            ReadSceneTemplateSettings();

        [SetUp]
        public void SetUp()
        {
            CleanupRootDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupRootDirectory();
        }

        [Test]
        public void OpenExposesPrefabRootAndDisposeReleasesIt()
        {
            var prefabPath = CreatePrefab("Root", "Child");

            GameObject loadedRoot;
            using (var scope = PrefabReadOnlyScope.Open(prefabPath))
            {
                loadedRoot = scope.Root;
                Assert.That(loadedRoot, Is.Not.Null);
                Assert.That(loadedRoot.name, Is.EqualTo("Root"));
                Assert.That(loadedRoot.transform.Find("Child"), Is.Not.Null);
            }

            Assert.That(
                loadedRoot == null,
                Is.True,
                "Prefab contents should be destroyed after disposal.");
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var prefabPath = CreatePrefab("Root");

            using (var scope = PrefabReadOnlyScope.Open(prefabPath))
            {
                var loadedRoot = scope.Root;
                Assert.DoesNotThrow(() =>
                {
                    scope.Dispose();
                    scope.Dispose();
                });
                Assert.That(
                    loadedRoot == null,
                    Is.True,
                    "Prefab contents should be destroyed after the first disposal.");
            }
        }

        [Test]
        public void DisposeDoesNotSaveChangesMadeToLoadedContents()
        {
            var prefabPath = CreatePrefab("Original");

            using (var scope = PrefabReadOnlyScope.Open(prefabPath))
            {
                scope.Root.name = "ChangedInMemory";
            }

            var reloaded = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Assert.That(reloaded.name, Is.EqualTo("Original"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reloaded);
            }
        }

        [Test]
        public void DisposeReleasesContentsWhenCallerThrows()
        {
            var prefabPath = CreatePrefab("Original");
            GameObject loadedRoot = null;

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var scope = PrefabReadOnlyScope.Open(prefabPath))
                {
                    loadedRoot = scope.Root;
                    throw new InvalidOperationException("Expected test exception.");
                }
            });

            Assert.That(
                loadedRoot == null,
                Is.True,
                "Prefab contents should be destroyed when the caller throws.");
            var reloaded = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Assert.That(reloaded, Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reloaded);
            }
        }

        [Test]
        public void OpenRejectsMissingAndNonPrefabPaths()
        {
            Assert.Throws<ArgumentException>(() =>
                PrefabReadOnlyScope.Open(null));

            Assert.Throws<ArgumentException>(() =>
                PrefabReadOnlyScope.Open(string.Empty));

            Assert.Throws<ArgumentException>(() =>
                PrefabReadOnlyScope.Open("Packages/NotAnAsset.prefab"));

            Assert.Throws<ArgumentException>(() =>
                PrefabReadOnlyScope.Open(RootDirectory + "/Missing.prefab"));

            var scenePath = CreateScene("NotPrefab.unity");

            Assert.Throws<ArgumentException>(() =>
                PrefabReadOnlyScope.Open(scenePath));
        }

        private static string CreatePrefab(
            string rootName,
            string childName = null)
        {
            EnsureFolder(RootDirectory);
            var path = RootDirectory + "/" + rootName + ".prefab";
            var root = new GameObject(rootName);
            if (!string.IsNullOrEmpty(childName))
            {
                var child = new GameObject(childName);
                child.transform.SetParent(root.transform);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Assert.That(prefab, Is.Not.Null);
            return path;
        }

        private static string CreateScene(string sceneName)
        {
            EnsureFolder(RootDirectory);
            var path = RootDirectory + "/" + sceneName;
            var scene = default(Scene);
            try
            {
                var mode = SelectSceneCreationMode();
                scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    mode);
                Assert.That(
                    EditorSceneManager.SaveScene(scene, path),
                    Is.True,
                    "Unity failed to save the temporary Scene fixture.");
                AssetDatabase.Refresh();
                Assert.That(
                    AssetDatabase.AssetPathToGUID(path),
                    Is.Not.Empty,
                    "The temporary Scene fixture was not imported.");
                return path;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        var replacement = EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            NewSceneMode.Single);
                        Assert.That(
                            replacement.IsValid() && replacement.isLoaded,
                            Is.True,
                            "Unity could not replace the last Task13 fixture Scene.");
                    }
                    else
                    {
                        Assert.That(
                            EditorSceneManager.CloseScene(scene, true),
                            Is.True,
                            "Unity could not close the Task13 fixture Scene.");
                    }
                }
            }
        }

        private static NewSceneMode SelectSceneCreationMode()
        {
            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded)
                .ToList();
            if (loadedScenes.Any(scene => scene.isDirty))
            {
                throw new InvalidOperationException(
                    "Task13 will not discard a dirty loaded Scene.");
            }

            if (loadedScenes.Any(scene =>
                    !string.IsNullOrEmpty(scene.path) &&
                    !scene.isDirty) &&
                !loadedScenes.Any(scene => string.IsNullOrEmpty(scene.path)))
            {
                return NewSceneMode.Additive;
            }

            if (loadedScenes.Count == 1 &&
                IsSafeUntitledPlaceholder(loadedScenes[0]))
            {
                return NewSceneMode.Single;
            }

            throw new InvalidOperationException(
                "Task13 requires a saved clean anchor or a clean empty/default untitled placeholder.");
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
            AssetDatabase.DeleteAsset(RootDirectory);
            AssetDatabase.Refresh();

            if (OriginalSceneTemplateSettings == null)
            {
                if (File.Exists(SceneTemplateSettingsPath))
                {
                    File.Delete(SceneTemplateSettingsPath);
                }
            }
            else if (!File.Exists(SceneTemplateSettingsPath) ||
                     !AreBytesEqual(
                         OriginalSceneTemplateSettings,
                         File.ReadAllBytes(SceneTemplateSettingsPath)))
            {
                File.WriteAllBytes(
                    SceneTemplateSettingsPath,
                    OriginalSceneTemplateSettings);
            }
        }

        private static byte[] ReadSceneTemplateSettings()
        {
            return File.Exists(SceneTemplateSettingsPath)
                ? File.ReadAllBytes(SceneTemplateSettingsPath)
                : null;
        }

        private static bool AreBytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
