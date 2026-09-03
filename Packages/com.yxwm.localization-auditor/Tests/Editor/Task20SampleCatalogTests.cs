using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 验证任务 20 的 60 个样本具备完整分类、规则归属和可追溯描述。
    public sealed class Task20SampleCatalogTests
    {
        [Test]
        public void CatalogHasRequiredBalancedSampleCounts()
        {
            Assert.That(Task20SampleCatalog.All, Has.Count.EqualTo(60));
            Assert.That(
                Task20SampleCatalog.Count(Task20SampleCategory.ExpectedError),
                Is.EqualTo(20));
            Assert.That(
                Task20SampleCatalog.Count(Task20SampleCategory.ExpectedClean),
                Is.EqualTo(20));
            Assert.That(
                Task20SampleCatalog.Count(Task20SampleCategory.FontCoverage),
                Is.EqualTo(10));
            Assert.That(
                Task20SampleCatalog.Count(Task20SampleCategory.Boundary),
                Is.EqualTo(10));
        }

        [Test]
        public void CatalogSampleIdsAndDescriptionsAreUniqueAndComplete()
        {
            Assert.That(
                Task20SampleCatalog.All.Select(sample => sample.Id),
                Is.Unique);
            Assert.That(
                Task20SampleCatalog.All.All(sample =>
                    !string.IsNullOrWhiteSpace(sample.Id) &&
                    !string.IsNullOrWhiteSpace(sample.Title) &&
                    !string.IsNullOrWhiteSpace(sample.Input) &&
                    !string.IsNullOrWhiteSpace(sample.ExpectedBehavior) &&
                    !string.IsNullOrWhiteSpace(sample.RuleId)),
                Is.True);
        }

        [Test]
        public void CatalogCoversRequiredLocalesAndRuleFamilies()
        {
            var locales = Task20SampleCatalog.All
                .Where(sample => !string.IsNullOrEmpty(sample.LocaleCode))
                .Select(sample => sample.LocaleCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.That(locales, Does.Contain("en"));
            Assert.That(locales, Does.Contain("zh-Hans"));
            Assert.That(
                Task20SampleCatalog.All.Select(sample => sample.RuleId),
                Does.Contain(StringTableCompletenessRule.RuleId));
            Assert.That(
                Task20SampleCatalog.All.Select(sample => sample.RuleId),
                Does.Contain(EmptyTranslationRule.RuleId));
            Assert.That(
                Task20SampleCatalog.All.Select(sample => sample.RuleId),
                Does.Contain(LocalizedStringReferenceRule.RuleId));
            Assert.That(
                Task20SampleCatalog.All.Select(sample => sample.RuleId),
                Does.Contain(TmpFontCoverageRule.RuleId));
        }

        [Test]
        public void BoundarySamplesDeclareManualReviewInsteadOfPretendingSupport()
        {
            var boundarySamples = Task20SampleCatalog.All
                .Where(sample => sample.Category == Task20SampleCategory.Boundary)
                .ToArray();

            Assert.That(boundarySamples, Has.Length.EqualTo(10));
            Assert.That(
                boundarySamples.All(sample =>
                    sample.ExpectedDisposition == Task20SampleDisposition.ManualReview &&
                    sample.RuleId == Task20SampleCatalog.ManualReviewRuleId),
                Is.True);
        }

        public static IEnumerable<TestCaseData> CatalogSamples()
        {
            foreach (var sample in Task20SampleCatalog.All)
            {
                yield return new TestCaseData(sample)
                    .SetName("Sample_" + sample.Id + "_" + sample.Title.Replace(" ", "_"));
            }
        }

        [TestCaseSource(nameof(CatalogSamples))]
        public void EachCatalogSampleHasAnExecutableExpectation(Task20Sample sample)
        {
            Assert.That(sample.ExpectedBehavior, Is.Not.Empty);

            switch (sample.Category)
            {
                case Task20SampleCategory.ExpectedError:
                    Assert.That(sample.ExpectedDisposition, Is.EqualTo(
                        Task20SampleDisposition.Issue));
                    Assert.That(sample.ExpectedSeverity, Is.EqualTo(AuditSeverity.Error));
                    Assert.That(
                        Task20SampleCatalog.SupportedRuleIds,
                        Does.Contain(sample.RuleId));
                    break;
                case Task20SampleCategory.ExpectedClean:
                    Assert.That(sample.ExpectedDisposition, Is.EqualTo(
                        Task20SampleDisposition.NoIssue));
                    Assert.That(sample.ExpectedSeverity, Is.Null);
                    Assert.That(
                        Task20SampleCatalog.SupportedRuleIds,
                        Does.Contain(sample.RuleId));
                    break;
                case Task20SampleCategory.FontCoverage:
                    Assert.That(sample.RuleId, Is.EqualTo(TmpFontCoverageRule.RuleId));
                    Assert.That(sample.ExpectedDisposition, Is.Not.EqualTo(
                        Task20SampleDisposition.ManualReview));
                    break;
                case Task20SampleCategory.Boundary:
                    Assert.That(sample.ExpectedDisposition, Is.EqualTo(
                        Task20SampleDisposition.ManualReview));
                    Assert.That(sample.RuleId, Is.EqualTo(
                        Task20SampleCatalog.ManualReviewRuleId));
                    break;
                default:
                    Assert.Fail("Unknown sample category: " + sample.Category);
                    break;
            }
        }
    }
}
