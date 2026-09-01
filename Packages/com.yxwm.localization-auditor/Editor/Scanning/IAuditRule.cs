using System.Collections.Generic;
using System.Threading;

namespace Yxwm.LocalizationAuditor
{
    // 每条规则只负责产生自己的问题，执行顺序和结果汇总由 AuditRunner 统一管理。
    public interface IAuditRule
    {
        string Id { get; }

        IEnumerable<AuditIssue> Evaluate(
            AuditContext context,
            CancellationToken cancellationToken);
    }
}
