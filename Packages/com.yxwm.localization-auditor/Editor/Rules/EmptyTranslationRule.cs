using System;
using System.Collections.Generic;
using System.Threading;

namespace Yxwm.LocalizationAuditor
{
    // 检查已经存在但没有有效文本的翻译，不重复报告缺失的 Table 或 Key。
    internal sealed class EmptyTranslationRule : IAuditRule
    {
        public const string RuleId = "EMPTY_TRANSLATION";

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
            foreach (var collection in StringTableDataEnumerator.Enumerate())
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var table in collection.Tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var entry in table.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!entry.Exists || !entry.IsEmpty)
                        {
                            continue;
                        }

                        yield return new AuditIssue(
                            RuleId,
                            AuditSeverity.Error,
                            "String Table '" + table.LocaleCode +
                            "' has an empty translation for key '" + entry.Key + "'.",
                            "Provide a non-empty translation for the Locale Table entry.",
                            new AuditIssueLocation(
                                localeCode: table.LocaleCode,
                                tableName: collection.CollectionName,
                                key: entry.Key,
                                assetPath: table.AssetPath));
                    }
                }
            }
        }
    }
}
