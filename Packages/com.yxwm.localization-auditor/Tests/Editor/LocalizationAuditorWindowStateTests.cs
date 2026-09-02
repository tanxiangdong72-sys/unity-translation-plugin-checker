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
    // 验证窗口状态与运行服务，不依赖 IMGUI 绘制细节。
    public sealed class LocalizationAuditorWindowStateTests
    {
        private const string FixtureParentDirectory =
            "Assets/LocalizationAuditorTestFixtures";

        private string _fixtureDirectory;
        private string _scenePath;
        private string _prefabPath;
        private string _anchorPath;

        [SetUp]
        public void SetUp()
        {
            _fixtureDirectory = FixtureParentDirectory + "/Task17_" +
                Guid.NewGuid().ToString("N");
            _scenePath = _fixtureDirectory + "/Target.unity";
            _prefabPath = _fixtureDirectory + "/Target.prefab";
            _anchorPath = FixtureParentDirectory + "/Task17Anchor_" +
                Guid.NewGuid().ToString("N") + ".unity";
        }

        [TearDown]
        public void TearDown()
        {
            ReplaceFixtureAnchorIfLoaded();
            if (AssetDatabase.IsValidFolder(_fixtureDirectory))
            {
                Assert.That(AssetDatabase.DeleteAsset(_fixtureDirectory), Is.True);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(_anchorPath) != null)
            {
                Assert.That(AssetDatabase.DeleteAsset(_anchorPath), Is.True);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void WindowCanBeCreatedAndStateStartsWithoutTargetsOrRun()
        {
            var window = EditorWindow.CreateInstance<LocalizationAuditorWindow>();
            try
            {
                var state = new LocalizationAuditorWindowState();

                Assert.That(window, Is.Not.Null);
                Assert.That(state.Targets, Is.Empty);
                Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.NotRun));
                Assert.That(state.LastReport, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RefreshTargetsDiscoversAndDeduplicatesSceneAndPrefabFixtures()
        {
            CreateFixtureTargets();
            var state = new LocalizationAuditorWindowState();

            state.RefreshTargets(new[] { _fixtureDirectory, _prefabPath });

            Assert.That(
                state.Targets.Select(target => target.AssetPath),
                Is.EqualTo(new[] { _prefabPath, _scenePath }));
            Assert.That(state.SceneTargetCount, Is.EqualTo(1));
            Assert.That(state.PrefabTargetCount, Is.EqualTo(1));
            Assert.That(state.StatusMessage, Does.Contain("2"));
        }

        [Test]
        public void RunAuditBuildsRequestWithAllDefaultRulesAndExposesReportSummary()
        {
            CreateFixtureTargets();
            AuditRequest capturedRequest = null;
            var state = new LocalizationAuditorWindowState(
                runAudit: request =>
                {
                    capturedRequest = request;
                    return new AuditReport(
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        AuditRunStatus.Completed,
                        request.AssetPaths.Count,
                        new[]
                        {
                            CreateIssue(AuditSeverity.Error),
                            CreateIssue(AuditSeverity.Warning),
                            CreateIssue(AuditSeverity.NotVerified)
                        });
                });

            state.RefreshTargets(new[] { _fixtureDirectory });
            state.RunAudit();

            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.Completed));
            Assert.That(state.LastRequest, Is.SameAs(capturedRequest));
            Assert.That(capturedRequest.AssetPaths, Is.EqualTo(new[] { _prefabPath, _scenePath }));
            Assert.That(capturedRequest.EnabledRuleIds, Is.EqualTo(
                LocalizationAuditorWindowState.DefaultRuleIds));
            Assert.That(capturedRequest.EnabledRuleIds, Is.EqualTo(new[]
            {
                EmptyTranslationRule.RuleId,
                LocalizedStringReferenceRule.RuleId,
                StringTableCompletenessRule.RuleId,
                TmpFontCoverageRule.RuleId
            }));
            Assert.That(state.LastReport.ScannedAssetCount, Is.EqualTo(2));
            Assert.That(state.ErrorCount, Is.EqualTo(1));
            Assert.That(state.WarningCount, Is.EqualTo(1));
            Assert.That(state.NotVerifiedCount, Is.EqualTo(1));
            Assert.That(state.DiagnosticCount, Is.EqualTo(0));
        }

        [Test]
        public void RefreshTargetsFailureClearsPreviousTargetsRequestAndReport()
        {
            var target = new AuditTarget("Assets/Task17Target.prefab", AuditTargetKind.Prefab);
            var shouldFailDiscovery = false;
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ =>
                {
                    if (shouldFailDiscovery)
                    {
                        throw new InvalidOperationException("Injected discovery failure.");
                    }

                    return new AuditTargetDiscoveryResult(
                        new[] { target },
                        Array.Empty<AuditDiagnostic>());
                },
                runAudit: request => new AuditReport(
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    AuditRunStatus.Completed,
                    request.AssetPaths.Count,
                    Array.Empty<AuditIssue>()));

            state.RefreshTargets(new[] { "Assets" });
            state.RunAudit();
            Assert.That(state.Targets, Has.Count.EqualTo(1));
            Assert.That(state.LastRequest, Is.Not.Null);
            Assert.That(state.LastReport, Is.Not.Null);

            shouldFailDiscovery = true;
            state.RefreshTargets(new[] { "Assets" });

            Assert.That(state.Targets, Is.Empty);
            Assert.That(state.LastRequest, Is.Null);
            Assert.That(state.LastReport, Is.Null);
            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.Failed));
            Assert.That(state.StatusMessage, Does.Contain("Injected discovery failure."));
        }

        [Test]
        public void RefreshTargetsSuccessClearsPreviousRequestAndReportAndResetsRunState()
        {
            var initialTarget = new AuditTarget(
                "Assets/Task17InitialTarget.prefab",
                AuditTargetKind.Prefab);
            var refreshedTarget = new AuditTarget(
                "Assets/Task17RefreshedTarget.prefab",
                AuditTargetKind.Prefab);
            var discoveryCallCount = 0;
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ =>
                {
                    discoveryCallCount++;
                    return new AuditTargetDiscoveryResult(
                        discoveryCallCount == 1
                            ? new[] { initialTarget }
                            : discoveryCallCount == 2
                                ? Array.Empty<AuditTarget>()
                                : new[] { refreshedTarget },
                        Array.Empty<AuditDiagnostic>());
                },
                runAudit: request => new AuditReport(
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    AuditRunStatus.Completed,
                    request.AssetPaths.Count,
                    Array.Empty<AuditIssue>()));

            state.RefreshTargets(new[] { "Assets" });
            state.RunAudit();
            Assert.That(state.LastRequest, Is.Not.Null);
            Assert.That(state.LastReport, Is.Not.Null);
            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.Completed));

            state.RefreshTargets(new[] { "Assets" });

            Assert.That(state.Targets, Is.Empty);
            Assert.That(state.LastRequest, Is.Null);
            Assert.That(state.LastReport, Is.Null);
            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.NotRun));
            Assert.That(state.StatusMessage, Is.EqualTo("No audit targets found."));

            state.RefreshTargets(new[] { "Assets" });

            Assert.That(
                state.Targets.Select(target => target.AssetPath),
                Is.EqualTo(new[] { refreshedTarget.AssetPath }));
            Assert.That(state.LastRequest, Is.Null);
            Assert.That(state.LastReport, Is.Null);
            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.NotRun));
            Assert.That(state.StatusMessage, Is.EqualTo("Found 1 audit target(s)."));
        }

        [Test]
        public void RunAuditWithoutTargetsKeepsObservableNotRunState()
        {
            var state = new LocalizationAuditorWindowState();

            Assert.DoesNotThrow(() => state.RunAudit());

            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.NotRun));
            Assert.That(state.LastReport, Is.Null);
            Assert.That(state.StatusMessage, Does.Contain("No audit targets"));
        }

        [Test]
        public void RunAuditFailureIsCapturedInObservableState()
        {
            var target = new AuditTarget("Assets/Target.prefab", AuditTargetKind.Prefab);
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ => new AuditTargetDiscoveryResult(
                    new[] { target },
                    Array.Empty<AuditDiagnostic>()),
                runAudit: _ => throw new InvalidOperationException("Expected failure."));

            state.RefreshTargets(new[] { "Assets" });
            Assert.DoesNotThrow(() => state.RunAudit());

            Assert.That(state.RunState, Is.EqualTo(LocalizationAuditorWindowRunState.Failed));
            Assert.That(state.LastReport, Is.Null);
            Assert.That(state.StatusMessage, Does.Contain("Expected failure."));
        }

        private void CreateFixtureTargets()
        {
            EnsureFolder(_fixtureDirectory);
            EnsureSavedFixtureAnchor();
            var root = new GameObject("Task17Prefab");
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, _prefabPath), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, _scenePath), Is.True);
            }
            finally
            {
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
            }

            AssetDatabase.Refresh();
            Assert.That(File.Exists(_scenePath), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath), Is.Not.Null);
        }

        private void EnsureSavedFixtureAnchor()
        {
            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded)
                .ToArray();
            if (loadedScenes.Any(scene => scene.isDirty))
            {
                throw new InvalidOperationException(
                    "Task17 cannot create a fixture while a loaded Scene is dirty.");
            }

            var untitledScenes = loadedScenes
                .Where(scene => string.IsNullOrEmpty(scene.path))
                .ToArray();
            if (untitledScenes.Length == 0)
            {
                return;
            }

            if (untitledScenes.Length != 1)
            {
                throw new InvalidOperationException(
                    "Task17 cannot create a fixture with multiple untitled Scenes loaded.");
            }

            Assert.That(
                EditorSceneManager.SaveScene(untitledScenes[0], _anchorPath),
                Is.True,
                "Unity could not save the Task17 fixture anchor.");
            AssetDatabase.Refresh();
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(_anchorPath),
                Is.Not.Null);
        }

        private void ReplaceFixtureAnchorIfLoaded()
        {
            var anchor = SceneManager.GetSceneByPath(_anchorPath);
            if (!anchor.IsValid() || !anchor.isLoaded)
            {
                return;
            }

            var loadedSceneCount = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Count(scene => scene.IsValid() && scene.isLoaded);
            if (loadedSceneCount > 1)
            {
                Assert.That(
                    EditorSceneManager.CloseScene(anchor, true),
                    Is.True,
                    "Unity could not close the Task17 fixture anchor.");
                return;
            }

            var replacement = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.That(
                replacement.IsValid() && replacement.isLoaded,
                Is.True,
                "Unity could not replace the Task17 fixture anchor.");
        }

        private static AuditIssue CreateIssue(AuditSeverity severity)
        {
            return new AuditIssue(
                "TASK17_TEST",
                severity,
                "Test issue.",
                "Fix it.",
                new AuditIssueLocation());
        }

        private static void EnsureFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + segments[index]))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current += "/" + segments[index];
            }
        }
    }
}
