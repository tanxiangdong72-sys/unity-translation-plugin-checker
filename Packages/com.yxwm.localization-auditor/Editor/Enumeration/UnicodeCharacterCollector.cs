using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    internal sealed class UnicodeCharacterSet
    {
        private readonly IReadOnlyList<int> _codePoints;

        internal UnicodeCharacterSet(
            string localeCode,
            IEnumerable<int> codePoints)
        {
            LocaleCode = localeCode ?? string.Empty;
            var sortedCodePoints = codePoints == null
                ? new List<int>()
                : new HashSet<int>(codePoints).OrderBy(codePoint => codePoint).ToList();
            _codePoints = new ReadOnlyCollection<int>(sortedCodePoints);
        }

        public string LocaleCode { get; }
        public IReadOnlyList<int> CodePoints => _codePoints;
    }

    internal static class UnicodeCharacterCollector
    {
        public static IReadOnlyList<UnicodeCharacterSet> Collect(
            IEnumerable<StringTableCollectionSnapshot> collections)
        {
            // 多个 String Table Collection 可能共享同一个 Locale，因此先按 Locale 聚合。
            var codePointsByLocale = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal);

            if (collections == null)
            {
                return new ReadOnlyCollection<UnicodeCharacterSet>(
                    new List<UnicodeCharacterSet>());
            }

            foreach (var collection in collections)
            {
                if (collection == null)
                {
                    continue;
                }

                foreach (var table in collection.Tables)
                {
                    if (!codePointsByLocale.TryGetValue(
                            table.LocaleCode,
                            out var codePoints))
                    {
                        codePoints = new HashSet<int>();
                        codePointsByLocale.Add(table.LocaleCode, codePoints);
                    }

                    foreach (var entry in table.Entries)
                    {
                        if (!entry.Exists || entry.IsEmpty)
                        {
                            continue;
                        }

                        AddTextCodePoints(entry.LocalizedValue, codePoints);
                    }
                }
            }

            var result = codePointsByLocale
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new UnicodeCharacterSet(pair.Key, pair.Value))
                .ToList();
            return new ReadOnlyCollection<UnicodeCharacterSet>(result);
        }

        private static void AddTextCodePoints(
            string text,
            ISet<int> codePoints)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                int codePoint;

                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= text.Length ||
                        !char.IsLowSurrogate(text[index + 1]))
                    {
                        // 孤立代理项不是有效 Unicode scalar，不参与字体覆盖检查。
                        continue;
                    }

                    codePoint = char.ConvertToUtf32(current, text[++index]);
                }
                else if (char.IsLowSurrogate(current))
                {
                    // 低代理项没有对应高代理项时同样跳过。
                    continue;
                }
                else
                {
                    codePoint = current;
                }

                if (codePoint <= char.MaxValue &&
                    char.IsWhiteSpace((char)codePoint))
                {
                    continue;
                }

                codePoints.Add(codePoint);
            }
        }
    }
}
