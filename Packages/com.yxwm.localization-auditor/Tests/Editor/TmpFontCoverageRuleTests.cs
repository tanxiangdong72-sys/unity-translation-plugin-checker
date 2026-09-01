using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 用真实 TMP 资产验证缺字、fallback、配置缺失、循环和只读行为。
    public sealed class TmpFontCoverageRuleTests
    {
        private const string RootDirectory =
            "Assets/LocalizationAuditorTestFixtures/Task11";
        private const string CollectionName = "Task11 Strings";

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
        public void ReportsAllMissingCodePointsAsOneErrorPerLocale()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "A中😀");
                var rootPath = CreateFontAsset("Root", 0x41);

                var issues = EvaluateRule(new Dictionary<string, string>
                {
                    ["en"] = rootPath
                });

                var issue = issues.Single();
                Assert.That(issue.RuleId, Is.EqualTo(TmpFontCoverageRule.RuleId));
                Assert.That(issue.Severity, Is.EqualTo(AuditSeverity.Error));
                Assert.That(issue.Location.LocaleCode, Is.EqualTo("en"));
                // 字符集按 Locale 聚合，可能来自多个 Collection，因此不绑定单一 Table。
                Assert.That(issue.Location.TableName, Is.Empty);
                Assert.That(issue.Location.FontAssetPath, Is.EqualTo(rootPath));
                Assert.That(issue.MissingCodePoints, Is.EqualTo(new[] { 0x4E2D, 0x1F600 }));
            }
        }

        [Test]
        public void FallbackFontCoversCharactersWithoutReportingAnError()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "A中");
                var rootPath = CreateFontAsset("Root", 0x41);
                var fallbackPath = CreateFontAsset("Cjk", 0x4E2D);
                var root = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(rootPath);
                var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);
                root.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();

                var issues = EvaluateRule(new Dictionary<string, string>
                {
                    ["en"] = rootPath
                });

                Assert.That(issues, Is.Empty);
            }
        }

        [Test]
        public void MissingFontMappingIsReportedAsNotVerified()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "Hello");

                var issues = EvaluateRule(new Dictionary<string, string>());

                var issue = issues.Single();
                Assert.That(issue.Severity, Is.EqualTo(AuditSeverity.NotVerified));
                Assert.That(issue.Location.LocaleCode, Is.EqualTo("en"));
                Assert.That(issue.Location.FontAssetPath, Is.Empty);
                Assert.That(issue.MissingCodePoints, Is.Empty);
            }
        }

        [Test]
        public void FallbackCycleIsReportedAsWarning()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "A");
                var rootPath = CreateFontAsset("Root", 0x41);
                var fallbackPath = CreateFontAsset("Fallback", 0x42);
                var root = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(rootPath);
                var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);
                root.fallbackFontAssetTable.Add(fallback);
                fallback.fallbackFontAssetTable.Add(root);
                EditorUtility.SetDirty(root);
                EditorUtility.SetDirty(fallback);
                AssetDatabase.SaveAssets();

                var issues = EvaluateRule(new Dictionary<string, string>
                {
                    ["en"] = rootPath
                });

                var issue = issues.Single();
                Assert.That(issue.Severity, Is.EqualTo(AuditSeverity.Warning));
                Assert.That(issue.Location.LocaleCode, Is.EqualTo("en"));
                Assert.That(issue.Location.FontAssetPath, Is.EqualTo(rootPath));
            }
        }

        [Test]
        public void CoverageCheckDoesNotModifyFontAssetFile()
        {
            using (var fixture = LocalizationFixtureFactory.Create(
                       RootDirectory,
                       CollectionName,
                       "en"))
            {
                fixture.AddEntry("GREETING", "en", "A中");
                var rootPath = CreateFontAsset("Root", 0x41);
                var before = File.ReadAllText(rootPath);

                EvaluateRule(new Dictionary<string, string>
                {
                    ["en"] = rootPath
                });

                Assert.That(File.ReadAllText(rootPath), Is.EqualTo(before));
            }
        }

        private static AuditIssue[] EvaluateRule(
            IReadOnlyDictionary<string, string> fontPaths,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var rule = new TmpFontCoverageRule();
            return rule.Evaluate(
                    new AuditContext(new AuditRequest(
                        localeFontAssetPaths: fontPaths)),
                    cancellationToken)
                .ToArray();
        }

        private static string CreateFontAsset(
            string name,
            params int[] codePoints)
        {
            var fontsDirectory = RootDirectory + "/Fonts";
            if (!AssetDatabase.IsValidFolder(fontsDirectory))
            {
                AssetDatabase.CreateFolder(RootDirectory, "Fonts");
            }

            var path = fontsDirectory + "/" + name + ".asset";
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = name;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();

            var characterLookupField = typeof(TMP_FontAsset).GetField(
                "m_CharacterLookupDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic);
            characterLookupField.SetValue(
                font,
                codePoints.ToDictionary(
                    codePoint => (uint)codePoint,
                    _ => (TMP_Character)null));

            AssetDatabase.CreateAsset(font, path);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return path;
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
