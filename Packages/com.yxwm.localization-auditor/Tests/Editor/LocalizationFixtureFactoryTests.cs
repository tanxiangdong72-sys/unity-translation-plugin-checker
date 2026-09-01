using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yxwm.LocalizationAuditor.Tests
{
    public sealed class LocalizationFixtureFactoryTests
    {
        private const string RootDirectory = "Assets/LocalizationAuditorTestFixtures/Task5";
        private const string CollectionName = "Task5 Strings";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CleanupRootDirectory();
            CleanupAddressablesProjectState();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            CleanupRootDirectory();
            CleanupAddressablesProjectState();
        }

        [SetUp]
        public void SetUp()
        {
            CleanupRootDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupRootDirectory();
        }

        [Test]
        public void CreateBuildsLocalesAndOneStringTablePerLocale()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans",
                       "ja"))
            {
                Assert.That(fixture.StringTableCollection, Is.Not.Null);
                Assert.That(
                    fixture.StringTableCollection.TableCollectionName,
                    Is.EqualTo(CollectionName));
                Assert.That(
                    fixture.Locales.Select(locale => locale.Identifier.Code),
                    Is.EqualTo(new[] { "en", "ja", "zh-Hans" }));
                Assert.That(
                    fixture.Tables.Keys,
                    Is.EquivalentTo(new[] { "en", "zh-Hans", "ja" }));
                Assert.That(
                    fixture.Tables.Values.All(table =>
                        AssetDatabase.GetAssetPath(table).StartsWith(
                            RootDirectory,
                            StringComparison.Ordinal)),
                    Is.True);
            }
        }

        [Test]
        public void AddEntryWritesValueToTheRequestedLocaleTable()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en",
                       "zh-Hans"))
            {
                var englishEntry = fixture.AddEntry("GREETING", "en", "Hello");
                var chineseEntry = fixture.AddEntry("GREETING", "zh-Hans", "你好");

                Assert.That(englishEntry.LocalizedValue, Is.EqualTo("Hello"));
                Assert.That(chineseEntry.LocalizedValue, Is.EqualTo("你好"));
                Assert.That(
                    fixture.GetTable("en").GetEntry("GREETING").LocalizedValue,
                    Is.EqualTo("Hello"));
                Assert.That(
                    fixture.GetTable("zh-Hans").GetEntry("GREETING").LocalizedValue,
                    Is.EqualTo("你好"));
            }
        }

        [Test]
        public void DisposeRemovesGeneratedAssetsAndProjectLocales()
        {
            LocalizationFixture fixture = null;
            try
            {
                fixture = LocalizationFixtureFactory.Create(
                    RootDirectory,
                    CollectionName,
                    "en",
                    "zh-Hans");

                Assert.That(AssetDatabase.IsValidFolder(RootDirectory), Is.True);
            }
            finally
            {
                fixture?.Dispose();
            }

            AssetDatabase.Refresh();
            Assert.That(AssetDatabase.IsValidFolder(RootDirectory), Is.False);
            Assert.That(LocalizationEditorSettings.GetLocale("en"), Is.Null);
            Assert.That(
                LocalizationEditorSettings.GetStringTableCollection(CollectionName),
                Is.Null);
        }

        [Test]
        public void CreateRejectsDirectoriesOutsideFixtureRoot()
        {
            Assert.Throws<ArgumentException>(() =>
                LocalizationFixtureFactory.Create(
                    "Assets/Unexpected",
                    CollectionName,
                    "en"));
        }

        [Test]
        public void CreateRejectsDuplicateLocaleCodes()
        {
            Assert.Throws<ArgumentException>(() =>
                LocalizationFixtureFactory.Create(
                    RootDirectory,
                    CollectionName,
                    "en",
                    "en"));
        }

        [Test]
        public void AddEntryRejectsUnknownLocaleCode()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                Assert.Throws<ArgumentException>(() =>
                    fixture.AddEntry("GREETING", "fr", "Bonjour"));
            }
        }

        private static void CleanupRootDirectory()
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CleanupAddressablesProjectState()
        {
            // 该验证工程专门用于测试，运行前后可以移除由 Localization 自动创建的配置。
            EditorBuildSettings.RemoveConfigObject(
                AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
            EditorBuildSettings.RemoveConfigObject(
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName);
            AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);

            var field = typeof(AddressableAssetSettingsDefaultObject).GetField(
                "s_DefaultSettingsObject",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
