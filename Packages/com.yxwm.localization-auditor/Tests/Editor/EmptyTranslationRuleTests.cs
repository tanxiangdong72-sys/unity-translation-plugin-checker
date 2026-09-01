using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor.Localization;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 用可控的 String Table 资产验证空翻译的三种输入形态和排重边界。
    public sealed class EmptyTranslationRuleTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task8";
        private const string CollectionName = "Task8 Strings";

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
        public void ReportsNullEmptyAndWhitespaceTranslationsAsErrors()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans"))
            {
                fixture.AddEntry("NULL_VALUE", "en", null);
                fixture.AddEntry("NULL_VALUE", "zh-Hans", "你好");
                fixture.AddEntry("EMPTY_VALUE", "en", string.Empty);
                fixture.AddEntry("EMPTY_VALUE", "zh-Hans", "你好");
                fixture.AddEntry("WHITESPACE_VALUE", "en", "Hello");
                fixture.AddEntry("WHITESPACE_VALUE", "zh-Hans", " \t\r\n ");
                fixture.AddEntry("VALID_VALUE", "en", "Hello");
                fixture.AddEntry("VALID_VALUE", "zh-Hans", "你好");

                var issues = EvaluateRule();

                Assert.That(issues, Has.Length.EqualTo(3));
                Assert.That(
                    issues.Select(issue => issue.Location.Key),
                    Is.EqualTo(new[]
                    {
                        "EMPTY_VALUE",
                        "NULL_VALUE",
                        "WHITESPACE_VALUE"
                    }));
                Assert.That(
                    issues.All(issue => issue.Severity == AuditSeverity.Error),
                    Is.True);
                Assert.That(
                    issues.All(issue => issue.RuleId == EmptyTranslationRule.RuleId),
                    Is.True);
            }
        }

        [Test]
        public void DoesNotReportMissingEntries()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");

                var issues = EvaluateRule();

                Assert.That(
                    issues.Any(issue =>
                        issue.Location.Key == "GREETING" &&
                        issue.Location.LocaleCode == "zh-Hans"),
                    Is.False);
            }
        }

        [Test]
        public void ReportsEveryLocaleWithAnEmptyTranslation()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                fixture.AddEntry("GREETING", "en", string.Empty);
                fixture.AddEntry("GREETING", "zh-Hans", string.Empty);
                fixture.AddEntry("GREETING", "ja", "プレイ");

                var issues = EvaluateRule();

                Assert.That(
                    issues.Select(issue => issue.Location.LocaleCode),
                    Is.EqualTo(new[] { "en", "zh-Hans" }));
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
            var rule = new EmptyTranslationRule();
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
                    UnityEditor.AssetDatabase.GetAssetPath(locale).StartsWith(
                        RootDirectory,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var locale in locales)
            {
                LocalizationEditorSettings.RemoveLocale(locale);
                var localePath = UnityEditor.AssetDatabase.GetAssetPath(locale);
                if (!string.IsNullOrEmpty(localePath))
                {
                    UnityEditor.AssetDatabase.DeleteAsset(localePath);
                }
            }

            LocalizationFixtureFactoryCleanup.Remove(
                RootDirectory,
                Array.Empty<UnityEngine.Localization.Locale>(),
                true);
        }
    }
}
