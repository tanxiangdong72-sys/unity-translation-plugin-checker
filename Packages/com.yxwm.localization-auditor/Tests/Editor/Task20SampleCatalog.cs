using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yxwm.LocalizationAuditor.Tests
{
    public enum Task20SampleCategory
    {
        ExpectedError = 0,
        ExpectedClean = 1,
        FontCoverage = 2,
        Boundary = 3
    }

    public enum Task20SampleDisposition
    {
        Issue = 0,
        NoIssue = 1,
        ManualReview = 2
    }

    // 每个样本都记录输入、预期和规则归属，便于后续统计召回率与误报率。
    public sealed class Task20Sample
    {
        internal Task20Sample(
            string id,
            Task20SampleCategory category,
            string title,
            string ruleId,
            string localeCode,
            string input,
            string expectedBehavior,
            Task20SampleDisposition expectedDisposition,
            AuditSeverity? expectedSeverity)
        {
            Id = id;
            Category = category;
            Title = title;
            RuleId = ruleId;
            LocaleCode = localeCode ?? string.Empty;
            Input = input;
            ExpectedBehavior = expectedBehavior;
            ExpectedDisposition = expectedDisposition;
            ExpectedSeverity = expectedSeverity;
        }

        public string Id { get; }
        public Task20SampleCategory Category { get; }
        public string Title { get; }
        public string RuleId { get; }
        public string LocaleCode { get; }
        public string Input { get; }
        public string ExpectedBehavior { get; }
        public Task20SampleDisposition ExpectedDisposition { get; }
        public AuditSeverity? ExpectedSeverity { get; }
    }

    internal static class Task20SampleCatalog
    {
        internal const string ManualReviewRuleId = "UNSUPPORTED_SCOPE";

        private static readonly IReadOnlyList<Task20Sample> Samples =
            BuildSamples();

        public static IReadOnlyList<Task20Sample> All => Samples;
        public static IReadOnlyList<string> SupportedRuleIds { get; } =
            new ReadOnlyCollection<string>(new[]
            {
                StringTableCompletenessRule.RuleId,
                EmptyTranslationRule.RuleId,
                LocalizedStringReferenceRule.RuleId,
                TmpFontCoverageRule.RuleId
            });

        public static int Count(Task20SampleCategory category)
        {
            return Samples.Count(sample => sample.Category == category);
        }

        private static IReadOnlyList<Task20Sample> BuildSamples()
        {
            var samples = new List<Task20Sample>
            {
                Error("E01", "Missing Simplified Chinese Locale Table", StringTableCompletenessRule.RuleId, "zh-Hans", "Collection has en and ja tables but no zh-Hans table.", "Report one Error for the missing zh-Hans table."),
                Error("E02", "Missing Japanese Locale Table", StringTableCompletenessRule.RuleId, "ja", "Collection has en and zh-Hans tables but no ja table.", "Report one Error for the missing ja table."),
                Error("E03", "Missing English Shared Key", StringTableCompletenessRule.RuleId, "en", "Shared key GREETING exists but the en table entry is absent.", "Report one Error for the missing en key."),
                Error("E04", "Missing Chinese Shared Key", StringTableCompletenessRule.RuleId, "zh-Hans", "Shared key START_GAME exists but the zh-Hans entry is absent.", "Report one Error for the missing zh-Hans key."),
                Error("E05", "Several Keys Missing From One Locale", StringTableCompletenessRule.RuleId, "ja", "Japanese table omits MENU_PLAY and MENU_QUIT from a three-key collection.", "Report one Error per missing Japanese key."),
                Error("E06", "Missing Table With Existing Shared Keys", StringTableCompletenessRule.RuleId, "zh-Hans", "The zh-Hans table is removed while shared data contains five keys.", "Report the missing table without duplicate missing-key reports."),
                Error("E07", "Nested Table Asset Missing A Locale", StringTableCompletenessRule.RuleId, "en", "A collection stored below a nested Tables folder lacks its en table.", "Report the missing en table using the collection asset path."),
                Error("E08", "Null English Translation", EmptyTranslationRule.RuleId, "en", "GREETING exists in en with a null localized value.", "Report one Error for the empty en translation."),
                Error("E09", "Empty Chinese Translation", EmptyTranslationRule.RuleId, "zh-Hans", "START_GAME exists in zh-Hans with an empty string.", "Report one Error for the empty zh-Hans translation."),
                Error("E10", "Whitespace English Translation", EmptyTranslationRule.RuleId, "en", "PAUSE_MENU contains only spaces, tabs, and line breaks.", "Report one Error for the whitespace-only translation."),
                Error("E11", "Whitespace Chinese Translation", EmptyTranslationRule.RuleId, "zh-Hans", "OPTIONS contains Chinese table whitespace only.", "Report one Error for the whitespace-only translation."),
                Error("E12", "Empty Values Across Two Locales", EmptyTranslationRule.RuleId, "en,zh-Hans", "GREETING is empty in en and zh-Hans but populated in ja.", "Report one Error for each empty locale entry."),
                Error("E13", "Several Empty Keys In One Table", EmptyTranslationRule.RuleId, "zh-Hans", "Three existing zh-Hans entries contain null, empty, and whitespace values.", "Report three independent Error issues with stable key ordering."),
                Error("E14", "Missing Entry Is Not An Empty Translation", EmptyTranslationRule.RuleId, "zh-Hans", "Shared key is absent from zh-Hans rather than present with an empty value.", "Do not report it from EMPTY_TRANSLATION; completeness owns the issue."),
                Error("E15", "Scene Reference Uses Missing Table", LocalizedStringReferenceRule.RuleId, "en", "Scene LocalizeStringEvent points to the missing collection Missing Strings.", "Report one Error with the Scene object and property path."),
                Error("E16", "Prefab Reference Uses Missing Entry Name", LocalizedStringReferenceRule.RuleId, "en", "Prefab LocalizeStringEvent points to an unknown entry name.", "Report one Error with the Prefab object path."),
                Error("E17", "Prefab Reference Uses Missing Entry ID", LocalizedStringReferenceRule.RuleId, "en", "Prefab LocalizeStringEvent uses a valid table GUID and unknown entry id.", "Report one Error with the numeric key id."),
                Error("E18", "Configured Table Has Empty Entry Reference", LocalizedStringReferenceRule.RuleId, "en", "Scene reference configures a valid table but leaves its entry empty.", "Report one Error and preserve the serialized property path."),
                Error("E19", "Nested Scene Reference Uses Invalid Key", LocalizedStringReferenceRule.RuleId, "zh-Hans", "Nested Scene object references a valid collection and missing Chinese key.", "Report one Error with the complete nested object path."),
                Error("E20", "Nested Prefab Reference Uses Invalid GUID", LocalizedStringReferenceRule.RuleId, "zh-Hans", "Nested Prefab object uses an unresolved table GUID.", "Report one Error with the original GUID table reference."),
                Clean("C01", "Complete English And Chinese Tables", StringTableCompletenessRule.RuleId, "en,zh-Hans", "Every shared key has an existing table entry in en and zh-Hans.", "Produce no completeness issue."),
                Clean("C02", "Complete Three-Locale Collection", StringTableCompletenessRule.RuleId, "en,zh-Hans,ja", "All shared keys exist in en, zh-Hans, and ja tables.", "Produce no completeness issue."),
                Clean("C03", "Two Complete Collections", StringTableCompletenessRule.RuleId, "en,zh-Hans", "Two independent collections contain every key for both locales.", "Produce no completeness issue."),
                Clean("C04", "Punctuation Key Is Complete", StringTableCompletenessRule.RuleId, "en,zh-Hans", "Key MENU_SETTINGS_&_HELP exists in every configured table.", "Produce no completeness issue."),
                Clean("C05", "Numeric Key Is Complete", StringTableCompletenessRule.RuleId, "en,zh-Hans", "Key LEVEL_001 exists in every configured table.", "Produce no completeness issue."),
                Clean("C06", "Collection With No Shared Keys", StringTableCompletenessRule.RuleId, "en,zh-Hans", "Collection has all configured locale tables but no shared entries yet.", "Produce no completeness issue."),
                Clean("C07", "Nested Tables Folder Is Complete", StringTableCompletenessRule.RuleId, "en,zh-Hans", "A nested collection contains all keys in both locale tables.", "Produce no completeness issue."),
                Clean("C08", "Normal English Translation", EmptyTranslationRule.RuleId, "en", "GREETING has the value Hello.", "Produce no empty-translation issue."),
                Clean("C09", "Normal Chinese Translation", EmptyTranslationRule.RuleId, "zh-Hans", "GREETING has the value 你好.", "Produce no empty-translation issue."),
                Clean("C10", "Internal Spaces Are Valid Text", EmptyTranslationRule.RuleId, "en", "PLAYER_NAME has the value New Player with internal spaces.", "Produce no empty-translation issue."),
                Clean("C11", "Chinese Punctuation Is Valid Text", EmptyTranslationRule.RuleId, "zh-Hans", "CONFIRM has the value 确定？ with punctuation.", "Produce no empty-translation issue."),
                Clean("C12", "Multiline Translation Is Valid", EmptyTranslationRule.RuleId, "en", "TUTORIAL contains two non-empty lines separated by a newline.", "Produce no empty-translation issue."),
                Clean("C13", "Numeric Text Is Valid", EmptyTranslationRule.RuleId, "en", "SCORE_LABEL has the value 0.", "Produce no empty-translation issue."),
                Clean("C14", "Emoji Text Is Valid", EmptyTranslationRule.RuleId, "zh-Hans", "ACHIEVEMENT has non-empty Chinese text followed by an emoji.", "Produce no empty-translation issue."),
                Clean("C15", "Valid Named Scene Reference", LocalizedStringReferenceRule.RuleId, "en", "Scene LocalizeStringEvent points to an existing collection and key by name.", "Produce no invalid-reference issue."),
                Clean("C16", "Valid GUID And Entry ID Reference", LocalizedStringReferenceRule.RuleId, "en", "Prefab LocalizeStringEvent points to the shared data GUID and existing id.", "Produce no invalid-reference issue."),
                Clean("C17", "Valid Chinese Nested Reference", LocalizedStringReferenceRule.RuleId, "zh-Hans", "Nested Scene object points to an existing Chinese key.", "Produce no invalid-reference issue."),
                Clean("C18", "Empty Reference Is Ignored", LocalizedStringReferenceRule.RuleId, "en", "LocalizedString component has no table or entry configured.", "Produce no invalid-reference issue."),
                Clean("C19", "Empty Table Reference Is Ignored", LocalizedStringReferenceRule.RuleId, "zh-Hans", "Entry text exists in serialized data but the table reference is empty.", "Produce no invalid-reference issue."),
                Clean("C20", "Valid Nested Prefab Reference", LocalizedStringReferenceRule.RuleId, "zh-Hans", "Deeply nested Prefab object points to an existing collection and key.", "Produce no invalid-reference issue."),
                Font("F01", "ASCII Covered By Root Font", "en", "Translation uses A-Z and ASCII punctuation; root font contains all code points.", "Produce no TMP font coverage issue.", Task20SampleDisposition.NoIssue, null),
                Font("F02", "Chinese Character Missing From Root Font", "zh-Hans", "Translation contains 你 but the root font contains only ASCII characters.", "Report one Error containing U+4F60.", Task20SampleDisposition.Issue, AuditSeverity.Error),
                Font("F03", "Chinese Character Covered By Fallback", "zh-Hans", "Root font covers ASCII and fallback font covers 你.", "Produce no TMP font coverage issue.", Task20SampleDisposition.NoIssue, null),
                Font("F04", "Emoji Missing From Font Chain", "en", "Translation contains U+1F600 but root and fallback fonts omit it.", "Report one Error containing U+1F600.", Task20SampleDisposition.Issue, AuditSeverity.Error),
                Font("F05", "Chinese Punctuation Covered", "zh-Hans", "Root or fallback font contains the Chinese question mark U+FF1F.", "Produce no TMP font coverage issue.", Task20SampleDisposition.NoIssue, null),
                Font("F06", "Mixed English Chinese And Emoji Coverage", "zh-Hans", "Translation contains Latin, Chinese, and emoji with only partial font coverage.", "Report one Error with all missing code points grouped by locale.", Task20SampleDisposition.Issue, AuditSeverity.Error),
                Font("F07", "Locale Has No Font Mapping", "zh-Hans", "The locale has translated characters but AuditRequest contains no font path.", "Report NotVerified rather than claiming coverage.", Task20SampleDisposition.Issue, AuditSeverity.NotVerified),
                Font("F08", "Configured Font Asset Cannot Load", "en", "Locale mapping points to a missing TMP Font Asset path.", "Report NotVerified with the configured path.", Task20SampleDisposition.Issue, AuditSeverity.NotVerified),
                Font("F09", "Fallback Cycle", "en", "Root font and fallback font reference each other.", "Report a Warning for the finite-search cycle.", Task20SampleDisposition.Issue, AuditSeverity.Warning),
                Font("F10", "Multilingual Fallback Chain Covers All Characters", "zh-Hans", "Root font covers Latin and fallback chain covers Chinese and punctuation.", "Produce no TMP font coverage issue.", Task20SampleDisposition.NoIssue, null)
+            };
+
+            samples.AddRange(new[]
+            {
+                Boundary("B01", "Runtime Generated Text", "en", "Text is assembled at runtime and never serialized in a Scene or Prefab.", "Manual review required because static extraction cannot verify it."),
+                Boundary("B02", "Custom Smart Formatter", "en", "A project-specific Smart Formatter generates text outside the standard table value.", "Manual review required because formatter semantics are outside the MVP."),
+                Boundary("B03", "Third-Party Localization Framework", "zh-Hans", "Text is owned by a third-party localization package rather than Unity Localization.", "Manual review required because the supported framework is different."),
+                Boundary("B04", "Runtime Created String Table", "en", "The String Table collection is created only during application startup.", "Manual review required because the Editor cannot inspect runtime-only assets."),
+                Boundary("B05", "Encrypted Translation Payload", "zh-Hans", "Translations are decrypted into memory and are not present as inspectable assets.", "Manual review required because the source payload is unavailable."),
+                Boundary("B06", "Remote Translation Service", "en", "Localized text arrives from a remote service at runtime.", "Manual review required because network responses are outside static scanning."),
+                Boundary("B07", "Reflection Created LocalizedString", "zh-Hans", "A LocalizedString component is created and configured through reflection at runtime.", "Manual review required because no serialized reference exists."),
+                Boundary("B08", "Runtime Only TMP Font", "zh-Hans", "The TMP Font Asset is downloaded and assigned only after entering Play Mode.", "Manual review required because the Editor mapping cannot resolve it."),
+                Boundary("B09", "Custom Shader Text Rendering", "en", "Characters are rendered by a custom shader without a TMP Font Asset.", "Manual review required because TMP coverage does not apply."),
+                Boundary("B10", "Unknown Serialized Component", "zh-Hans", "A missing script owns a localized field that Unity cannot deserialize.", "Manual review required because the component contract is unavailable.")
+            });
+
+            return new ReadOnlyCollection<Task20Sample>(samples);
+        }
+
+        private static Task20Sample Error(
+            string id,
+            string title,
+            string ruleId,
+            string localeCode,
+            string input,
+            string expectedBehavior)
+        {
+            return new Task20Sample(
+                id,
+                Task20SampleCategory.ExpectedError,
+                title,
+                ruleId,
+                localeCode,
+                input,
+                expectedBehavior,
+                Task20SampleDisposition.Issue,
+                AuditSeverity.Error);
+        }
+
+        private static Task20Sample Clean(
+            string id,
+            string title,
+            string ruleId,
+            string localeCode,
+            string input,
+            string expectedBehavior)
+        {
+            return new Task20Sample(
+                id,
+                Task20SampleCategory.ExpectedClean,
+                title,
+                ruleId,
+                localeCode,
+                input,
+                expectedBehavior,
+                Task20SampleDisposition.NoIssue,
+                null);
+        }
+
+        private static Task20Sample Font(
+            string id,
+            string title,
+            string localeCode,
+            string input,
+            string expectedBehavior,
+            Task20SampleDisposition disposition,
+            AuditSeverity? severity)
+        {
+            return new Task20Sample(
+                id,
+                Task20SampleCategory.FontCoverage,
+                title,
+                TmpFontCoverageRule.RuleId,
+                localeCode,
+                input,
+                expectedBehavior,
+                disposition,
+                severity);
+        }
+
+        private static Task20Sample Boundary(
+            string id,
+            string title,
+            string localeCode,
+            string input,
+            string expectedBehavior)
+        {
+            return new Task20Sample(
+                id,
+                Task20SampleCategory.Boundary,
+                title,
+                ManualReviewRuleId,
+                localeCode,
+                input,
+                expectedBehavior,
+                Task20SampleDisposition.ManualReview,
+                null);
+        }
+    }
+}
