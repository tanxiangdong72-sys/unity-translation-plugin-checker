using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证报告列表的纯状态模型，不依赖 IMGUI 或资源文件。
    public sealed class AuditReportViewStateTests
    {
        private static readonly DateTimeOffset StartedAt =
            new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void FiltersByIssueStateAndSeverityWithoutChangingReportOrder()
        {
            var report = CreateReport(
                CreateIssue("warning-open", AuditSeverity.Warning, AuditIssueState.Open),
                CreateIssue("error-ignored", AuditSeverity.Error, AuditIssueState.Ignored),
                CreateIssue("error-open", AuditSeverity.Error, AuditIssueState.Open),
                CreateIssue("not-verified-open", AuditSeverity.NotVerified, AuditIssueState.Open));
            var view = new AuditReportViewState(report);

            Assert.That(
                view.GetIssues(AuditIssueFilter.All).Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "error-open", "error-ignored", "warning-open", "not-verified-open" }));
            Assert.That(
                view.GetIssues(AuditIssueFilter.Open).Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "error-open", "warning-open", "not-verified-open" }));
            Assert.That(
                view.GetIssues(new AuditIssueFilter(
                    null,
                    AuditIssueStateFilter.Ignored)).Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "error-ignored" }));
            Assert.That(
                view.GetIssues(new AuditIssueFilter(
                    AuditSeverity.Error,
                    AuditIssueStateFilter.All)).Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "error-open", "error-ignored" }));
            Assert.That(
                view.GetIssues(new AuditIssueFilter(
                    AuditSeverity.Warning,
                    AuditIssueStateFilter.Ignored)).Select(issue => issue.RuleId),
                Is.Empty);
            Assert.That(
                view.GetIssues(new AuditIssueFilter(
                    AuditSeverity.NotVerified,
                    AuditIssueStateFilter.Open)).Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "not-verified-open" }));
            Assert.That(
                report.Issues.Select(issue => issue.RuleId),
                Is.EqualTo(new[] { "error-open", "error-ignored", "warning-open", "not-verified-open" }));
            Assert.That(
                view.GetIssues(AuditIssueFilter.Open).Select(issue => issue.RuleId),
                Is.EqualTo(view.GetIssues(AuditIssueFilter.Open).Select(issue => issue.RuleId)));
        }

        [Test]
        public void ExposesAccurateSummaryAndFilteredCount()
        {
            var report = CreateReport(
                CreateIssue("error-open", AuditSeverity.Error, AuditIssueState.Open),
                CreateIssue("warning-open", AuditSeverity.Warning, AuditIssueState.Open),
                CreateIssue("not-verified", AuditSeverity.NotVerified, AuditIssueState.Open),
                CreateIssue("error-ignored", AuditSeverity.Error, AuditIssueState.Ignored),
                CreateIssue("ignored-warning", AuditSeverity.Warning, AuditIssueState.Ignored));
            var view = new AuditReportViewState(report);

            Assert.That(view.Summary.TotalIssueCount, Is.EqualTo(5));
            Assert.That(view.Summary.ErrorCount, Is.EqualTo(1));
            Assert.That(view.Summary.WarningCount, Is.EqualTo(1));
            Assert.That(view.Summary.NotVerifiedCount, Is.EqualTo(1));
            Assert.That(view.Summary.IgnoredCount, Is.EqualTo(2));
            Assert.That(view.Summary.DiagnosticCount, Is.EqualTo(1));
            Assert.That(view.FilteredIssueCount, Is.EqualTo(3));
            Assert.That(
                view.GetIssues(AuditIssueFilter.All),
                Is.InstanceOf<ReadOnlyCollection<AuditIssue>>());
        }

        [Test]
        public void EmptyViewStateProvidesClearStatusMessages()
        {
            Assert.That(AuditReportViewState.GetStatusMessage(null), Does.Contain("No audit"));
            Assert.That(
                AuditReportViewState.GetStatusMessage(CreateReport()),
                Does.Contain("No issues"));
            Assert.That(
                AuditReportViewState.GetStatusMessage(
                    CreateReport(CreateIssue("one", AuditSeverity.Error, AuditIssueState.Open)),
                    new AuditIssueFilter(AuditSeverity.Warning, AuditIssueStateFilter.Open)),
                Does.Contain("No issues match"));
        }

        private static AuditReport CreateReport(params AuditIssue[] issues)
        {
            return new AuditReport(
                StartedAt,
                StartedAt.AddSeconds(1),
                AuditRunStatus.Completed,
                1,
                issues,
                new[]
                {
                    new AuditDiagnostic("TEST", "Assets/Test.prefab", "Diagnostic")
                });
        }

        private static AuditIssue CreateIssue(
            string ruleId,
            AuditSeverity severity,
            AuditIssueState state)
        {
            return new AuditIssue(
                ruleId,
                severity,
                "A deliberately long test message that remains valid for the report view.",
                location: new AuditIssueLocation(
                    assetPath: "Assets/Long/Path/Test.prefab",
                    objectPath: "Root/Child",
                    propertyPath: "m_Text"),
                state: state);
        }
    }

    // 验证 headless 安全路径，并在图形 Editor 中保留真实 IMGUI 绘制覆盖。
    public sealed class LocalizationAuditorWindowReportSmokeTests
    {
        [Test]
        public void WindowOnGuiDoesNotThrowWithoutReportWhenInvokedThroughReflection()
        {
            var window = ScriptableObject.CreateInstance<LocalizationAuditorWindow>();
            try
            {
                InvokeOnEnable(window);
                SetWindowState(window, new LocalizationAuditorWindowState());
                Assert.DoesNotThrow(() => InvokeOnGui(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowOnGuiDoesNotThrowForLongTextReportWhenInvokedThroughReflection()
        {
            var report = new AuditReport(
                new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 2, 0, 0, 1, TimeSpan.Zero),
                AuditRunStatus.Completed,
                1,
                new[]
                {
                    new AuditIssue(
                        "LONG_TEXT",
                        AuditSeverity.Error,
                        "A deliberately long message that must wrap across multiple IMGUI lines " +
                        "instead of being clipped or forcing a single-line layout.",
                        location: new AuditIssueLocation(
                            assetPath: "Assets/Very/Long/Localization/Audit/Fixture/Path.prefab",
                            objectPath: "Root/Canvas/Panel/LocalizedLabel/DeeplyNestedObject",
                            propertyPath: string.Empty))
                });
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ => new AuditTargetDiscoveryResult(
                    new[]
                    {
                        new AuditTarget(
                            "Assets/Very/Long/Localization/Audit/Fixture/Path.prefab",
                            AuditTargetKind.Prefab)
                    },
                    Array.Empty<AuditDiagnostic>()),
                runAudit: _ => report);
            state.RefreshTargets(new[] { "Assets" });
            state.RunAudit();

            var window = ScriptableObject.CreateInstance<LocalizationAuditorWindow>();
            try
            {
                InvokeOnEnable(window);
                SetWindowState(window, state);
                Assert.DoesNotThrow(() => InvokeOnGui(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator GraphicsEditorWindowRendersLongReportAndDisabledLocateButton()
        {
            if (Application.isBatchMode ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "Requires a graphics-capable, non-batchmode Unity Editor.");
            }

            var location = new AuditIssueLocation(
                assetPath: "Assets/GraphicsSmoke/Missing.prefab",
                objectPath: "Root/Canvas/Panel/LocalizedLabel");
            Assert.That(AuditIssueLocator.CanLocate(location), Is.False);

            var report = new AuditReport(
                new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 2, 0, 0, 1, TimeSpan.Zero),
                AuditRunStatus.Completed,
                1,
                new[]
                {
                    new AuditIssue(
                        "GRAPHICS_SMOKE",
                        AuditSeverity.Error,
                        "A deliberately long graphics smoke message that must render " +
                        "through the real EditorWindow IMGUI path without throwing.",
                        location: location)
                });
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ => new AuditTargetDiscoveryResult(
                    new[]
                    {
                        new AuditTarget(
                            "Assets/GraphicsSmoke/Missing.prefab",
                            AuditTargetKind.Prefab)
                    },
                    Array.Empty<AuditDiagnostic>()),
                runAudit: _ => report);
            state.RefreshTargets(new[] { "Assets" });
            state.RunAudit();

            var window = ScriptableObject.CreateInstance<LocalizationAuditorWindow>();
            GraphicsSmokeHostWindow guiHost = null;
            try
            {
                InvokeOnEnable(window);
                SetWindowState(window, state);
                guiHost = ScriptableObject.CreateInstance<GraphicsSmokeHostWindow>();
                guiHost.Invocation = () => InvokeOnGuiMethod(window);
                guiHost.position = new Rect(0, 0, 640, 480);
                guiHost.Show();
                guiHost.Repaint();

                yield return null;

                Assert.That(guiHost.Invoked, Is.True);
                Assert.That(
                    guiHost.InvocationException,
                    Is.Null,
                    guiHost.InvocationException?.ToString());
            }
            finally
            {
                if (guiHost != null)
                {
                    guiHost.Close();
                    UnityEngine.Object.DestroyImmediate(guiHost);
                }

                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void InvokeOnGui(LocalizationAuditorWindow window)
        {
            var previousEvent = Event.current;
            Event.current = new Event { type = EventType.Layout };
            try
            {
                InvokeOnGuiMethod(window);
            }
            finally
            {
                Event.current = previousEvent;
            }
        }

        private static void InvokeOnGuiMethod(LocalizationAuditorWindow window)
        {
            var onGui = typeof(LocalizationAuditorWindow).GetMethod(
                "OnGUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onGui, Is.Not.Null);
            onGui.Invoke(window, null);
        }

        private static void InvokeOnEnable(LocalizationAuditorWindow window)
        {
            var onEnable = typeof(LocalizationAuditorWindow).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onEnable, Is.Not.Null);
            onEnable.Invoke(window, null);
        }

        private static void SetWindowState(
            LocalizationAuditorWindow window,
            LocalizationAuditorWindowState state)
        {
            var stateField = typeof(LocalizationAuditorWindow).GetField(
                "_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stateField, Is.Not.Null);
            stateField.SetValue(window, state);
        }

        private sealed class GraphicsSmokeHostWindow : EditorWindow
        {
            public Action Invocation { get; set; }
            public Exception InvocationException { get; private set; }
            public bool Invoked { get; private set; }

            private void OnGUI()
            {
                if (Invoked)
                {
                    return;
                }

                Invoked = true;
                try
                {
                    Invocation();
                }
                catch (Exception exception)
                {
                    InvocationException = exception;
                }
            }
        }
    }
}
