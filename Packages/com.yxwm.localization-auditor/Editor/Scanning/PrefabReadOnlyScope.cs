using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    internal sealed class PrefabReadOnlyScope : IDisposable
    {
        private GameObject _root;
        private bool _disposed;

        private PrefabReadOnlyScope(
            string assetPath,
            GameObject root)
        {
            AssetPath = assetPath;
            _root = root;
        }

        public string AssetPath { get; }
        public GameObject Root => _root;

        public static PrefabReadOnlyScope Open(string assetPath)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            ValidatePrefabPath(normalizedPath);

            var root = PrefabUtility.LoadPrefabContents(normalizedPath);
            if (root == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load Prefab contents at '" + normalizedPath + "'.");
            }

            return new PrefabReadOnlyScope(normalizedPath, root);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_root == null)
            {
                return;
            }

            // 只卸载内存中的 Prefab 内容，不调用 SaveAsPrefabAsset 或其他保存 API。
            PrefabUtility.UnloadPrefabContents(_root);
            _root = null;
        }

        private static void ValidatePrefabPath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(
                    Path.GetExtension(assetPath),
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Prefab path must be an existing .prefab asset below Assets.",
                    nameof(assetPath));
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                throw new ArgumentException(
                    "Prefab path does not exist: " + assetPath,
                    nameof(assetPath));
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
        }
    }
}
