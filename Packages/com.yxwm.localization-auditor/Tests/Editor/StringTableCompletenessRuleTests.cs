using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 用可控的 Localization 资产验证缺 Table、缺 Key 和取消行为。
    public sealed class StringTableCompletenessRuleTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task7";
        private const string CollectionName = "Task7 Strings";

        [SetUp]
        public void SetUp()
        {
            CleanupFixtureRoot();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupFixtureRoot();
        }

        [Test]
        public void ReportsMissingLocaleTableAsError()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                var japaneseTable = fixture.GetTable("ja");
                fixture.StringTableCollection.RemoveTable(japaneseTable);
                EditorUtility.SetDirty(fixture.StringTableCollection);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(japaneseTable));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var issues = EvaluateRule();

                var issue = issues.Single();
                Assert.That(issue.RuleId, Is.EqualTo(StringTableCompletenessRule.RuleId));
                Assert.That(issue.Severity, Is.EqualTo(AuditSeverity.Error));
                Assert.That(issue.Location.LocaleCode, Is.EqualTo("ja"));
                Assert.That(issue.Location.TableName, Is.EqualTo(CollectionName));
                Assert.That(issue.Location.Key, Is.Empty);
                Assert.That(issue.Location.AssetPath, Is.EqualTo(
                    AssetDatabase.GetAssetPath(fixture.StringTableCollection)));
            }
        }

        [Test]
        public void ReportsMissingSharedEntryInExistingLocaleTableAsError()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");
                fixture.AddEntry("GREETING", "zh-Hans", "你好");

                var issues = EvaluateRule();

                var issue = issues.Single();
                Assert.That(issue.RuleId, Is.EqualTo(StringTableCompletenessRule.RuleId));
                Assert.That(issue.Severity, Is.EqualTo(AuditSeverity.Error));
                Assert.That(issue.Location.LocaleCode, Is.EqualTo("ja"));
                Assert.That(issue.Location.TableName, Is.EqualTo(CollectionName));
                Assert.That(issue.Location.Key, Is.EqualTo("GREETING"));
                Assert.That(issue.Location.AssetPath, Is.EqualTo(
                    AssetDatabase.GetAssetPath(fixture.GetTable("ja"))));
            }
        }

        [Test]
        public void DoesNotReportCompleteTables()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                foreach (var localeCode in new[] { "en", "zh-Hans", "ja" })
                {
                    fixture.AddEntry("GREETING", localeCode, localeCode);
                }

                Assert.That(EvaluateRule(), Is.Empty);
            }
        }

        [Test]
        public void ReportsMissingTableBeforeMissingEntriesInDeterministicOrder()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");
                fixture.StringTableCollection.RemoveTable(fixture.GetTable("ja"));
                EditorUtility.SetDirty(fixture.StringTableCollection);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var issues = EvaluateRule();

                Assert.That(issues, Has.Length.EqualTo(2));
                Assert.That(issues[0].Location.LocaleCode, Is.EqualTo("ja"));
                Assert.That(issues[0].Location.Key, Is.Empty);
                Assert.That(issues[1].Location.LocaleCode, Is.EqualTo("zh-Hans"));
                Assert.That(issues[1].Location.Key, Is.EqualTo("GREETING"));
            }
        }

        [Test]
        public void HonorsCancellationBeforeEnumeration()
        {
            using (LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    EvaluateRule(cancellation.Token));
            }
        }

        private static AuditIssue[] EvaluateRule(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var rule = new StringTableCompletenessRule();
            return rule.Evaluate(
                    new AuditContext(new AuditRequest()),
                    cancellationToken)
                .ToArray();
        }

        private static void CleanupFixtureRoot()
        {
            var locales = LocalizationEditorSettings
                .GetLocales()
                .Where(locale =>
                    AssetDatabase.GetAssetPath(locale).StartsWith(
                        RootDirectory,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var locale in locales)
            {
                LocalizationEditorSettings.RemoveLocale(locale);
                var localePath = AssetDatabase.GetAssetPath(locale);
                if (!string.IsNullOrEmpty(localePath))
                {
                    AssetDatabase.DeleteAsset(localePath);
                }
            }

            LocalizationFixtureFactoryCleanup.Remove(
                RootDirectory,
                Array.Empty<UnityEngine.Localization.Locale>(),
                true);
        }
    }
}
