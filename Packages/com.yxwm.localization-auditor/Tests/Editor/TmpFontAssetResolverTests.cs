using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证 TMP 根字体、fallback 优先级、去重、循环保护和空配置行为。
    public sealed class TmpFontAssetResolverTests
    {
        private TMP_FontAsset[] _createdFonts;

        [SetUp]
        public void SetUp()
        {
            _createdFonts = Array.Empty<TMP_FontAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var font in _createdFonts)
            {
                if (font != null)
                {
                    UnityEngine.Object.DestroyImmediate(font);
                }
            }
        }

        [Test]
        public void ResolvePreservesFallbackPriorityAndRemovesDuplicates()
        {
            var root = CreateFont("Root");
            var latin = CreateFont("Latin");
            var cjk = CreateFont("Cjk");
            var emoji = CreateFont("Emoji");
            root.fallbackFontAssetTable.Add(latin);
            root.fallbackFontAssetTable.Add(cjk);
            root.fallbackFontAssetTable.Add(latin);
            latin.fallbackFontAssetTable.Add(emoji);

            var resolution = TmpFontAssetResolver.Resolve(root);

            Assert.That(
                resolution.FontAssets.Select(font => font.name),
                Is.EqualTo(new[] { "Root", "Latin", "Emoji", "Cjk" }));
            Assert.That(resolution.RootFontAsset, Is.SameAs(root));
            Assert.That(resolution.IsRootMissing, Is.False);
            Assert.That(resolution.HasFallbackCycle, Is.False);
        }

        [Test]
        public void ResolveStopsAtFallbackCycleWithoutRepeatingFonts()
        {
            var root = CreateFont("Root");
            var fallback = CreateFont("Fallback");
            root.fallbackFontAssetTable.Add(fallback);
            fallback.fallbackFontAssetTable.Add(root);

            var resolution = TmpFontAssetResolver.Resolve(root);

            Assert.That(
                resolution.FontAssets.Select(font => font.name),
                Is.EqualTo(new[] { "Root", "Fallback" }));
            Assert.That(resolution.HasFallbackCycle, Is.True);
        }

        [Test]
        public void ResolveReportsMissingRootWithoutUsingGlobalFallbacks()
        {
            var resolution = TmpFontAssetResolver.Resolve(null);

            Assert.That(resolution.IsRootMissing, Is.True);
            Assert.That(resolution.RootFontAsset, Is.Null);
            Assert.That(resolution.FontAssets, Is.Empty);
            Assert.That(resolution.HasFallbackCycle, Is.False);
        }

        [Test]
        public void ResolveIgnoresNullFallbackEntries()
        {
            var root = CreateFont("Root");
            root.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>
            {
                null
            };

            var resolution = TmpFontAssetResolver.Resolve(root);

            Assert.That(resolution.FontAssets, Is.EqualTo(new[] { root }));
            Assert.That(resolution.HasFallbackCycle, Is.False);
        }

        private TMP_FontAsset CreateFont(string name)
        {
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = name;
            font.fallbackFontAssetTable =
                new System.Collections.Generic.List<TMP_FontAsset>();
            _createdFonts = _createdFonts.Append(font).ToArray();
            return font;
        }
    }
}
