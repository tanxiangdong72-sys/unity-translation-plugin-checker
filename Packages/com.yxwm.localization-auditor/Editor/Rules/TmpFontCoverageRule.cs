using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEditor;

namespace Yxwm.LocalizationAuditor
{
    // 检查翻译实际使用的字符是否存在于根 TMP 字体或其 fallback 链中。
    internal sealed class TmpFontCoverageRule : IAuditRule
    {
        public const string RuleId = "TMP_FONT_COVERAGE";

        public string Id => RuleId;

        public IEnumerable<AuditIssue> Evaluate(
            AuditContext context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var characterSets = UnicodeCharacterCollector.Collect(
                StringTableDataEnumerator.Enumerate());

            foreach (var characterSet in characterSets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!context.LocaleFontAssetPaths.TryGetValue(
                        characterSet.LocaleCode,
                        out var fontAssetPath))
                {
                    yield return CreateNotVerifiedIssue(
                        characterSet.LocaleCode,
                        string.Empty,
                        "No TMP Font Asset is configured for this locale.");
                    continue;
                }

                var rootFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    fontAssetPath);
                if (rootFontAsset == null)
                {
                    yield return CreateNotVerifiedIssue(
                        characterSet.LocaleCode,
                        fontAssetPath,
                        "The configured TMP Font Asset could not be loaded.");
                    continue;
                }

                var resolution = TmpFontAssetResolver.Resolve(rootFontAsset);
                if (resolution.HasFallbackCycle)
                {
                    yield return new AuditIssue(
                        RuleId,
                        AuditSeverity.Warning,
                        "TMP Font Asset fallback references contain a cycle.",
                        "Remove the fallback cycle so the font search order is finite.",
                        new AuditIssueLocation(
                            localeCode: characterSet.LocaleCode,
                            fontAssetPath: fontAssetPath));
                }

                var missingCodePoints = new List<int>();
                var inspectionFailed = false;
                foreach (var codePoint in characterSet.CodePoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var isCovered = false;
                    foreach (var fontAsset in resolution.FontAssets)
                    {
                        if (TryContainsCodePoint(fontAsset, codePoint, out isCovered) &&
                            isCovered)
                        {
                            break;
                        }

                        if (!isCovered)
                        {
                            continue;
                        }

                        inspectionFailed = true;
                        break;
                    }

                    if (inspectionFailed)
                    {
                        break;
                    }

                    if (!isCovered)
                    {
                        missingCodePoints.Add(codePoint);
                    }
                }

                if (inspectionFailed)
                {
                    yield return CreateNotVerifiedIssue(
                        characterSet.LocaleCode,
                        fontAssetPath,
                        "At least one TMP Font Asset could not be inspected without modifying it.");
                    continue;
                }

                if (missingCodePoints.Count > 0)
                {
                    yield return new AuditIssue(
                        RuleId,
                        AuditSeverity.Error,
                        "TMP Font Assets do not cover all translated characters for locale '" +
                        characterSet.LocaleCode +
                        "'.",
                        "Add the missing characters to the root font or a fallback Font Asset.",
                        new AuditIssueLocation(
                            localeCode: characterSet.LocaleCode,
                            fontAssetPath: fontAssetPath),
                        missingCodePoints);
                }
            }
        }

        private static bool TryContainsCodePoint(
            TMP_FontAsset fontAsset,
            int codePoint,
            out bool result)
        {
            try
            {
                var characterLookup = fontAsset.characterLookupTable;
                result = characterLookup != null &&
                    characterLookup.ContainsKey((uint)codePoint);
                return true;
            }
            catch
            {
                result = true;
                return false;
            }
        }

        private static AuditIssue CreateNotVerifiedIssue(
            string localeCode,
            string fontAssetPath,
            string message)
        {
            return new AuditIssue(
                RuleId,
                AuditSeverity.NotVerified,
                message,
                "Assign a valid TMP Font Asset for this locale and inspect it manually.",
                new AuditIssueLocation(
                    localeCode: localeCode,
                    fontAssetPath: fontAssetPath));
        }
    }
}
