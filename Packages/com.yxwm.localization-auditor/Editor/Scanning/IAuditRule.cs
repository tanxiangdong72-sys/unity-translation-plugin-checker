using System.Collections.Generic;
using System.Threading;

namespace Yxwm.LocalizationAuditor
{
    public interface IAuditRule
    {
        string Id { get; }

        IEnumerable<AuditIssue> Evaluate(
            AuditContext context,
            CancellationToken cancellationToken);
    }
}
