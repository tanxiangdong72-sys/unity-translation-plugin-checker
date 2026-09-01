using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Yxwm.LocalizationAuditor.Tests
{
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
