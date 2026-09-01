using System.Collections.Generic;
using System.Collections.ObjectModel;
using TMPro;
using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    internal sealed class TmpFontAssetResolution
    {
        internal TmpFontAssetResolution(
            TMP_FontAsset rootFontAsset,
            IEnumerable<TMP_FontAsset> fontAssets,
            bool hasFallbackCycle)
        {
            RootFontAsset = rootFontAsset;
            FontAssets = new ReadOnlyCollection<TMP_FontAsset>(
                fontAssets == null
                    ? new List<TMP_FontAsset>()
                    : new List<TMP_FontAsset>(fontAssets));
            HasFallbackCycle = hasFallbackCycle;
        }

        public TMP_FontAsset RootFontAsset { get; }
        public IReadOnlyList<TMP_FontAsset> FontAssets { get; }
        public bool IsRootMissing => RootFontAsset == null;
        public bool HasFallbackCycle { get; }
    }

    internal static class TmpFontAssetResolver
    {
        public static TmpFontAssetResolution Resolve(TMP_FontAsset rootFontAsset)
        {
            if (rootFontAsset == null)
            {
                return new TmpFontAssetResolution(
                    null,
                    new List<TMP_FontAsset>(),
                    false);
            }

            var resolvedFonts = new List<TMP_FontAsset>();
            var visitedEntityIds = new HashSet<EntityId>();
            var visitingEntityIds = new HashSet<EntityId>();
            var hasFallbackCycle = false;

            Visit(rootFontAsset);

            return new TmpFontAssetResolution(
                rootFontAsset,
                resolvedFonts,
                hasFallbackCycle);

            void Visit(TMP_FontAsset fontAsset)
            {
                if (fontAsset == null)
                {
                    return;
                }

                var entityId = fontAsset.GetEntityId();
                if (visitingEntityIds.Contains(entityId))
                {
                    // 当前路径再次遇到正在访问的字体，说明 fallback 形成了环。
                    hasFallbackCycle = true;
                    return;
                }

                if (!visitedEntityIds.Add(entityId))
                {
                    return;
                }

                resolvedFonts.Add(fontAsset);
                visitingEntityIds.Add(entityId);

                var fallbackFonts = fontAsset.fallbackFontAssetTable;
                if (fallbackFonts != null)
                {
                    foreach (var fallbackFont in fallbackFonts)
                    {
                        Visit(fallbackFont);
                    }
                }

                visitingEntityIds.Remove(entityId);
            }
        }
    }
}
