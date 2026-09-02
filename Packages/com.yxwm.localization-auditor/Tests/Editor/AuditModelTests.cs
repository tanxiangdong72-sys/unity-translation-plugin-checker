using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 这些测试锁定模型的快照、校验和确定性排序契约。
    public sealed class AuditModelTests
    {
        [Test]
        public void AuditRequestSnapshotsAndSortsInputs()
        {
            var assetPaths = new List<string>
            {
                "Assets/Z.prefab",
                "Assets/A.prefab",
                "Assets/A.prefab"
            };
            var ruleIds = new List<string>
            {
                "TMP_GLYPH",
                "TABLE_ENTRIES",
                "TMP_GLYPH"
            };
            var fontMappings = new Dictionary<string, string>
            {
                ["ja"] = "Assets/Fonts/Japanese.asset",
                ["en"] = "Assets/Fonts/English.asset"
            };

            var request = new AuditRequest(assetPaths, ruleIds, fontMappings);

            assetPaths.Clear();
            ruleIds.Clear();
            fontMappings.Clear();

            Assert.That(request.AssetPaths, Is.EqualTo(new[]
            {
                "Assets/A.prefab",
                "Assets/Z.prefab"
            }));
            Assert.That(request.EnabledRuleIds, Is.EqualTo(new[]
            {
                "TABLE_ENTRIES",
                "TMP_GLYPH"
            }));
            Assert.That(request.LocaleFontAssetPaths["en"], Is.EqualTo("Assets/Fonts/English.asset"));
            Assert.That(request.LocaleFontAssetPaths["ja"], Is.EqualTo("Assets/Fonts/Japanese.asset"));
        }

        [Test]
        public void AuditIssueSortsAndDeduplicatesMissingCodePoints()
        {
            var issue = new AuditIssue(
                "TMP_GLYPH",
                AuditSeverity.Error,
                "Missing glyphs.",
                missingCodePoints: new[] { 0x4E2D, 0x41, 0x41 });

            Assert.That(issue.State, Is.EqualTo(AuditIssueState.Open));
            Assert.That(issue.Location, Is.SameAs(AuditIssueLocation.Empty));
            Assert.That(issue.MissingCodePoints, Is.EqualTo(new[] { 0x41, 0x4E2D }));
        }

        // 回归验证旧六个位置参数的语义不变，并确认新增定位字段可继续使用位置参数传入。
        [Test]
        public void AuditIssueLocationPreservesLegacyPositionalArgumentsAndAppendsNewFields()
        {
            var location = new AuditIssueLocation(
                "en",
                "Strings",
                "KEY",
                "Assets/Prefab.prefab",
                "Root/Label",
                "Assets/Fonts/English.asset",
                "UnityEngine.UI.Text",
                "m_Text");

            Assert.That(location.LocaleCode, Is.EqualTo("en"));
            Assert.That(location.TableName, Is.EqualTo("Strings"));
            Assert.That(location.Key, Is.EqualTo("KEY"));
            Assert.That(location.AssetPath, Is.EqualTo("Assets/Prefab.prefab"));
            Assert.That(location.ObjectPath, Is.EqualTo("Root/Label"));
            Assert.That(location.FontAssetPath, Is.EqualTo("Assets/Fonts/English.asset"));
            Assert.That(location.ComponentType, Is.EqualTo("UnityEngine.UI.Text"));
            Assert.That(location.PropertyPath, Is.EqualTo("m_Text"));
        }

        [Test]
        public void AuditReportSortsIssuesAndCalculatesCounts()
        {
            var startedAt = new DateTimeOffset(2026, 9, 1, 1, 0, 0, TimeSpan.Zero);
            var finishedAt = startedAt.AddSeconds(5);
            var ignored = new AuditIssue(
                "TABLE_ENTRIES",
                AuditSeverity.Error,
                "Ignored issue.",
                location: new AuditIssueLocation(assetPath: "Assets/A.prefab"),
                state: AuditIssueState.Ignored);
            var warning = new AuditIssue(
                "TABLE_ENTRIES",
                AuditSeverity.Warning,
                "Warning issue.",
                location: new AuditIssueLocation(assetPath: "Assets/Z.prefab"));
            var error = new AuditIssue(
                "TMP_GLYPH",
                AuditSeverity.Error,
                "Error issue.",
                location: new AuditIssueLocation(assetPath: "Assets/B.prefab"));

            var report = new AuditReport(
                startedAt,
                finishedAt,
                AuditRunStatus.Completed,
                3,
                new[] { warning, ignored, error },
                Array.Empty<AuditDiagnostic>());

            Assert.That(report.Duration, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(report.Issues, Is.EqualTo(new[] { error, ignored, warning }));
            Assert.That(report.ErrorCount, Is.EqualTo(1));
            Assert.That(report.WarningCount, Is.EqualTo(1));
            Assert.That(report.NotVerifiedCount, Is.EqualTo(0));
            Assert.That(report.IgnoredCount, Is.EqualTo(1));
        }

        [Test]
        public void AuditIssueRejectsInvalidUnicodeCodePoint()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AuditIssue(
                    "TMP_GLYPH",
                    AuditSeverity.Error,
                    "Invalid code point.",
                    missingCodePoints: new[] { 0x110000 }));
        }
    }
}
