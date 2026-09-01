using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yxwm.LocalizationAuditor
{
    internal static class StringTableDataEnumerator
    {
        // Unity 的公开 Tables 属性会清理 broken 引用；这里保留原始列表以实现真正只读的审计读取。
        private static readonly FieldInfo TablesField =
            typeof(LocalizationTableCollection).GetField(
                "m_Tables",
                BindingFlags.Instance | BindingFlags.NonPublic);

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

            var sharedData = collection.SharedData;
            var collectionName = sharedData == null
                ? string.Empty
                : sharedData.TableCollectionName;
            var sharedEntries = sharedData == null
                ? Enumerable.Empty<SharedTableEntrySnapshot>()
                : sharedData.Entries
                    .Where(entry => entry != null)
                    .Select(entry => new SharedTableEntrySnapshot(entry.Id, entry.Key));

            var tables = GetStringTablesWithoutMutation(collection)
                .Select(table => CreateTableSnapshot(sharedData, table))
                .ToList();

            return new StringTableCollectionSnapshot(
                collectionName,
                AssetDatabase.GetAssetPath(collection),
                sharedData == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(sharedData),
                sharedEntries,
                tables);
        }

        private static IEnumerable<StringTable> GetStringTablesWithoutMutation(
            StringTableCollection collection)
        {
            if (TablesField == null)
            {
                throw new MissingFieldException(
                    typeof(LocalizationTableCollection).FullName,
                    "m_Tables");
            }

            var tableReferences = TablesField.GetValue(collection) as System.Collections.IEnumerable;
            if (tableReferences == null)
            {
                yield break;
            }

            // 只读取 LazyLoadReference 的状态和 asset，不调用会修改集合的公开属性。
            foreach (var tableReference in tableReferences)
            {
                if (tableReference == null)
                {
                    continue;
                }

                var referenceType = tableReference.GetType();
                var isBrokenProperty = referenceType.GetProperty("isBroken");
                if (isBrokenProperty?.GetValue(tableReference) is bool isBroken && isBroken)
                {
                    continue;
                }

                var assetProperty = referenceType.GetProperty("asset");
                var table = assetProperty?.GetValue(tableReference) as StringTable;
                if (table != null)
                {
                    yield return table;
                }
            }
        }

        private static StringTableSnapshot CreateTableSnapshot(
            SharedTableData sharedData,
            StringTable table)
        {
            var sharedEntries = sharedData == null
                ? Enumerable.Empty<SharedTableData.SharedTableEntry>()
                : sharedData.Entries.Where(entry => entry != null);

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
