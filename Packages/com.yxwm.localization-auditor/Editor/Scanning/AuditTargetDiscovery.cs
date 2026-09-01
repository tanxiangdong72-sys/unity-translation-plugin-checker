using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Yxwm.LocalizationAuditor
{
    internal enum AuditTargetKind
    {
        Scene = 0,
        Prefab = 1
    }

    internal sealed class AuditTarget
    {
        internal AuditTarget(string assetPath, AuditTargetKind kind)
        {
            AssetPath = assetPath;
            Kind = kind;
        }

        public string AssetPath { get; }
        public AuditTargetKind Kind { get; }
    }

    internal sealed class AuditTargetDiscoveryResult
    {
        internal AuditTargetDiscoveryResult(
            IEnumerable<AuditTarget> targets,
            IEnumerable<AuditDiagnostic> diagnostics)
        {
            Targets = new ReadOnlyCollection<AuditTarget>(
                (targets ?? Enumerable.Empty<AuditTarget>())
                .OrderBy(target => target.AssetPath, StringComparer.Ordinal)
                .ToList());
            Diagnostics = new ReadOnlyCollection<AuditDiagnostic>(
                (diagnostics ?? Enumerable.Empty<AuditDiagnostic>())
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.AssetPath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToList());
        }

        public IReadOnlyList<AuditTarget> Targets { get; }
        public IReadOnlyList<AuditDiagnostic> Diagnostics { get; }
    }

    internal static class AuditTargetDiscovery
    {
        public static AuditTargetDiscoveryResult Discover(
            IEnumerable<string> requestedPaths)
        {
            var targetsByPath = new Dictionary<string, AuditTarget>(
                StringComparer.Ordinal);
            var diagnostics = new List<AuditDiagnostic>();

            foreach (var requestedPath in requestedPaths ?? Enumerable.Empty<string>())
            {
                var assetPath = NormalizeAssetPath(requestedPath);
                if (!assetPath.StartsWith("Assets", StringComparison.Ordinal) ||
                    (assetPath.Length > "Assets".Length &&
                     assetPath["Assets".Length] != '/'))
                {
                    diagnostics.Add(new AuditDiagnostic(
                        "TARGET_PATH_OUTSIDE_ASSETS",
                        "The requested target path is outside the Assets folder.",
                        assetPath));
                    continue;
                }

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    AddFolderTargets(assetPath, targetsByPath);
                    continue;
                }

                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
                {
                    diagnostics.Add(new AuditDiagnostic(
                        "TARGET_PATH_NOT_FOUND",
                        "The requested target path does not exist.",
                        assetPath));
                    continue;
                }

                AddTarget(assetPath, targetsByPath);
            }

            return new AuditTargetDiscoveryResult(
                targetsByPath.Values,
                diagnostics);
        }

        private static void AddFolderTargets(
            string folderPath,
            IDictionary<string, AuditTarget> targetsByPath)
        {
            AddFoundAssets(
                AssetDatabase.FindAssets("t:Scene", new[] { folderPath }),
                AuditTargetKind.Scene,
                targetsByPath);
            AddFoundAssets(
                AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }),
                AuditTargetKind.Prefab,
                targetsByPath);
        }

        private static void AddFoundAssets(
            IEnumerable<string> guids,
            AuditTargetKind kind,
            IDictionary<string, AuditTarget> targetsByPath)
        {
            foreach (var guid in guids ?? Enumerable.Empty<string>())
            {
                var assetPath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (GetTargetKind(assetPath) == kind)
                {
                    targetsByPath[assetPath] = new AuditTarget(assetPath, kind);
                }
            }
        }

        private static void AddTarget(
            string assetPath,
            IDictionary<string, AuditTarget> targetsByPath)
        {
            var kind = GetTargetKind(assetPath);
            if (!kind.HasValue)
            {
                return;
            }

            targetsByPath[assetPath] = new AuditTarget(assetPath, kind.Value);
        }

        private static AuditTargetKind? GetTargetKind(string assetPath)
        {
            var extension = Path.GetExtension(assetPath);
            if (string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase))
            {
                return AuditTargetKind.Scene;
            }

            if (string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return AuditTargetKind.Prefab;
            }

            return null;
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
        }
    }
}
