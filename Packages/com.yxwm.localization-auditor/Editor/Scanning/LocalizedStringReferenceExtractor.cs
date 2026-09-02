using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yxwm.LocalizationAuditor
{
    internal enum TableReferenceSnapshotType
    {
        Empty,
        Guid,
        Name
    }

    internal enum TableEntryReferenceSnapshotType
    {
        Empty,
        Id,
        Name
    }

    internal sealed class TableReferenceSnapshot
    {
        internal TableReferenceSnapshot(string rawValue)
        {
            RawValue = rawValue ?? string.Empty;
            Type = string.IsNullOrEmpty(RawValue)
                ? TableReferenceSnapshotType.Empty
                : RawValue.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
                    ? TableReferenceSnapshotType.Guid
                    : TableReferenceSnapshotType.Name;
        }

        public string RawValue { get; }
        public TableReferenceSnapshotType Type { get; }
        public bool IsEmpty => Type == TableReferenceSnapshotType.Empty;
    }

    internal sealed class TableEntryReferenceSnapshot
    {
        internal TableEntryReferenceSnapshot(string rawKey, long rawKeyId)
        {
            RawKey = rawKey ?? string.Empty;
            RawKeyId = rawKeyId;
            Type = rawKeyId != 0
                ? TableEntryReferenceSnapshotType.Id
                : string.IsNullOrEmpty(RawKey)
                    ? TableEntryReferenceSnapshotType.Empty
                    : TableEntryReferenceSnapshotType.Name;
        }

        public string RawKey { get; }
        public long RawKeyId { get; }
        public TableEntryReferenceSnapshotType Type { get; }
        public bool IsEmpty => Type == TableEntryReferenceSnapshotType.Empty;
    }

    internal sealed class LocalizedStringReferenceSnapshot
    {
        internal LocalizedStringReferenceSnapshot(
            string assetPath,
            string objectPath,
            string componentType,
            string serializedPropertyPath,
            TableReferenceSnapshot tableReference,
            TableEntryReferenceSnapshot tableEntryReference)
        {
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            ComponentType = componentType ?? string.Empty;
            SerializedPropertyPath = serializedPropertyPath ?? string.Empty;
            TableReference = tableReference;
            TableEntryReference = tableEntryReference;
        }

        public string AssetPath { get; }
        public string ObjectPath { get; }
        public string ComponentType { get; }
        public string SerializedPropertyPath { get; }
        public TableReferenceSnapshot TableReference { get; }
        public TableEntryReferenceSnapshot TableEntryReference { get; }
    }

    internal static class LocalizedStringReferenceExtractor
    {
        public static IReadOnlyList<LocalizedStringReferenceSnapshot> ExtractFromGameObject(
            string assetPath,
            GameObject root)
        {
            if (root == null)
            {
                return EmptyResults();
            }

            var results = new List<LocalizedStringReferenceSnapshot>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                ExtractFromComponent(
                    assetPath,
                    root.transform,
                    component,
                    results);
            }

            return SortResults(results);
        }

        public static IReadOnlyList<LocalizedStringReferenceSnapshot> ExtractFromScene(
            string assetPath,
            Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return EmptyResults();
            }

            var results = new List<LocalizedStringReferenceSnapshot>();
            foreach (var root in scene.GetRootGameObjects())
            {
                results.AddRange(ExtractFromGameObject(assetPath, root));
            }

            return SortResults(results);
        }

        private static void ExtractFromComponent(
            string assetPath,
            Transform scanRoot,
            Component component,
            ICollection<LocalizedStringReferenceSnapshot> results)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.GetIterator();
            while (property.Next(true))
            {
                if (property.name != "m_TableReference")
                {
                    continue;
                }

                var tableName = property.FindPropertyRelative("m_TableCollectionName");
                var entry = property.serializedObject.FindProperty(
                    property.propertyPath.Replace(
                        "m_TableReference",
                        "m_TableEntryReference",
                        StringComparison.Ordinal));
                var key = entry?.FindPropertyRelative("m_Key");
                var keyId = entry?.FindPropertyRelative("m_KeyId");
                if (tableName == null || entry == null || key == null || keyId == null)
                {
                    continue;
                }

                results.Add(new LocalizedStringReferenceSnapshot(
                    assetPath,
                    GetObjectPath(scanRoot, component.transform),
                    component.GetType().FullName ?? component.GetType().Name,
                    property.propertyPath,
                    new TableReferenceSnapshot(tableName.stringValue),
                    new TableEntryReferenceSnapshot(key.stringValue, keyId.longValue)));
            }
        }

        private static string GetObjectPath(Transform root, Transform target)
        {
            var names = new List<string>();
            for (var current = target; current != null; current = current.parent)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static IReadOnlyList<LocalizedStringReferenceSnapshot> SortResults(
            IEnumerable<LocalizedStringReferenceSnapshot> results)
        {
            var sorted = results
                .OrderBy(item => item.AssetPath, StringComparer.Ordinal)
                .ThenBy(item => item.ObjectPath, StringComparer.Ordinal)
                .ThenBy(item => item.ComponentType, StringComparer.Ordinal)
                .ThenBy(item => item.SerializedPropertyPath, StringComparer.Ordinal)
                .ThenBy(item => item.TableReference.RawValue, StringComparer.Ordinal)
                .ThenBy(item => item.TableEntryReference.RawKey, StringComparer.Ordinal)
                .ThenBy(item => item.TableEntryReference.RawKeyId)
                .ToList();
            return new ReadOnlyCollection<LocalizedStringReferenceSnapshot>(sorted);
        }

        private static IReadOnlyList<LocalizedStringReferenceSnapshot> EmptyResults()
        {
            return new ReadOnlyCollection<LocalizedStringReferenceSnapshot>(
                new List<LocalizedStringReferenceSnapshot>());
        }
    }
}
