using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 这些测试锁定 String Table 快照的层级关系、缺失语义和只读行为。
    public sealed class StringTableDataEnumeratorTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task6";
        private const string CollectionName = "Task6 Strings";

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
        public void EnumerateSortsCollectionsTablesAndSharedEntries()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "zh-Hans",
                       "en",
                       "ja"))
            {
                fixture.AddEntry("MENU_PLAY", "en", "Play");
                fixture.AddEntry("MENU_PLAY", "zh-Hans", "开始");
                fixture.AddEntry("MENU_PLAY", "ja", "プレイ");
                fixture.AddEntry("GREETING", "en", "Hello");
                fixture.AddEntry("GREETING", "zh-Hans", "你好");

                var snapshots = StringTableDataEnumerator.Enumerate();
                var snapshot = snapshots.Single(item => item.CollectionName == CollectionName);

                Assert.That(
                    snapshots.Select(item => item.CollectionName),
                    Is.Ordered);
                Assert.That(
                    snapshot.Tables.Select(table => table.LocaleCode),
                    Is.EqualTo(new[] { "en", "ja", "zh-Hans" }));
                Assert.That(
                    snapshot.SharedEntries.Select(entry => entry.Key),
                    Is.EqualTo(new[] { "GREETING", "MENU_PLAY" }));
                Assert.That(
                    snapshot.Tables.All(table =>
                        table.Entries.Select(entry => entry.Key)
                            .SequenceEqual(new[] { "GREETING", "MENU_PLAY" })),
                    Is.True);
            }
        }

        [Test]
        public void EnumerateDistinguishesMissingEntryFromEmptyTranslation()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");
                fixture.AddEntry("GREETING", "zh-Hans", string.Empty);

                var snapshot = StringTableDataEnumerator.Enumerate()
                    .Single(item => item.CollectionName == CollectionName);
                var chineseEntry = snapshot.GetTable("zh-Hans").GetEntry("GREETING");
                var japaneseEntry = snapshot.GetTable("ja").GetEntry("GREETING");

                Assert.That(chineseEntry.Exists, Is.True);
                Assert.That(chineseEntry.LocalizedValue, Is.EqualTo(string.Empty));
                Assert.That(japaneseEntry.Exists, Is.False);
                Assert.That(japaneseEntry.LocalizedValue, Is.Null);
            }
        }

        [Test]
        public void SnapshotDoesNotChangeWhenSourceTableChangesAfterEnumeration()
        {
            StringTableCollectionSnapshot snapshot;
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");
                snapshot = StringTableDataEnumerator.Enumerate()
                    .Single(item => item.CollectionName == CollectionName);

                fixture.AddEntry("FAREWELL", "en", "Goodbye");
            }

            Assert.That(
                snapshot.GetTable("en").Entries.Select(entry => entry.Key),
                Is.EqualTo(new[] { "GREETING" }));
        }

        [Test]
        public void EnumerateDoesNotModifyProjectBuildSettings()
        {
            const string settingsPath = "ProjectSettings/EditorBuildSettings.asset";
            var before = File.ReadAllText(settingsPath);

            using (LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                StringTableDataEnumerator.Enumerate();
            }

            var after = File.ReadAllText(settingsPath);
            Assert.That(after, Is.EqualTo(before));
        }

        private static void CleanupFixtureRoot()
        {
            var locales = UnityEditor.Localization.LocalizationEditorSettings
                .GetLocales()
                .Where(locale =>
                    AssetDatabase.GetAssetPath(locale).StartsWith(
                        RootDirectory,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var locale in locales)
            {
                UnityEditor.Localization.LocalizationEditorSettings.RemoveLocale(locale);
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
