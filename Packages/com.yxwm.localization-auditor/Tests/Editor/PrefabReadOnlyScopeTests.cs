using System;
using System.IO;
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
                scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
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
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
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
