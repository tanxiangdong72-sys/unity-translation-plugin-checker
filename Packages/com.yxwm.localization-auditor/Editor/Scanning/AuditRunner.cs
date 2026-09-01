using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditRunner
    {
        public IReadOnlyList<IAuditRule> Rules { get; }

        public AuditRunner(IEnumerable<IAuditRule> rules)
        {
            var ruleList = rules == null
                ? new List<IAuditRule>()
                : new List<IAuditRule>(rules);

            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in ruleList)
            {
                if (rule == null)
                {
                    throw new ArgumentException(
                        "Rules cannot contain null values.",
                        nameof(rules));
                }

                if (string.IsNullOrWhiteSpace(rule.Id))
                {
                    throw new ArgumentException(
                        "Every rule must have a non-empty id.",
                        nameof(rules));
                }

                if (!ruleIds.Add(rule.Id))
                {
                    throw new ArgumentException(
                        "Rule ids must be unique.",
                        nameof(rules));
                }
            }

            ruleList.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            Rules = new ReadOnlyCollection<IAuditRule>(ruleList);
        }

        public AuditReport Run(
            AuditRequest request,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<AuditProgress> progress = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var startedAtUtc = DateTimeOffset.UtcNow;
            var issues = new List<AuditIssue>();
            var diagnostics = new List<AuditDiagnostic>();
            var context = new AuditContext(request);
            var completedRuleCount = 0;
            var status = AuditRunStatus.Completed;

            if (cancellationToken.IsCancellationRequested)
            {
                status = AuditRunStatus.Cancelled;
            }
            else
            {
                for (var index = 0; index < Rules.Count; index++)
                {
                    var rule = Rules[index];
                    if (cancellationToken.IsCancellationRequested)
                    {
                        status = AuditRunStatus.Cancelled;
                        break;
                    }

                    ReportProgress(
                        progress,
                        diagnostics,
                        completedRuleCount,
                        Rules.Count,
                        rule.Id);

                    try
                    {
                        var ruleIssues = rule.Evaluate(context, cancellationToken);
                        if (ruleIssues != null)
                        {
                            foreach (var issue in ruleIssues)
                            {
                                if (issue == null)
                                {
                                    diagnostics.Add(new AuditDiagnostic(
                                        "RULE_NULL_ISSUE",
                                        "A rule returned a null issue and it was skipped.",
                                        exceptionType: string.Empty));
                                    continue;
                                }

                                issues.Add(issue);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            status = AuditRunStatus.Cancelled;
                            break;
                        }

                        AddRuleExceptionDiagnostic(
                            diagnostics,
                            rule,
                            new OperationCanceledException(
                                "The rule raised cancellation without a requested cancellation."));
                    }
                    catch (Exception exception)
                    {
                        AddRuleExceptionDiagnostic(diagnostics, rule, exception);
                    }

                    completedRuleCount++;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        status = AuditRunStatus.Cancelled;
                        break;
                    }
                }
            }

            ReportProgress(
                progress,
                diagnostics,
                completedRuleCount,
                Rules.Count,
                string.Empty);

            return new AuditReport(
                startedAtUtc,
                DateTimeOffset.UtcNow,
                status,
                context.AssetPaths.Count,
                issues,
                diagnostics);
        }

        private static void ReportProgress(
            Action<AuditProgress> progress,
            ICollection<AuditDiagnostic> diagnostics,
            int completedRuleCount,
            int totalRuleCount,
            string currentRuleId)
        {
            if (progress == null)
            {
                return;
            }

            try
            {
                progress(new AuditProgress(
                    completedRuleCount,
                    totalRuleCount,
                    currentRuleId));
            }
            catch (Exception exception)
            {
                diagnostics.Add(new AuditDiagnostic(
                    "PROGRESS_CALLBACK_EXCEPTION",
                    "The progress callback failed and scanning continued.",
                    exceptionType: exception.GetType().FullName));
            }
        }

        private static void AddRuleExceptionDiagnostic(
            ICollection<AuditDiagnostic> diagnostics,
            IAuditRule rule,
            Exception exception)
        {
            diagnostics.Add(new AuditDiagnostic(
                "RULE_EXCEPTION",
                "Rule '" + rule.Id + "' failed: " + exception.Message,
                exceptionType: exception.GetType().FullName));
        }
    }
}
