using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    internal sealed class SharedTableEntrySnapshot
    {
        internal SharedTableEntrySnapshot(long keyId, string key)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
        }

        public long KeyId { get; }
        public string Key { get; }
    }

    internal sealed class StringTableEntrySnapshot
    {
        internal StringTableEntrySnapshot(
            long keyId,
            string key,
            bool exists,
            string localizedValue)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
            Exists = exists;
            LocalizedValue = localizedValue;
        }

        public long KeyId { get; }
        public string Key { get; }
        public bool Exists { get; }
        public string LocalizedValue { get; }
        public bool IsEmpty => Exists && string.IsNullOrWhiteSpace(LocalizedValue);
    }

    internal sealed class StringTableSnapshot
    {
        private readonly IReadOnlyList<StringTableEntrySnapshot> _entries;

        internal StringTableSnapshot(
            string localeCode,
            string assetPath,
            IEnumerable<StringTableEntrySnapshot> entries)
        {
            LocaleCode = localeCode ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            _entries = SnapshotEntries(entries);
        }

        public string LocaleCode { get; }
        public string AssetPath { get; }
        public IReadOnlyList<StringTableEntrySnapshot> Entries => _entries;

        public StringTableEntrySnapshot GetEntry(string key)
        {
            if (key == null)
            {
                return null;
            }

            return _entries.FirstOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.Ordinal));
        }

        private static IReadOnlyList<StringTableEntrySnapshot> SnapshotEntries(
            IEnumerable<StringTableEntrySnapshot> entries)
        {
            var snapshot = entries == null
                ? new List<StringTableEntrySnapshot>()
                : new List<StringTableEntrySnapshot>(entries);
            snapshot.Sort((left, right) =>
            {
                var comparison = StringComparer.Ordinal.Compare(left.Key, right.Key);
                return comparison != 0
                    ? comparison
                    : left.KeyId.CompareTo(right.KeyId);
            });
            return new ReadOnlyCollection<StringTableEntrySnapshot>(snapshot);
        }
    }

    internal sealed class StringTableCollectionSnapshot
    {
        private readonly IReadOnlyList<SharedTableEntrySnapshot> _sharedEntries;
        private readonly IReadOnlyList<StringTableSnapshot> _tables;

        internal StringTableCollectionSnapshot(
            string collectionName,
            string assetPath,
            string sharedDataAssetPath,
            IEnumerable<SharedTableEntrySnapshot> sharedEntries,
            IEnumerable<StringTableSnapshot> tables)
        {
            CollectionName = collectionName ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            SharedDataAssetPath = sharedDataAssetPath ?? string.Empty;
            _sharedEntries = SnapshotSharedEntries(sharedEntries);
            _tables = SnapshotTables(tables);
        }

        public string CollectionName { get; }
        public string AssetPath { get; }
        public string SharedDataAssetPath { get; }
        public IReadOnlyList<SharedTableEntrySnapshot> SharedEntries => _sharedEntries;
        public IReadOnlyList<StringTableSnapshot> Tables => _tables;

        public StringTableSnapshot GetTable(string localeCode)
        {
            if (localeCode == null)
            {
                return null;
            }

            return _tables.FirstOrDefault(table =>
                string.Equals(table.LocaleCode, localeCode, StringComparison.Ordinal));
        }

        private static IReadOnlyList<SharedTableEntrySnapshot> SnapshotSharedEntries(
            IEnumerable<SharedTableEntrySnapshot> entries)
        {
            var snapshot = entries == null
                ? new List<SharedTableEntrySnapshot>()
                : new List<SharedTableEntrySnapshot>(entries);
            snapshot.Sort((left, right) =>
            {
                var comparison = StringComparer.Ordinal.Compare(left.Key, right.Key);
                return comparison != 0
                    ? comparison
                    : left.KeyId.CompareTo(right.KeyId);
            });
            return new ReadOnlyCollection<SharedTableEntrySnapshot>(snapshot);
        }

        private static IReadOnlyList<StringTableSnapshot> SnapshotTables(
            IEnumerable<StringTableSnapshot> tables)
        {
            var snapshot = tables == null
                ? new List<StringTableSnapshot>()
                : new List<StringTableSnapshot>(tables);
            snapshot.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.LocaleCode, right.LocaleCode));
            return new ReadOnlyCollection<StringTableSnapshot>(snapshot);
        }
    }
}
