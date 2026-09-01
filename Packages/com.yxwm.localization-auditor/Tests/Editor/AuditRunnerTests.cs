using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Yxwm.LocalizationAuditor.Tests
{
    // 使用可控的测试规则验证 Runner 的顺序、取消和异常隔离契约。
    public sealed class AuditRunnerTests
    {
        [Test]
        public void RunnerExecutesRulesInOrdinalOrder()
        {
            var executionOrder = new List<string>();
            var runner = new AuditRunner(new IAuditRule[]
            {
                new RecordingRule("RULE_B", executionOrder),
                new RecordingRule("RULE_A", executionOrder)
            });

            var report = runner.Run(new AuditRequest(new[] { "Assets/A.prefab" }));

            Assert.That(executionOrder, Is.EqualTo(new[] { "RULE_A", "RULE_B" }));
            Assert.That(report.Status, Is.EqualTo(AuditRunStatus.Completed));
            Assert.That(report.ScannedAssetCount, Is.EqualTo(1));
            Assert.That(report.Issues, Has.Count.EqualTo(2));
            Assert.That(report.Diagnostics, Is.Empty);
        }

        [Test]
        public void RunnerContinuesAfterRuleExceptionAndRecordsDiagnostic()
        {
            var executionOrder = new List<string>();
            var runner = new AuditRunner(new IAuditRule[]
            {
                new RecordingRule("02_AFTER", executionOrder),
                new ThrowingRule("01_BROKEN", executionOrder)
            });

            var report = runner.Run(new AuditRequest());

            Assert.That(executionOrder, Is.EqualTo(new[] { "01_BROKEN", "02_AFTER" }));
            Assert.That(report.Status, Is.EqualTo(AuditRunStatus.Completed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].RuleId, Is.EqualTo("02_AFTER"));
            Assert.That(report.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(report.Diagnostics[0].Code, Is.EqualTo("RULE_EXCEPTION"));
            Assert.That(report.Diagnostics[0].ExceptionType, Does.Contain(nameof(InvalidOperationException)));
        }

        [Test]
        public void RunnerStopsBeforeNextRuleWhenCancellationIsRequested()
        {
            var executionOrder = new List<string>();
            using (var cancellation = new CancellationTokenSource())
            {
                var runner = new AuditRunner(new IAuditRule[]
                {
                    new CancellingRule("01_CANCEL", executionOrder, cancellation),
                    new RecordingRule("02_AFTER", executionOrder)
                });

                var report = runner.Run(new AuditRequest(), cancellation.Token);

                Assert.That(executionOrder, Is.EqualTo(new[] { "01_CANCEL" }));
                Assert.That(report.Status, Is.EqualTo(AuditRunStatus.Cancelled));
                Assert.That(report.Issues, Has.Count.EqualTo(1));
                Assert.That(report.Diagnostics, Is.Empty);
            }
        }

        [Test]
        public void RunnerReportsStableRuleProgress()
        {
            var progress = new List<AuditProgress>();
            var runner = new AuditRunner(new IAuditRule[]
            {
                new RecordingRule("RULE_B", new List<string>()),
                new RecordingRule("RULE_A", new List<string>())
            });

            runner.Run(
                new AuditRequest(),
                progress: progress.Add);

            Assert.That(progress, Has.Count.EqualTo(3));
            Assert.That(progress[0].CurrentRuleId, Is.EqualTo("RULE_A"));
            Assert.That(progress[0].CompletedRuleCount, Is.EqualTo(0));
            Assert.That(progress[1].CurrentRuleId, Is.EqualTo("RULE_B"));
            Assert.That(progress[1].CompletedRuleCount, Is.EqualTo(1));
            Assert.That(progress[2].CurrentRuleId, Is.Empty);
            Assert.That(progress[2].CompletedRuleCount, Is.EqualTo(2));
            Assert.That(progress[2].IsComplete, Is.True);
        }

        [Test]
        public void RunnerRejectsDuplicateRuleIds()
        {
            Assert.Throws<ArgumentException>(() =>
                new AuditRunner(new IAuditRule[]
                {
                    new RecordingRule("DUPLICATE", new List<string>()),
                    new RecordingRule("DUPLICATE", new List<string>())
                }));
        }

        private sealed class RecordingRule : IAuditRule
        {
            private readonly List<string> _executionOrder;

            public RecordingRule(string id, List<string> executionOrder)
            {
                Id = id;
                _executionOrder = executionOrder;
            }

            public string Id { get; }

            public IEnumerable<AuditIssue> Evaluate(
                AuditContext context,
                CancellationToken cancellationToken)
            {
                _executionOrder.Add(Id);
                return new[]
                {
                    new AuditIssue(Id, AuditSeverity.Warning, Id + " issue.")
                };
            }
        }

        private sealed class ThrowingRule : IAuditRule
        {
            private readonly List<string> _executionOrder;

            public ThrowingRule(string id, List<string> executionOrder)
            {
                Id = id;
                _executionOrder = executionOrder;
            }

            public string Id { get; }

            public IEnumerable<AuditIssue> Evaluate(
                AuditContext context,
                CancellationToken cancellationToken)
            {
                _executionOrder.Add(Id);
                throw new InvalidOperationException("Expected test exception.");
            }
        }

        private sealed class CancellingRule : IAuditRule
        {
            private readonly List<string> _executionOrder;
            private readonly CancellationTokenSource _cancellation;

            public CancellingRule(
                string id,
                List<string> executionOrder,
                CancellationTokenSource cancellation)
            {
                Id = id;
                _executionOrder = executionOrder;
                _cancellation = cancellation;
            }

            public string Id { get; }

            public IEnumerable<AuditIssue> Evaluate(
                AuditContext context,
                CancellationToken cancellationToken)
            {
                _executionOrder.Add(Id);
                _cancellation.Cancel();
                return new[]
                {
                    new AuditIssue(Id, AuditSeverity.Warning, Id + " issue.")
                };
            }
        }
    }
}
