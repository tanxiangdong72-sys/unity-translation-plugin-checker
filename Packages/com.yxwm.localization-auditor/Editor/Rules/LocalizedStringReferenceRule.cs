using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;

namespace Yxwm.LocalizationAuditor
{
    // 检查 Scene 和 Prefab 中 LocalizedString 的 Table/Entry 引用是否仍然有效。
    internal sealed class LocalizedStringReferenceRule : IAuditRule
    {
        public const string RuleId = "LOCALIZED_STRING_REFERENCE";

        private const string FixSuggestion =
            "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry.";

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
            var index = LocalizedStringReferenceIndex.Create(
                StringTableDataEnumerator.Enumerate());
            var issues = new List<AuditIssue>();
            foreach (var target in AuditTargetDiscovery.Discover(context.AssetPaths).Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<LocalizedStringReferenceSnapshot> references;
                if (target.Kind == AuditTargetKind.Scene)
                {
                    using (var scope = SceneReadOnlyScope.Open(target.AssetPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        references = LocalizedStringReferenceExtractor.ExtractFromScene(
                            target.AssetPath,
                            scope.Scene);
                        AddIssues(index, references, issues, cancellationToken);
                    }
                }
                else
                {
                    using (var scope = PrefabReadOnlyScope.Open(target.AssetPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        references = LocalizedStringReferenceExtractor.ExtractFromGameObject(
                            target.AssetPath,
                            scope.Root);
                        AddIssues(index, references, issues, cancellationToken);
                    }
                }
            }

            issues.Sort(LocalizedStringReferenceIssueComparer.Instance);
            return issues;
        }

        private static void AddIssues(
            LocalizedStringReferenceIndex index,
            IEnumerable<LocalizedStringReferenceSnapshot> references,
            ICollection<AuditIssue> issues,
            CancellationToken cancellationToken)
        {
            foreach (var reference in references ?? Enumerable.Empty<LocalizedStringReferenceSnapshot>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var issue = index.CreateIssue(reference);
                if (issue != null)
                {
                    issues.Add(issue);
                }
            }
        }

        private sealed class LocalizedStringReferenceIssueComparer : IComparer<AuditIssue>
        {
            internal static readonly LocalizedStringReferenceIssueComparer Instance =
                new LocalizedStringReferenceIssueComparer();

            public int Compare(AuditIssue left, AuditIssue right)
            {
                var comparison = Compare(left.Location.AssetPath, right.Location.AssetPath);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Compare(left.Location.ObjectPath, right.Location.ObjectPath);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Compare(left.Location.ComponentType, right.Location.ComponentType);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Compare(left.Location.PropertyPath, right.Location.PropertyPath);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Compare(left.Location.TableName, right.Location.TableName);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Compare(left.Location.Key, right.Location.Key);
                return comparison != 0
                    ? comparison
                    : Compare(left.Message, right.Message);
            }

            private static int Compare(string left, string right)
            {
                return StringComparer.Ordinal.Compare(left, right);
            }
        }
    }

    // 纯内存索引负责把 String Table 快照与序列化引用匹配，便于稳定单测。
    internal sealed class LocalizedStringReferenceIndex
    {
        private readonly IReadOnlyDictionary<string, StringTableCollectionSnapshot> _collectionsByName;
        private readonly IReadOnlyDictionary<string, StringTableCollectionSnapshot> _collectionsByGuid;

        private LocalizedStringReferenceIndex(
            IReadOnlyDictionary<string, StringTableCollectionSnapshot> collectionsByName,
            IReadOnlyDictionary<string, StringTableCollectionSnapshot> collectionsByGuid)
        {
            _collectionsByName = collectionsByName;
            _collectionsByGuid = collectionsByGuid;
        }

        internal static LocalizedStringReferenceIndex Create(
            IEnumerable<StringTableCollectionSnapshot> snapshots)
        {
            var byName = new Dictionary<string, StringTableCollectionSnapshot>(
                StringComparer.Ordinal);
            var byGuid = new Dictionary<string, StringTableCollectionSnapshot>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var snapshot in snapshots ?? Enumerable.Empty<StringTableCollectionSnapshot>())
            {
                if (!string.IsNullOrEmpty(snapshot.CollectionName))
                {
                    byName[snapshot.CollectionName] = snapshot;
                }

                if (!string.IsNullOrEmpty(snapshot.SharedDataAssetPath))
                {
                    var guid = AssetDatabase.AssetPathToGUID(snapshot.SharedDataAssetPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        byGuid[guid] = snapshot;
                    }
                }
            }

            return new LocalizedStringReferenceIndex(byName, byGuid);
        }

        internal AuditIssue CreateIssue(LocalizedStringReferenceSnapshot reference)
        {
            if (reference == null ||
                reference.TableReference == null ||
                reference.TableReference.IsEmpty)
            {
                return null;
            }

            var tableRawValue = reference.TableReference.RawValue;
            var table = ResolveCollection(reference.TableReference);
            if (reference.TableEntryReference == null)
            {
                return null;
            }

            var location = CreateLocation(reference, tableRawValue, reference.TableEntryReference);
            if (table == null)
            {
                return new AuditIssue(
                    LocalizedStringReferenceRule.RuleId,
                    AuditSeverity.Error,
                    "LocalizedString table reference '" + tableRawValue +
                    "' does not resolve to a String Table collection.",
                    "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry.",
                    location);
            }

            if (reference.TableEntryReference.IsEmpty)
            {
                return new AuditIssue(
                    LocalizedStringReferenceRule.RuleId,
                    AuditSeverity.Error,
                    "LocalizedString entry reference is empty while table reference '" +
                    tableRawValue + "' is configured.",
                    "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry.",
                    location);
            }

            var sharedEntry = reference.TableEntryReference.Type == TableEntryReferenceSnapshotType.Name
                ? table.SharedEntries.FirstOrDefault(entry =>
                    string.Equals(
                        entry.Key,
                        reference.TableEntryReference.RawKey,
                        StringComparison.Ordinal))
                : table.SharedEntries.FirstOrDefault(entry =>
                    entry.KeyId == reference.TableEntryReference.RawKeyId);
            if (sharedEntry != null)
            {
                return null;
            }

            if (reference.TableEntryReference.Type == TableEntryReferenceSnapshotType.Name)
            {
                return new AuditIssue(
                    LocalizedStringReferenceRule.RuleId,
                    AuditSeverity.Error,
                    "LocalizedString entry name '" +
                    reference.TableEntryReference.RawKey +
                    "' does not exist in String Table collection '" +
                    table.CollectionName + "'.",
                    "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry.",
                    location);
            }

            return new AuditIssue(
                LocalizedStringReferenceRule.RuleId,
                AuditSeverity.Error,
                "LocalizedString entry id '" +
                reference.TableEntryReference.RawKeyId +
                "' does not exist in table reference '" +
                tableRawValue +
                "' (resolved collection '" +
                table.CollectionName + "').",
                "Fix the LocalizedString Table/Key reference or restore the corresponding String Table/Entry.",
                location);
        }

        private StringTableCollectionSnapshot ResolveCollection(
            TableReferenceSnapshot tableReference)
        {
            if (tableReference.Type == TableReferenceSnapshotType.Name)
            {
                _collectionsByName.TryGetValue(tableReference.RawValue, out var collection);
                return collection;
            }

            var rawGuid = tableReference.RawValue.Substring("GUID:".Length);
            _collectionsByGuid.TryGetValue(rawGuid, out var guidCollection);
            return guidCollection;
        }

        private static AuditIssueLocation CreateLocation(
            LocalizedStringReferenceSnapshot reference,
            string tableRawValue,
            TableEntryReferenceSnapshot entryReference)
        {
            return new AuditIssueLocation(
                tableName: tableRawValue,
                key: entryReference.Type == TableEntryReferenceSnapshotType.Id
                    ? entryReference.RawKeyId.ToString()
                    : entryReference.RawKey,
                assetPath: reference.AssetPath,
                objectPath: reference.ObjectPath,
                componentType: reference.ComponentType,
                propertyPath: reference.SerializedPropertyPath);
        }
    }
}
