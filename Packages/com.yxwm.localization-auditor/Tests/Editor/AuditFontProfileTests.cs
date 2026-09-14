using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 使用独立临时资源验证配置持久化和扫描快照，不触碰用户字体。
    public sealed class AuditFontProfileTests
    {
        private string _folder;
        private AuditFontProfile _profile;
        private TMP_FontAsset _font;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/AuditFontProfileTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
            _profile = ScriptableObject.CreateInstance<AuditFontProfile>();
            _font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            AssetDatabase.CreateAsset(_profile, _folder + "/Profile.asset");
            AssetDatabase.CreateAsset(_font, _folder + "/Font.asset");
        }

        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(_folder); }

        private void SetEntries(params (string code, TMP_FontAsset font)[] entries)
        {
            using (var serialized = new SerializedObject(_profile))
            {
                var list = serialized.FindProperty("_entries");
                list.arraySize = entries.Length;
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("LocaleCode").stringValue = entries[i].code;
                    entry.FindPropertyRelative("Font").objectReferenceValue = entries[i].font;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [Test]
        public void EmptyAndUnassignedFontsDoNotPretendToBeVerified()
        {
            Assert.That(_profile.BuildMappings(), Is.Empty);
            SetEntries(("", null), ("zh-Hans", null));
            Assert.That(_profile.BuildMappings(), Is.Empty);
        }

        [Test]
        public void DuplicateTrimmedCodesAreRejectedEvenWithUnassignedFont()
        {
            SetEntries(("en", _font), (" en ", null));
            Assert.Throws<InvalidOperationException>(() => _profile.BuildMappings());
        }

        [Test]
        public void FontWithoutCodeIsRejected()
        {
            SetEntries(("  ", _font));
            Assert.Throws<InvalidOperationException>(() => _profile.BuildMappings());
        }

        [Test]
        public void UnsavedFontIsRejected()
        {
            var transient = ScriptableObject.CreateInstance<TMP_FontAsset>();
            var savedProfile = _profile;
            _profile = ScriptableObject.CreateInstance<AuditFontProfile>();
            try
            {
                SetEntries(("en", transient));
                Assert.Throws<InvalidOperationException>(() => _profile.BuildMappings());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(_profile);
                _profile = savedProfile;
                UnityEngine.Object.DestroyImmediate(transient);
            }
        }

        [Test]
        public void SavedProfileSurvivesReimportAndMappingDoesNotModifyAssets()
        {
            SetEntries((" zh-Hans ", _font));
            AssetDatabase.SaveAssetIfDirty(_profile);
            var path = AssetDatabase.GetAssetPath(_profile);
            var before = File.ReadAllBytes(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<AuditFontProfile>(path);
            Assert.That(loaded.BuildMappings()["zh-Hans"], Is.EqualTo(_folder + "/Font.asset"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(loaded), Is.False);
        }

        [Test]
        public void EachMappingIsAnIndependentSnapshot()
        {
            SetEntries(("en", _font));
            var first = _profile.BuildMappings();
            SetEntries(("zh-Hans", _font));
            Assert.That(first.ContainsKey("en"), Is.True);
            Assert.That(first.ContainsKey("zh-Hans"), Is.False);
        }

        [Test]
        public void WindowStateCopiesMappingsIntoRequestAndCanClearThem()
        {
            var state = new LocalizationAuditorWindowState(
                discoverTargets: _ => new AuditTargetDiscoveryResult(
                    new[] { new AuditTarget("Assets/Test.prefab", AuditTargetKind.Prefab) },
                    Array.Empty<AuditDiagnostic>()),
                runAudit: request => new AuditReport(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    AuditRunStatus.Completed, 1, Array.Empty<AuditIssue>()));
            var mappings = new Dictionary<string, string> { ["en"] = _folder + "/Font.asset" };
            state.SetFontMappings(mappings);
            mappings.Clear();
            state.RefreshTargets();
            state.RunAudit();
            Assert.That(state.LastRequest.LocaleFontAssetPaths["en"], Is.EqualTo(_folder + "/Font.asset"));
            state.SetFontMappings(null);
            state.RunAudit();
            Assert.That(state.LastRequest.LocaleFontAssetPaths, Is.Empty);
        }
    }
}
