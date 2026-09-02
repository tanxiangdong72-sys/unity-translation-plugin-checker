using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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

    // 保证窗口在无报告和长文本状态下真正进入私有 OnGUI。
    public sealed class LocalizationAuditorWindowReportSmokeTests
    {
        [UnityTest]
        public IEnumerator WindowOnGuiDoesNotThrowWithoutReportWhenInvokedThroughReflection()
        {
            var window = ScriptableObject.CreateInstance<LocalizationAuditorWindow>();
            ReflectionInvokerWindow guiHost = null;
            try
            {
                SetWindowState(window, new LocalizationAuditorWindowState());
                guiHost = CreateGuiHost(() => InvokeOnGui(window));

                yield return null;

                Assert.That(guiHost.Invoked, Is.True);
                Assert.That(
                    guiHost.InvocationException,
                    Is.Null,
                    guiHost.InvocationException?.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guiHost);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator WindowOnGuiDoesNotThrowForLongTextReportWhenInvokedThroughReflection()
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
            ReflectionInvokerWindow guiHost = null;
            try
            {
                SetWindowState(window, state);
                guiHost = CreateGuiHost(() => InvokeOnGui(window));

                yield return null;

                Assert.That(guiHost.Invoked, Is.True);
                Assert.That(
                    guiHost.InvocationException,
                    Is.Null,
                    guiHost.InvocationException?.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guiHost);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static ReflectionInvokerWindow CreateGuiHost(Action action)
        {
            var guiHost = ScriptableObject.CreateInstance<ReflectionInvokerWindow>();
            guiHost.Invocation = action;
            guiHost.position = new Rect(0, 0, 640, 480);
            guiHost.Show();
            guiHost.Repaint();
            return guiHost;
        }

        private static void InvokeOnGui(LocalizationAuditorWindow window)
        {
            var onGui = typeof(LocalizationAuditorWindow).GetMethod(
                "OnGUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onGui, Is.Not.Null);
            onGui.Invoke(window, null);
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

        private sealed class ReflectionInvokerWindow : EditorWindow
        {
            public Action Invocation { get; set; }
            public Exception InvocationException { get; private set; }
            public bool Invoked { get; private set; }

            public void OnGUI()
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
