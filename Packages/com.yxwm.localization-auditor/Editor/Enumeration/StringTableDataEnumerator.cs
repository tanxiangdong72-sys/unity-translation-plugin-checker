using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yxwm.LocalizationAuditor
{
    internal static class StringTableDataEnumerator
    {
        public static IReadOnlyList<StringTableCollectionSnapshot> Enumerate()
        {
            // Unity 返回的集合顺序不作为产品契约，这里统一按名称和资源路径排序。
            var collections = LocalizationEditorSettings
                .GetStringTableCollections()
                .Where(collection => collection != null)
                .OrderBy(collection => collection.TableCollectionName, StringComparer.Ordinal)
                .ThenBy(
                    collection => AssetDatabase.GetAssetPath(collection),
                    StringComparer.Ordinal)
                .Select(Enumerate)
                .ToList();

            return new ReadOnlyCollection<StringTableCollectionSnapshot>(collections);
        }

        public static StringTableCollectionSnapshot Enumerate(
            StringTableCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            var sharedEntries = collection.SharedData == null
                ? Enumerable.Empty<SharedTableEntrySnapshot>()
                : collection.SharedData.Entries
                    .Where(entry => entry != null)
                    .Select(entry => new SharedTableEntrySnapshot(entry.Id, entry.Key));

            var tables = collection.StringTables
                .Where(table => table != null)
                .Select(table => CreateTableSnapshot(collection, table))
                .ToList();

            return new StringTableCollectionSnapshot(
                collection.TableCollectionName,
                AssetDatabase.GetAssetPath(collection),
                collection.SharedData == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(collection.SharedData),
                sharedEntries,
                tables);
        }

        private static StringTableSnapshot CreateTableSnapshot(
            StringTableCollection collection,
            StringTable table)
        {
            var sharedEntries = collection.SharedData == null
                ? Enumerable.Empty<SharedTableData.SharedTableEntry>()
                : collection.SharedData.Entries.Where(entry => entry != null);

            // 每个表都按共享 Key 生成完整行；表中不存在的 Entry 保留为 Exists=false。
            var entries = sharedEntries.Select(sharedEntry =>
            {
                var tableEntry = table.GetEntry(sharedEntry.Id);
                return new StringTableEntrySnapshot(
                    sharedEntry.Id,
                    sharedEntry.Key,
                    tableEntry != null,
                    tableEntry?.LocalizedValue);
            });

            return new StringTableSnapshot(
                table.LocaleIdentifier.Code,
                AssetDatabase.GetAssetPath(table),
                entries);
        }
    }
}
