using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor.Localization;

namespace Yxwm.LocalizationAuditor
{
    // 检查官方 String Table 的结构完整性，不评价翻译内容是否为空或自然。
    internal sealed class StringTableCompletenessRule : IAuditRule
    {
        public const string RuleId = "STRING_TABLE_COMPLETENESS";

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
            // Locale 来源于项目设置，集合必须为每个项目 Locale 提供对应 Table。
            var localeCodes = LocalizationEditorSettings
                .GetLocales()
                .Where(locale => locale != null)
                .Select(locale => locale.Identifier.Code)
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();

            var collections = StringTableDataEnumerator.Enumerate();
            foreach (var collection in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var localeCode in localeCodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (collection.GetTable(localeCode) != null)
                    {
                        continue;
                    }

                    yield return new AuditIssue(
                        RuleId,
                        AuditSeverity.Error,
                        "String Table collection '" + collection.CollectionName +
                        "' is missing a table for locale '" + localeCode + "'.",
                        "Create the missing Locale Table in the String Table Collection.",
                        new AuditIssueLocation(
                            localeCode: localeCode,
                            tableName: collection.CollectionName,
                            assetPath: collection.AssetPath));
                }

                foreach (var table in collection.Tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 只有已存在的 Table 才检查 Key，避免一个缺表产生重复的缺 Key 报告。
                    foreach (var sharedEntry in collection.SharedEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var entry = table.GetEntry(sharedEntry.Key);
                        if (entry != null && entry.Exists)
                        {
                            continue;
                        }

                        yield return new AuditIssue(
                            RuleId,
                            AuditSeverity.Error,
                            "String Table '" + table.LocaleCode +
                            "' is missing key '" + sharedEntry.Key + "'.",
                            "Add the shared key to the Locale Table.",
                            new AuditIssueLocation(
                                localeCode: table.LocaleCode,
                                tableName: collection.CollectionName,
                                key: sharedEntry.Key,
                                assetPath: table.AssetPath));
                    }
                }
            }
        }
    }
}
