using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证扫描输入的路径校验、Scene/Prefab 发现、去重和只读行为。
    public sealed class AuditTargetDiscoveryTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task12";
        private static readonly bool HadSceneTemplateSettings =
            File.Exists("ProjectSettings/SceneTemplateSettings.json");

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
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            return path;
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
        }

        private static void CleanupRootDirectory()
        {
            AssetDatabase.DeleteAsset(RootDirectory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!HadSceneTemplateSettings &&
                File.Exists("ProjectSettings/SceneTemplateSettings.json"))
            {
                File.Delete("ProjectSettings/SceneTemplateSettings.json");
            }
        }
    }
}
