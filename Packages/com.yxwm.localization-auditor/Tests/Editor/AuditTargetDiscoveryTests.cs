using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证扫描输入的路径校验、Scene/Prefab 发现、去重和只读行为。
    public sealed class AuditTargetDiscoveryTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task12";
        private const string FixtureRootDirectory =
            "Assets/LocalizationAuditorTestFixtures";
        private const string AnchorPath = FixtureRootDirectory + "/Task12_Anchor.unity";
        private const string SceneTemplateSettingsPath =
            "ProjectSettings/SceneTemplateSettings.json";
        private static readonly byte[] OriginalSceneTemplateSettings =
            ReadSceneTemplateSettings();
        private static readonly HashSet<string> CreatedTask12Paths =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<ulong> CreatedTask12SceneHandles =
            new HashSet<ulong>();

        [SetUp]
        public void SetUp()
        {
            CloseTask12Scenes();
            CleanupRootDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            CloseTask12Scenes();
            CleanupRootDirectory();
        }

        [Test]
        public void DiscoverRecursivelyFindsScenesAndPrefabsInStableOrder()
        {
            var nestedDirectory = RootDirectory + "/Nested";
            EnsureFolder(nestedDirectory);
            var rootPrefabPath = CreatePrefab(RootDirectory + "/Root.prefab");
            var nestedPrefabPath = CreatePrefab(nestedDirectory + "/Nested.prefab");
            var nestedScenePath = CreateScene(nestedDirectory + "/Nested.unity");

            var result = AuditTargetDiscovery.Discover(new[]
            {
                nestedDirectory,
                RootDirectory,
                rootPrefabPath
            });

            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.Targets.Select(target => target.AssetPath),
                Is.EqualTo(new[]
                {
                    nestedPrefabPath,
                    nestedScenePath,
                    rootPrefabPath
                }));
            Assert.That(
                result.Targets.Select(target => target.Kind),
                Is.EqualTo(new[]
                {
                    AuditTargetKind.Prefab,
                    AuditTargetKind.Scene,
                    AuditTargetKind.Prefab
                }));
        }

        [Test]
        public void DiscoverAcceptsDirectSceneAndPrefabFiles()
        {
            var scenePath = CreateScene(RootDirectory + "/Direct.unity");
            var prefabPath = CreatePrefab(RootDirectory + "/Direct.prefab");

            var result = AuditTargetDiscovery.Discover(new[]
            {
                prefabPath,
                scenePath
            });

            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.Targets.Select(target => target.AssetPath),
                Is.EqualTo(new[] { prefabPath, scenePath }));
        }

        [Test]
        public void DiscoverReportsInvalidAssetsPathsAndIgnoresUnsupportedFiles()
        {
            EnsureFolder(RootDirectory);
            var textPath = RootDirectory + "/Readme.asset";
            AssetDatabase.CreateAsset(new TextAsset("test"), textPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = AuditTargetDiscovery.Discover(new[]
            {
                textPath,
                "Packages/NotAnAsset",
                RootDirectory + "/Missing"
            });

            Assert.That(result.Targets, Is.Empty);
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Code),
                Is.EqualTo(new[]
                {
                    "TARGET_PATH_NOT_FOUND",
                    "TARGET_PATH_OUTSIDE_ASSETS"
                }));
        }

        [Test]
        public void DiscoverReturnsEmptyResultForNullOrEmptyInput()
        {
            Assert.That(AuditTargetDiscovery.Discover(null).Targets, Is.Empty);
            Assert.That(
                AuditTargetDiscovery.Discover(Array.Empty<string>()).Diagnostics,
                Is.Empty);
        }

        [Test]
        public void DiscoverDoesNotModifyTargetFiles()
        {
            var scenePath = CreateScene(RootDirectory + "/Readonly.unity");
            var prefabPath = CreatePrefab(RootDirectory + "/Readonly.prefab");
            var sceneBefore = File.ReadAllText(scenePath);
            var prefabBefore = File.ReadAllText(prefabPath);

            AuditTargetDiscovery.Discover(new[] { RootDirectory });

            Assert.That(File.ReadAllText(scenePath), Is.EqualTo(sceneBefore));
            Assert.That(File.ReadAllText(prefabPath), Is.EqualTo(prefabBefore));
        }

        private static string CreateScene(string path)
        {
            EnsureSavedTask12Anchor();
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True,
                "Unity could not create the Task12 Scene fixture.");
            var sceneHandle = scene.handle.GetRawData();
            CreatedTask12SceneHandles.Add(sceneHandle);
            try
            {
                Assert.That(
                    EditorSceneManager.SaveScene(scene, path),
                    Is.True,
                    "Unity could not save the Task12 Scene fixture.");
                AssetDatabase.Refresh();
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(path),
                    Is.Not.Null);
                CreatedTask12Paths.Add(path);
                scene = SceneManager.GetSceneByPath(path);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                CreatedTask12SceneHandles.Remove(sceneHandle);
                sceneHandle = scene.handle.GetRawData();
                CreatedTask12SceneHandles.Add(sceneHandle);
                return path;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        ReplaceLastTask12SceneWithAnchor(scene);
                    }
                    else
                    {
                        Assert.That(
                            EditorSceneManager.CloseScene(scene, true),
                            Is.True,
                            "Unity could not close the Task12 Scene fixture.");
                        CreatedTask12SceneHandles.Remove(sceneHandle);
                    }
                }
            }
        }

        private static string CreatePrefab(string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Assert.That(prefab, Is.Not.Null);
            return path;
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

            if (string.Equals(assetPath, RootDirectory, StringComparison.Ordinal) &&
                AssetDatabase.IsValidFolder(assetPath))
            {
                CreatedTask12Paths.Add(RootDirectory);
            }
        }

        private static void CleanupRootDirectory()
        {
            CloseTask12Scenes();
            var loadedAnchor = FindTrackedTask12Scene(AnchorPath);
            if (loadedAnchor.IsValid() && loadedAnchor.isLoaded)
            {
                var oldHandle = loadedAnchor.handle.GetRawData();
                var replacement = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Assert.That(
                    replacement.IsValid() && replacement.isLoaded,
                    Is.True,
                    "Unity could not replace the last Task12 fixture Scene.");
                CreatedTask12SceneHandles.Remove(oldHandle);
            }

            DeleteAssetIfPresent(AnchorPath);
            if (AssetDatabase.IsValidFolder(RootDirectory))
            {
                Assert.That(
                    AssetDatabase.DeleteAsset(RootDirectory),
                    Is.True,
                    "Unity could not delete the exact Task12 fixture directory.");
            }
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.IsValidFolder(RootDirectory),
                Is.False,
                "Task12 fixture directory remained after exact cleanup.");
            Assert.That(
                Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Any(IsLoadedTask12Path),
                Is.False,
                "A Task12 fixture Scene remained loaded after exact cleanup.");
            CreatedTask12Paths.Clear();
            CreatedTask12SceneHandles.Clear();
            RestoreSceneTemplateSettings();
        }

        private static void EnsureSavedTask12Anchor()
        {
            EnsureNoDirtyNonTask12Scenes();
            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded)
                .ToList();
            var untitledScenes = loadedScenes
                .Where(scene => string.IsNullOrEmpty(scene.path))
                .ToList();
            if (loadedScenes.Any(scene => scene.isDirty && !IsTrackedTask12Scene(scene)))
            {
                throw new InvalidOperationException(
                    "Task12 cannot create a fixture while a non-Task12 Scene is dirty.");
            }

            if (untitledScenes.Count == 0)
            {
                if (loadedScenes.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Task12 cannot create a fixture because no loaded Scene is available.");
                }

                return;
            }

            if (untitledScenes.Count != 1 ||
                !IsSafeUntitledPlaceholder(untitledScenes[0]))
            {
                throw new InvalidOperationException(
                    "Task12 cannot create a fixture while an unsafe untitled Scene is loaded.");
            }

            var anchor = untitledScenes[0];
            EnsureFolder(FixtureRootDirectory);
            Assert.That(
                EditorSceneManager.SaveScene(anchor, AnchorPath),
                Is.True,
                "Unity could not save the Task12 fixture anchor.");
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                Is.Not.Null);
            CreatedTask12Paths.Add(AnchorPath);
            var savedAnchor = SceneManager.GetSceneByPath(AnchorPath);
            Assert.That(savedAnchor.IsValid() && savedAnchor.isLoaded, Is.True);
            CreatedTask12SceneHandles.Add(savedAnchor.handle.GetRawData());
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

        private static void CloseTask12Scenes()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!IsTrackedTask12Scene(scene))
                {
                    continue;
                }

                if (SceneManager.sceneCount == 1)
                {
                    ReplaceLastTask12SceneWithAnchor(scene);
                    continue;
                }

                var sceneHandle = scene.handle.GetRawData();
                Assert.That(
                    EditorSceneManager.CloseScene(scene, true),
                    Is.True,
                    "Unity could not close the Task12 fixture Scene.");
                CreatedTask12SceneHandles.Remove(sceneHandle);
            }
        }

        private static void ReplaceLastTask12SceneWithAnchor(Scene scene)
        {
            var oldHandle = scene.handle.GetRawData();
            EnsureFolder(FixtureRootDirectory);
            var replacement = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.That(
                replacement.IsValid() && replacement.isLoaded,
                Is.True,
                "Unity could not replace the last Task12 fixture Scene.");
            Assert.That(
                EditorSceneManager.SaveScene(replacement, AnchorPath),
                Is.True,
                "Unity could not save the Task12 fixture anchor.");
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                Is.Not.Null);
            CreatedTask12Paths.Add(AnchorPath);
            CreatedTask12SceneHandles.Remove(oldHandle);
            var savedAnchor = SceneManager.GetSceneByPath(AnchorPath);
            Assert.That(savedAnchor.IsValid() && savedAnchor.isLoaded, Is.True);
            CreatedTask12SceneHandles.Add(savedAnchor.handle.GetRawData());
        }

        private static bool IsTrackedTask12Scene(Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                !CreatedTask12Paths.Contains(scene.path))
            {
                return false;
            }

            if (string.Equals(scene.path, AnchorPath, StringComparison.Ordinal))
            {
                return true;
            }

            return CreatedTask12SceneHandles.Contains(scene.handle.GetRawData());
        }

        private static Scene FindTrackedTask12Scene(string path)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (IsTrackedTask12Scene(scene) &&
                    string.Equals(scene.path, path, StringComparison.Ordinal))
                {
                    return scene;
                }
            }

            return default(Scene);
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null)
            {
                CreatedTask12Paths.Remove(assetPath);
                return;
            }

            Assert.That(
                AssetDatabase.DeleteAsset(assetPath),
                Is.True,
                "Unity could not delete the exact Task12 fixture asset '" +
                assetPath +
                "'.");
            CreatedTask12Paths.Remove(assetPath);
        }

        private static bool IsLoadedTask12Path(Scene scene)
        {
            return scene.IsValid() &&
                scene.isLoaded &&
                (string.Equals(scene.path, RootDirectory, StringComparison.Ordinal) ||
                 scene.path.StartsWith(RootDirectory + "/", StringComparison.Ordinal));
        }

        private static void EnsureNoDirtyNonTask12Scenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    IsTrackedTask12Scene(scene) ||
                    !scene.isDirty)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "Task12 will not discard a dirty non-Task12 Scene at '" +
                    (string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path) +
                    "'.");
            }
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
