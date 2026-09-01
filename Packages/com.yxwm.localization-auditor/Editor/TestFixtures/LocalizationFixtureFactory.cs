using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yxwm.LocalizationAuditor
{
    internal static class LocalizationFixtureFactory
    {
        private const string AllowedRoot = "Assets/LocalizationAuditorTestFixtures";

        public static LocalizationFixture Create(
            string rootDirectory,
            string collectionName,
            params string[] localeCodes)
        {
            ValidateRootDirectory(rootDirectory);
            ValidateCollectionName(collectionName);
            var distinctLocaleCodes = ValidateLocaleCodes(localeCodes);
            var hadAddressablesSetup =
                AddressableAssetSettingsDefaultObject.SettingsExists ||
                AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);

            if (AssetDatabase.IsValidFolder(rootDirectory))
            {
                throw new ArgumentException(
                    "Fixture root already exists. Clean it before creating a fixture.",
                    nameof(rootDirectory));
            }

            EnsureFolder(rootDirectory);
            var localesDirectory = rootDirectory + "/Locales";
            var tablesDirectory = rootDirectory + "/Tables";
            EnsureFolder(localesDirectory);
            EnsureFolder(tablesDirectory);

            var locales = new List<Locale>();
            try
            {
                foreach (var localeCode in distinctLocaleCodes)
                {
                    var locale = Locale.CreateLocale(localeCode);
                    var localePath = localesDirectory + "/" + localeCode + ".asset";
                    AssetDatabase.CreateAsset(locale, localePath);
                    LocalizationEditorSettings.AddLocale(locale);
                    locales.Add(locale);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var collection = LocalizationEditorSettings.CreateStringTableCollection(
                    collectionName,
                    tablesDirectory,
                    locales);
                if (collection == null)
                {
                    throw new InvalidOperationException(
                        "Unity Localization did not create the String Table Collection.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var tables = new Dictionary<string, StringTable>(StringComparer.Ordinal);
                foreach (var locale in locales)
                {
                    var table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            "Unity Localization did not create a table for locale '" +
                            locale.Identifier.Code +
                            "'.");
                    }

                    tables.Add(locale.Identifier.Code, table);
                }

                return new LocalizationFixture(
                    rootDirectory,
                    collection,
                    locales,
                    tables,
                    !hadAddressablesSetup);
            }
            catch
            {
                LocalizationFixtureFactoryCleanup.Remove(
                    rootDirectory,
                    locales,
                    !hadAddressablesSetup);
                throw;
            }
        }

        private static void ValidateRootDirectory(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Fixture root is required.", nameof(rootDirectory));
            }

            var normalizedRoot = rootDirectory.Replace('\\', '/').TrimEnd('/');
            if (normalizedRoot.Contains("..", StringComparison.Ordinal) ||
                (!normalizedRoot.Equals(AllowedRoot, StringComparison.Ordinal) &&
                 !normalizedRoot.StartsWith(AllowedRoot + "/", StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Fixture assets must be created below " + AllowedRoot + ".",
                    nameof(rootDirectory));
            }
        }

        private static void ValidateCollectionName(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName) ||
                collectionName.Contains('/', StringComparison.Ordinal) ||
                collectionName.Contains('\\', StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Collection name must be a simple non-empty name.",
                    nameof(collectionName));
            }
        }

        private static IReadOnlyList<string> ValidateLocaleCodes(string[] localeCodes)
        {
            if (localeCodes == null || localeCodes.Length == 0)
            {
                throw new ArgumentException(
                    "At least one locale code is required.",
                    nameof(localeCodes));
            }

            var distinctCodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var localeCode in localeCodes)
            {
                if (string.IsNullOrWhiteSpace(localeCode) ||
                    localeCode.Contains('/', StringComparison.Ordinal) ||
                    localeCode.Contains('\\', StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Locale codes must be non-empty file-safe codes.",
                        nameof(localeCodes));
                }

                if (!distinctCodes.Add(localeCode))
                {
                    throw new ArgumentException(
                        "Locale codes must be unique.",
                        nameof(localeCodes));
                }
            }

            var sortedCodes = distinctCodes.ToArray();
            Array.Sort(sortedCodes, StringComparer.Ordinal);
            return Array.AsReadOnly(sortedCodes);
        }

        private static void EnsureFolder(string assetPath)
        {
            var normalizedPath = assetPath.Replace('\\', '/').TrimEnd('/');
            var segments = normalizedPath.Split('/');
            var currentPath = segments[0];

            for (var index = 1; index < segments.Length; index++)
            {
                var nextPath = currentPath + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }
    }

    internal sealed class LocalizationFixture : IDisposable
    {
        private bool _disposed;
        private readonly List<Locale> _locales;
        private readonly Dictionary<string, StringTable> _tables;
        private readonly bool _removeAddressablesOnDispose;

        internal LocalizationFixture(
            string rootDirectory,
            StringTableCollection stringTableCollection,
            IEnumerable<Locale> locales,
            IDictionary<string, StringTable> tables,
            bool removeAddressablesOnDispose)
        {
            RootDirectory = rootDirectory;
            StringTableCollection = stringTableCollection;
            _locales = new List<Locale>(locales);
            _tables = new Dictionary<string, StringTable>(tables, StringComparer.Ordinal);
            _removeAddressablesOnDispose = removeAddressablesOnDispose;
        }

        public string RootDirectory { get; }
        public StringTableCollection StringTableCollection { get; }
        public IReadOnlyList<Locale> Locales => new ReadOnlyCollection<Locale>(_locales);
        public IReadOnlyDictionary<string, StringTable> Tables =>
            new ReadOnlyDictionary<string, StringTable>(_tables);

        public StringTable GetTable(string localeCode)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(localeCode) ||
                !_tables.TryGetValue(localeCode, out var table))
            {
                throw new ArgumentException(
                    "The fixture does not contain locale '" + localeCode + "'.",
                    nameof(localeCode));
            }

            return table;
        }

        public StringTableEntry AddEntry(
            string key,
            string localeCode,
            string localizedValue)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Entry key is required.", nameof(key));
            }

            var table = GetTable(localeCode);
            var entry = table.AddEntry(key, localizedValue);

            // Unity 的表和共享 Key 数据都需要标记为 dirty 才能可靠保存。
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
            AssetDatabase.SaveAssets();
            return entry;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LocalizationFixtureFactoryCleanup.Remove(
                RootDirectory,
                _locales,
                _removeAddressablesOnDispose);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LocalizationFixture));
            }
        }
    }

    internal static class LocalizationFixtureFactoryCleanup
    {
        public static void Remove(
            string rootDirectory,
            IEnumerable<Locale> locales,
            bool removeAddressables)
        {
            // 先解除 Addressables/Localization 注册，再删除磁盘上的资源文件。
            foreach (var locale in locales.Reverse())
            {
                LocalizationEditorSettings.RemoveLocale(locale);
                var localePath = AssetDatabase.GetAssetPath(locale);
                if (!string.IsNullOrEmpty(localePath))
                {
                    AssetDatabase.DeleteAsset(localePath);
                }
            }

            AssetDatabase.DeleteAsset(rootDirectory);
            var rootMetaPath = rootDirectory + ".meta";
            if (File.Exists(rootMetaPath))
            {
                File.Delete(rootMetaPath);
            }

            DeleteEmptyFixtureParent(rootDirectory);

            if (removeAddressables)
            {
                // 测试工程原本没有 Addressables 时，连同自动生成的项目配置一起恢复。
                EditorBuildSettings.RemoveConfigObject(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
                EditorBuildSettings.RemoveConfigObject(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName);
                AssetDatabase.DeleteAsset(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
                ResetAddressableSettingsCache();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void DeleteEmptyFixtureParent(string rootDirectory)
        {
            const string fixtureParent = "Assets/LocalizationAuditorTestFixtures";
            var separatorIndex = rootDirectory.LastIndexOf('/');
            if (separatorIndex < 0 ||
                !rootDirectory.Substring(0, separatorIndex)
                    .Equals(fixtureParent, StringComparison.Ordinal))
            {
                return;
            }

            if (!Directory.Exists(fixtureParent) ||
                Directory.GetFileSystemEntries(fixtureParent).Length != 0)
            {
                return;
            }

            AssetDatabase.DeleteAsset(fixtureParent);
            var parentMetaPath = fixtureParent + ".meta";
            if (File.Exists(parentMetaPath))
            {
                File.Delete(parentMetaPath);
            }
        }

        private static void ResetAddressableSettingsCache()
        {
            // Addressables 使用私有静态缓存；删除配置后必须清空它，后续测试才能重新创建干净实例。
            var field = typeof(AddressableAssetSettingsDefaultObject).GetField(
                "s_DefaultSettingsObject",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
