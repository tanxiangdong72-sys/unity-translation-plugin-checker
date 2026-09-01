using System;
using System.Collections.Generic;

namespace Yxwm.LocalizationAuditor
{
    // 上下文只暴露请求的只读视图，规则不能通过它修改扫描配置。
    public sealed class AuditContext
    {
        public AuditRequest Request { get; }
        public IReadOnlyList<string> AssetPaths => Request.AssetPaths;
        public IReadOnlyDictionary<string, string> LocaleFontAssetPaths =>
            Request.LocaleFontAssetPaths;

        public AuditContext(AuditRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }
    }
}
