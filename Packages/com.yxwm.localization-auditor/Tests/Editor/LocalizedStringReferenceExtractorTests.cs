using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证序列化 LocalizedString 引用的只读提取和 fixture 清理。
    public sealed class LocalizedStringReferenceExtractorTests
    {
        private static readonly string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task15_" +
            Guid.NewGuid().ToString("N");
        private const string FixtureRootDirectory =
            "Assets/LocalizationAuditorTestFixtures/";
        private static readonly string AnchorPath = RootDirectory + "/Anchor.unity";
        private static readonly string ScenePath = RootDirectory + "/References.unity";
        private static readonly string PrefabPath = RootDirectory + "/References.prefab";
        private static readonly string ForeignDirtyScenePath =
            FixtureRootDirectory + "ForeignDirty_" + Guid.NewGuid().ToString("N") + ".unity";
        private const string SceneTemplateSettingsPath =
            "ProjectSettings/SceneTemplateSettings.json";
        private static readonly byte[] OriginalSceneTemplateSettings =
            ReadSceneTemplateSettings();
        private static readonly HashSet<string> CreatedFixturePaths =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<ulong> CreatedSceneHandles = new HashSet<ulong>();

        [SetUp]
        public void SetUp()
        {
            PrepareSceneEnvironment();
            CloseFixtureScene();
            CleanupFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            PrepareSceneEnvironment();
            CloseFixtureScene();
            CleanupFixtures();
        }

        [Test]
        public void ExtractFromSceneReadsNamedReferencesAndNestedEvent()
        {
            CreateSceneFixture();

            using (var scope = SceneReadOnlyScope.Open(ScenePath))
            {
                var references = LocalizedStringReferenceExtractor.ExtractFromScene(
                    ScenePath,
                    scope.Scene);

                Assert.That(references, Is.InstanceOf<IReadOnlyList<LocalizedStringReferenceSnapshot>>());
                Assert.That(references, Has.Count.EqualTo(11));
                Assert.That(
                    references.Select(reference =>
                        reference.ObjectPath + "|" +
                        reference.ComponentType + "|" +
                        reference.SerializedPropertyPath),
                    Is.EqualTo(references.Select(reference =>
                            reference.ObjectPath + "|" +
                            reference.ComponentType + "|" +
                            reference.SerializedPropertyPath)
                        .OrderBy(path => path, StringComparer.Ordinal)));

                var custom = references.Single(reference =>
                    reference.ObjectPath == "SceneRoot/Named" &&
                    reference.SerializedPropertyPath == "Reference.m_TableReference");
                Assert.That(custom.AssetPath, Is.EqualTo(ScenePath));
                Assert.That(custom.ObjectPath, Is.EqualTo("SceneRoot/Named"));
                Assert.That(custom.ComponentType, Is.EqualTo(typeof(Task15ReferenceFixture).FullName));
                Assert.That(custom.SerializedPropertyPath, Is.EqualTo("Reference.m_TableReference"));
                Assert.That(custom.TableReference.RawValue, Is.EqualTo("Task15 Strings"));
                Assert.That(custom.TableReference.Type, Is.EqualTo(TableReferenceSnapshotType.Name));
                Assert.That(custom.TableEntryReference.RawKey, Is.EqualTo("GREETING"));
                Assert.That(custom.TableEntryReference.RawKeyId, Is.EqualTo(0));
                Assert.That(custom.TableEntryReference.Type, Is.EqualTo(TableEntryReferenceSnapshotType.Name));

                var eventReference = references.Single(reference =>
                    reference.ObjectPath == "SceneRoot/Event");
                Assert.That(
                    eventReference.SerializedPropertyPath,
                    Is.EqualTo("m_StringReference.m_TableReference"));
                Assert.That(eventReference.ObjectPath, Is.EqualTo("SceneRoot/Event"));

                var nested = references.Single(reference =>
                    reference.ObjectPath == "SceneRoot/Nested" &&
                    reference.SerializedPropertyPath == "Group.Nested.m_TableReference");
                Assert.That(nested.ComponentType, Is.EqualTo(typeof(Task15ReferenceFixture).FullName));
                Assert.That(nested.SerializedPropertyPath, Is.EqualTo("Group.Nested.m_TableReference"));
                Assert.That(nested.TableReference.RawValue, Is.EqualTo("Nested Strings"));
                Assert.That(nested.TableEntryReference.RawKey, Is.EqualTo("NESTED"));

                var listReferences = references
                    .Where(reference => reference.ObjectPath == "SceneRoot/Nested" &&
                        reference.SerializedPropertyPath.StartsWith(
                        "References.Array.data[", StringComparison.Ordinal))
                    .ToList();
                Assert.That(listReferences, Has.Count.EqualTo(2));
                Assert.That(listReferences.Select(reference => reference.ObjectPath),
                    Is.All.EqualTo("SceneRoot/Nested"));
                Assert.That(listReferences.Select(reference => reference.SerializedPropertyPath),
                    Is.EqualTo(new[]
                    {
                        "References.Array.data[0].m_TableReference",
                        "References.Array.data[1].m_TableReference"
                    }));
                Assert.That(listReferences.Select(reference => reference.TableReference.RawValue),
                    Is.EqualTo(new[] { "List Strings", "List Strings" }));
                Assert.That(listReferences.Select(reference => reference.TableEntryReference.RawKey),
                    Is.EqualTo(new[] { "LIST_FIRST", "LIST_SECOND" }));

                var arrayReferences = references
                    .Where(reference => reference.ObjectPath == "SceneRoot/Nested" &&
                        reference.SerializedPropertyPath.StartsWith(
                        "ArrayReferences.Array.data[", StringComparison.Ordinal))
                    .ToList();
                Assert.That(arrayReferences, Has.Count.EqualTo(2));
                Assert.That(arrayReferences.Select(reference => reference.ObjectPath),
                    Is.All.EqualTo("SceneRoot/Nested"));
                Assert.That(arrayReferences.Select(reference => reference.SerializedPropertyPath),
                    Is.EqualTo(new[]
                    {
                        "ArrayReferences.Array.data[0].m_TableReference",
                        "ArrayReferences.Array.data[1].m_TableReference"
                    }));
                Assert.That(arrayReferences.Select(reference => reference.TableEntryReference.RawKey),
                    Is.EqualTo(new[] { "ARRAY_FIRST", "ARRAY_SECOND" }));
                Assert.That(arrayReferences.Select(reference => reference.TableReference.RawValue),
                    Is.EqualTo(new[] { "Array Strings", "Array Strings" }));
            }
        }

        [Test]
        public void ExtractFromPrefabIncludesInactiveGuidAndIdReference()
        {
            CreatePrefabFixture();
            var before = File.ReadAllBytes(PrefabPath);

            using (var scope = PrefabReadOnlyScope.Open(PrefabPath))
            {
                var references = LocalizedStringReferenceExtractor.ExtractFromGameObject(
                    PrefabPath,
                    scope.Root);

                Assert.That(references, Has.Count.EqualTo(1));
                var reference = references[0];
                Assert.That(reference.ObjectPath, Is.EqualTo(scope.Root.name + "/Inactive"));
                Assert.That(reference.TableReference.RawValue, Is.EqualTo(
                    "GUID:0123456789abcdef0123456789abcdef"));
                Assert.That(reference.TableReference.Type, Is.EqualTo(TableReferenceSnapshotType.Guid));
                Assert.That(reference.TableReference.IsEmpty, Is.False);
                Assert.That(reference.TableEntryReference.RawKeyId, Is.EqualTo(42));
                Assert.That(reference.TableEntryReference.Type, Is.EqualTo(TableEntryReferenceSnapshotType.Id));
                Assert.That(reference.TableEntryReference.IsEmpty, Is.False);
            }

            Assert.That(File.ReadAllBytes(PrefabPath), Is.EqualTo(before));
        }

        [Test]
        public void ExtractDistinguishesEmptyReferencesAndPreservesPropertyPaths()
        {
            CreateSceneFixture();

            using (var scope = SceneReadOnlyScope.Open(ScenePath))
            {
                var references = LocalizedStringReferenceExtractor.ExtractFromScene(
                    ScenePath,
                    scope.Scene);
                var empty = references.Single(reference =>
                    reference.ObjectPath == "SceneRoot/Empty" &&
                    reference.SerializedPropertyPath == "Reference.m_TableReference");

                Assert.That(empty.TableReference.Type, Is.EqualTo(TableReferenceSnapshotType.Empty));
                Assert.That(empty.TableReference.IsEmpty, Is.True);
                Assert.That(empty.TableEntryReference.Type, Is.EqualTo(TableEntryReferenceSnapshotType.Empty));
                Assert.That(empty.TableEntryReference.IsEmpty, Is.True);
                Assert.That(empty.SerializedPropertyPath, Does.EndWith("m_TableReference"));
            }
        }

        [Test]
        public void ExtractIsStableAndDoesNotChangeSceneBytes()
        {
            CreateSceneFixture();
            var before = File.ReadAllBytes(ScenePath);

            IReadOnlyList<LocalizedStringReferenceSnapshot> first;
            IReadOnlyList<LocalizedStringReferenceSnapshot> second;
            using (var scope = SceneReadOnlyScope.Open(ScenePath))
            {
                first = LocalizedStringReferenceExtractor.ExtractFromScene(ScenePath, scope.Scene);
                second = LocalizedStringReferenceExtractor.ExtractFromScene(ScenePath, scope.Scene);
            }

            Assert.That(second.Select(reference => reference.SerializedPropertyPath),
                Is.EqualTo(first.Select(reference => reference.SerializedPropertyPath)));
            Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(before));

            CleanupFixtures();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath), Is.Null);
            Assert.That(
                AssetDatabase.IsValidFolder(RootDirectory),
                Is.False,
                "Task15 cleanup must remove its Anchor and exact random fixture directory.");
        }

        [Test]
        public void NullInvalidAndUnreferencedInputsReturnEmptyReadOnlyResults()
        {
            var emptyGameObjectResult =
                LocalizedStringReferenceExtractor.ExtractFromGameObject(null, null);
            Assert.That(emptyGameObjectResult, Is.Empty);
            Assert.That(emptyGameObjectResult, Is.InstanceOf<IReadOnlyList<LocalizedStringReferenceSnapshot>>());

            var invalidSceneResult =
                LocalizedStringReferenceExtractor.ExtractFromScene("Assets/Missing.unity", default(Scene));
            Assert.That(invalidSceneResult, Is.Empty);

            var gameObject = new GameObject("NoReference");
            try
            {
                var result = LocalizedStringReferenceExtractor.ExtractFromGameObject(
                    "Assets/NoReference.prefab",
                    gameObject);
                Assert.That(result, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            var foreignScene = CreateForeignDirtyScene();
            try
            {
                Assert.Throws<InvalidOperationException>(() => PrepareSceneEnvironment());
                Assert.That(foreignScene.IsValid() && foreignScene.isLoaded, Is.True);
                Assert.That(foreignScene.isDirty, Is.True);
            }
            finally
            {
                if (foreignScene.IsValid() && foreignScene.isLoaded)
                {
                    if (SceneManager.sceneCount == 1)
                    {
                        EnsureFolder(RootDirectory);
                        var replacement = EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            NewSceneMode.Single);
                        Assert.That(
                            replacement.IsValid() && replacement.isLoaded,
                            Is.True,
                            "The test-created foreign Scene could not be replaced by a clean anchor.");
                        Assert.That(
                            EditorSceneManager.SaveScene(replacement, AnchorPath),
                            Is.True,
                            "The test-created foreign Scene could not be replaced by the Task15 anchor.");
                        AssetDatabase.Refresh();
                        Assert.That(
                            AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                            Is.Not.Null);
                        CreatedFixturePaths.Add(AnchorPath);
                        var savedAnchor = SceneManager.GetSceneByPath(AnchorPath);
                        Assert.That(savedAnchor.IsValid() && savedAnchor.isLoaded, Is.True);
                    }
                    else
                    {
                        Assert.That(
                            EditorSceneManager.CloseScene(foreignScene, false),
                            Is.True,
                            "The test-created foreign Scene should close without discarding changes.");
                    }
                }

                DeleteAssetIfPresent(ForeignDirtyScenePath);
                AssetDatabase.Refresh();
            }
        }

        private static Scene CreateForeignDirtyScene()
        {
            EnsureFolder(FixtureRootDirectory);
            EnsureFolder(RootDirectory);
            EnsureSavedFixtureAnchor();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(
                EditorSceneManager.SaveScene(scene, ForeignDirtyScenePath),
                Is.True,
                "The foreign Scene fixture should be saved before it is made dirty.");
            CreatedFixturePaths.Add(ForeignDirtyScenePath);
            var marker = new GameObject("ForeignDirtyMarker");
            SceneManager.MoveGameObjectToScene(marker, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return scene;
        }

        private static void CreateSceneFixture()
        {
            PrepareSceneEnvironment();
            EnsureFolder(RootDirectory);
            EnsureSavedFixtureAnchor();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var sceneHandle = scene.handle.GetRawData();
            CreatedSceneHandles.Add(sceneHandle);
            try
            {
                var root = new GameObject("SceneRoot");
                SceneManager.MoveGameObjectToScene(root, scene);

                CreateReferenceObject(root, "Named", "Task15 Strings", "GREETING");
                CreateReferenceObject(root, "Empty", null, null);
                CreateNestedReferenceObject(root);
                var eventObject = new GameObject("Event");
                eventObject.transform.SetParent(root.transform);
                eventObject.SetActive(false);
                var localizeEvent = eventObject.AddComponent<LocalizeStringEvent>();
                localizeEvent.StringReference.SetReference("Event Strings", "EVENT_KEY");

                Assert.That(EditorSceneManager.SaveScene(scene, ScenePath), Is.True);
                AssetDatabase.Refresh();
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
                CreatedFixturePaths.Add(ScenePath);
                CreatedSceneHandles.Remove(sceneHandle);
                scene = SceneManager.GetSceneByPath(ScenePath);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                sceneHandle = scene.handle.GetRawData();
                CreatedSceneHandles.Add(sceneHandle);
            }
            finally
            {
                if (scene.IsValid() &&
                    scene.isLoaded &&
                    SceneManager.sceneCount > 1)
                {
                    Assert.That(
                        EditorSceneManager.CloseScene(scene, true),
                        Is.True,
                        "Unity could not close the Task15 Scene created by this test.");
                    CreatedSceneHandles.Remove(sceneHandle);
                }
            }
        }

        private static void CreatePrefabFixture()
        {
            EnsureFolder(RootDirectory);
            var root = new GameObject("PrefabRoot");
            var inactive = new GameObject("Inactive");
            inactive.transform.SetParent(root.transform);
            inactive.SetActive(false);
            var localizeEvent = inactive.AddComponent<LocalizeStringEvent>();
            TableReference tableReference =
                Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
            TableEntryReference entryReference = 42L;
            localizeEvent.StringReference.SetReference(
                tableReference,
                entryReference);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Not.Null);
            CreatedFixturePaths.Add(PrefabPath);
        }

        private static GameObject CreateReferenceObject(
            GameObject root,
            string name,
            string table,
            string key)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            var fixture = child.AddComponent<Task15ReferenceFixture>();
            if (table != null || key != null)
            {
                fixture.Reference.SetReference(table, key);
            }

            return child;
        }

        private static void CreateNestedReferenceObject(GameObject root)
        {
            var child = new GameObject("Nested");
            child.transform.SetParent(root.transform);
            var fixture = child.AddComponent<Task15ReferenceFixture>();
            fixture.Group = new Task15ReferenceGroup();
            fixture.Group.Nested.SetReference("Nested Strings", "NESTED");
            fixture.References = new List<LocalizedString>
            {
                CreateReference("List Strings", "LIST_FIRST"),
                CreateReference("List Strings", "LIST_SECOND")
            };
            fixture.ArrayReferences = new[]
            {
                CreateReference("Array Strings", "ARRAY_FIRST"),
                CreateReference("Array Strings", "ARRAY_SECOND")
            };
        }

        private static LocalizedString CreateReference(string table, string key)
        {
            var reference = new LocalizedString();
            reference.SetReference(table, key);
            return reference;
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
                CreatedFixturePaths.Add(RootDirectory);
            }
        }

        private static void CloseFixtureScene()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    !IsTrackedTask15Scene(scene))
                {
                    continue;
                }

                if (SceneManager.sceneCount == 1)
                {
                    ReplaceLastTask15SceneWithSavedAnchor(scene);
                    continue;
                }

                var fixtureHandleToRemove = scene.handle.GetRawData();
                Assert.That(
                    EditorSceneManager.CloseScene(scene, true),
                    Is.True,
                    "Unity could not close the Task15 Scene created by this test.");
                CreatedSceneHandles.Remove(fixtureHandleToRemove);
            }
        }

        private static void PrepareSceneEnvironment()
        {
            CloseFixtureScene();
            EnsureNoDirtyNonFixtureScenes();
        }

        private static void EnsureSavedFixtureAnchor()
        {
            EnsureNoDirtyNonFixtureScenes();
            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded)
                .ToList();
            var untitledScenes = loadedScenes
                .Where(scene => string.IsNullOrEmpty(scene.path))
                .ToList();

            if (loadedScenes.Any(scene => scene.isDirty))
            {
                throw new InvalidOperationException(
                    "Task15 cannot create a fixture while a loaded Scene is dirty.");
            }

            if (untitledScenes.Count == 0)
            {
                if (loadedScenes.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Task15 cannot create a fixture because no loaded Scene is available.");
                }

                return;
            }

            if (untitledScenes.Count != 1 ||
                !IsSafeUnityUntitledPlaceholder(untitledScenes[0]))
            {
                throw new InvalidOperationException(
                    "Task15 cannot create a fixture while an unsafe untitled Scene is loaded.");
            }

            var anchor = untitledScenes[0];
            Assert.That(
                EditorSceneManager.SaveScene(anchor, AnchorPath),
                Is.True,
                "Task15 could not save its safe untitled placeholder anchor.");
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                Is.Not.Null);
            CreatedFixturePaths.Add(AnchorPath);
            var savedAnchor = SceneManager.GetSceneByPath(AnchorPath);
            Assert.That(savedAnchor.IsValid() && savedAnchor.isLoaded, Is.True);
            CreatedSceneHandles.Add(savedAnchor.handle.GetRawData());
        }

        private static bool IsSafeUnityUntitledPlaceholder(Scene scene)
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

        private static void CleanupFixtures()
        {
            CloseFixtureScene();
            DeleteAssetIfPresent(ScenePath);
            DeleteAssetIfPresent(PrefabPath);
            var loadedAnchor = FindTrackedTask15Scene(AnchorPath);
            if (loadedAnchor.IsValid() && loadedAnchor.isLoaded)
            {
                var oldHandle = loadedAnchor.handle.GetRawData();
                var replacement = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Assert.That(
                    replacement.IsValid() && replacement.isLoaded,
                    Is.True,
                    "Task15 could not replace its last fixture Scene before deleting Anchor.");
                CreatedSceneHandles.Remove(oldHandle);
            }

            DeleteAssetIfPresent(AnchorPath);
            if (AssetDatabase.IsValidFolder(RootDirectory))
            {
                Assert.That(
                    AssetDatabase.DeleteAsset(RootDirectory),
                    Is.True,
                    "Unity could not delete the exact Task15 fixture directory.");
            }
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.IsValidFolder(RootDirectory),
                Is.False,
                "Task15 fixture directory remained after exact cleanup.");
            CreatedFixturePaths.Clear();
            CreatedSceneHandles.Clear();
            RestoreSceneTemplateSettings();
        }

        private static Scene FindTrackedTask15Scene(string path)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (IsTrackedTask15Scene(scene) &&
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
                CreatedFixturePaths.Remove(assetPath);
                return;
            }

            Assert.That(
                AssetDatabase.DeleteAsset(assetPath),
                Is.True,
                "Unity could not delete the exact Task15 fixture asset '" + assetPath + "'.");
            CreatedFixturePaths.Remove(assetPath);
        }

        private static void ReplaceLastTask15SceneWithSavedAnchor(Scene scene)
        {
            var oldHandle = scene.handle.GetRawData();
            EnsureFolder(RootDirectory);
            var replacement = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.That(
                replacement.IsValid() && replacement.isLoaded,
                Is.True,
                "Task15 could not replace its last fixture Scene during cleanup.");
            Assert.That(
                EditorSceneManager.SaveScene(replacement, AnchorPath),
                Is.True,
                "Task15 could not save the replacement fixture anchor during cleanup.");
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AnchorPath),
                Is.Not.Null);
            CreatedFixturePaths.Add(AnchorPath);
            CreatedSceneHandles.Remove(oldHandle);
            var savedAnchor = SceneManager.GetSceneByPath(AnchorPath);
            Assert.That(savedAnchor.IsValid() && savedAnchor.isLoaded, Is.True);
            CreatedSceneHandles.Add(savedAnchor.handle.GetRawData());
        }

        private static void EnsureNoDirtyNonFixtureScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    IsTrackedTask15Scene(scene) ||
                    !scene.isDirty)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "Task15 will not discard a dirty non-fixture Scene at '" +
                    (string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path) +
                    "'.");
            }
        }

        private static bool IsTrackedTask15Scene(Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                !CreatedFixturePaths.Contains(scene.path))
            {
                return false;
            }

            if (string.Equals(scene.path, AnchorPath, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(scene.path, ScenePath, StringComparison.Ordinal) &&
                CreatedSceneHandles.Contains(scene.handle.GetRawData());
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
                File.WriteAllBytes(SceneTemplateSettingsPath, OriginalSceneTemplateSettings);
            }
        }
    }

    public sealed class Task15ReferenceFixture : MonoBehaviour
    {
        public LocalizedString Reference = new LocalizedString();
        public Task15ReferenceGroup Group;
        public List<LocalizedString> References;
        public LocalizedString[] ArrayReferences;
    }

    [Serializable]
    public sealed class Task15ReferenceGroup
    {
        public LocalizedString Nested = new LocalizedString();
    }
}
