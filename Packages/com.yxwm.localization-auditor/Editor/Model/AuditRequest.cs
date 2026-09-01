using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditRequest
    {
        public IReadOnlyList<string> AssetPaths { get; }
        public IReadOnlyList<string> EnabledRuleIds { get; }
        public IReadOnlyDictionary<string, string> LocaleFontAssetPaths { get; }

        public AuditRequest(
            IEnumerable<string> assetPaths = null,
            IEnumerable<string> enabledRuleIds = null,
            IReadOnlyDictionary<string, string> localeFontAssetPaths = null)
        {
            AssetPaths = SnapshotStrings(assetPaths, nameof(assetPaths));
            EnabledRuleIds = SnapshotStrings(enabledRuleIds, nameof(enabledRuleIds));
            LocaleFontAssetPaths = SnapshotFontMappings(localeFontAssetPaths);
        }

        private static IReadOnlyList<string> SnapshotStrings(
            IEnumerable<string> values,
            string parameterName)
        {
            if (values == null)
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            var snapshot = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        "Values cannot be null, empty, or whitespace.",
                        parameterName);
                }

                snapshot.Add(value);
            }

            var sortedValues = snapshot.ToArray();
            Array.Sort(sortedValues, StringComparer.Ordinal);
            return Array.AsReadOnly(sortedValues);
        }

        private static IReadOnlyDictionary<string, string> SnapshotFontMappings(
            IReadOnlyDictionary<string, string> mappings)
        {
            if (mappings == null)
            {
                return new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(StringComparer.Ordinal));
            }

            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.Key))
                {
                    throw new ArgumentException(
                        "Locale codes cannot be null, empty, or whitespace.",
                        nameof(mappings));
                }

                if (string.IsNullOrWhiteSpace(mapping.Value))
                {
                    throw new ArgumentException(
                        "Font asset paths cannot be null, empty, or whitespace.",
                        nameof(mappings));
                }

                if (!snapshot.TryAdd(mapping.Key, mapping.Value))
                {
                    throw new ArgumentException(
                        "Locale font mappings cannot contain duplicate locale codes.",
                        nameof(mappings));
                }
            }

            var orderedSnapshot = snapshot
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return new ReadOnlyDictionary<string, string>(orderedSnapshot);
        }
    }
}
