using System;

namespace Yxwm.LocalizationAuditor
{
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
