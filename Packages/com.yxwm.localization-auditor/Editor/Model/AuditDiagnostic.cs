using System;

namespace Yxwm.LocalizationAuditor
{
    // 诊断记录扫描流程本身的问题，例如规则异常，而不是用户项目问题。
    public sealed class AuditDiagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public string AssetPath { get; }
        public string ExceptionType { get; }

        public AuditDiagnostic(
            string code,
            string message,
            string assetPath = null,
            string exceptionType = null)
        {
            // 诊断必须有稳定代码和可读消息，便于 UI 和日志分类。
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Diagnostic code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Diagnostic message is required.", nameof(message));
            }

            Code = code;
            Message = message;
            AssetPath = assetPath ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
        }
    }
}
