using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Localization;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证字符收集的去重、空白过滤、代理对处理和 Locale 聚合行为。
    public sealed class UnicodeCharacterCollectorTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task9";
        private const string CollectionName = "Task9 Strings";

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
        public void CollectsDistinctNonWhitespaceCodePointsPerLocale()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans"))
            {
                fixture.AddEntry("ENGLISH", "en", "A A\nB\t!");
                fixture.AddEntry("CHINESE", "zh-Hans", "中😀\r\n");

                var characterSets = UnicodeCharacterCollector.Collect(
                    StringTableDataEnumerator.Enumerate());

                Assert.That(
                    characterSets.Select(set => set.LocaleCode),
                    Is.EqualTo(new[] { "en", "zh-Hans" }));
                Assert.That(
                    characterSets.Single(set => set.LocaleCode == "en").CodePoints,
                    Is.EqualTo(new[] { 33, 65, 66 }));
                Assert.That(
                    characterSets.Single(set => set.LocaleCode == "zh-Hans").CodePoints,
                    Is.EqualTo(new[] { 0x4E2D, 0x1F600 }));
            }
        }

        [Test]
        public void IgnoresMissingAndEmptyEntries()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans"))
            {
                fixture.AddEntry("VALID", "en", "A");
                fixture.AddEntry("EMPTY", "en", string.Empty);
                fixture.AddEntry("MISSING", "zh-Hans", " ");

                var english = UnicodeCharacterCollector.Collect(
                        StringTableDataEnumerator.Enumerate())
                    .Single(set => set.LocaleCode == "en");
                var chinese = UnicodeCharacterCollector.Collect(
                        StringTableDataEnumerator.Enumerate())
                    .Single(set => set.LocaleCode == "zh-Hans");

                Assert.That(english.CodePoints, Is.EqualTo(new[] { 65 }));
                Assert.That(chinese.CodePoints, Is.Empty);
            }
        }

        [Test]
        public void SkipsUnpairedSurrogatesInsteadOfTreatingThemAsUnicodeScalars()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("BROKEN_TEXT", "en", "\uD83D");

                var characterSet = UnicodeCharacterCollector.Collect(
                        StringTableDataEnumerator.Enumerate())
                    .Single();

                Assert.That(characterSet.CodePoints, Is.Empty);
            }
        }

        [Test]
        public void ReturnsAnEmptyReadOnlyResultForNullInput()
        {
            var result = UnicodeCharacterCollector.Collect(null);

            Assert.That(result, Is.Empty);
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
