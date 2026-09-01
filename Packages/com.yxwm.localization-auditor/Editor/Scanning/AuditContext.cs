using System;
using System.Collections.Generic;

namespace Yxwm.LocalizationAuditor
{
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
