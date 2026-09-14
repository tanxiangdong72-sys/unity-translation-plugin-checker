using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    // 显式保存用户选择；扫描只读取配置，不修改字体或翻译资源。
    [CreateAssetMenu(menuName = "Localization Auditor/Font Profile", fileName = "AuditFontProfile")]
    public sealed class AuditFontProfile : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public string LocaleCode;
            public TMP_FontAsset Font;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public IReadOnlyDictionary<string, string> BuildMappings()
        {
            var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (entry == null) continue;
                var code = entry.LocaleCode?.Trim();
                // 新增的空白行不影响其他规则；未配置字体仍由规则报告 NotVerified。
                if (string.IsNullOrEmpty(code) && entry.Font == null) continue;
                if (string.IsNullOrEmpty(code))
                    throw new InvalidOperationException("Font requires a Locale code / 请填写语言代码。");
                if (!codes.Add(code))
                    throw new InvalidOperationException("Duplicate Locale code / 语言代码重复: " + code);
                if (entry.Font == null) continue;
                var path = AssetDatabase.GetAssetPath(entry.Font);
                // 路径型审计请求不能唯一表示子资源字体，禁止悄悄加载成其他字体。
                if (string.IsNullOrEmpty(path) ||
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != entry.Font)
                    throw new InvalidOperationException("Use a saved, independently addressable TMP Font Asset / 请使用可独立加载的已保存字体: " + code);
                mappings.Add(code, path);
            }
            return mappings;
        }
    }
}
