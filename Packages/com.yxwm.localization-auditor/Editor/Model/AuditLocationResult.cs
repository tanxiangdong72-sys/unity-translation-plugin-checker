using UnityEngine;

namespace Yxwm.LocalizationAuditor
{
    internal enum AuditLocationResultStatus
    {
        Success = 0,
        InvalidLocation = 1,
        AssetNotFound = 2,
        SceneNotLoaded = 3,
        ObjectNotFound = 4,
        ComponentNotFound = 5,
        PropertyNotFound = 6,
        ResolutionFailed = 7
    }

    // 定位结果不可变，窗口可以安全显示失败原因而不依赖异常状态。
    internal sealed class AuditLocationResult
    {
        private AuditLocationResult(
            AuditLocationResultStatus status,
            string message,
            Object target,
            string assetPath,
            string objectPath)
        {
            Status = status;
            Message = message ?? string.Empty;
            Target = target;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
        }

        public AuditLocationResultStatus Status { get; }
        public bool Succeeded => Status == AuditLocationResultStatus.Success;
        public string Message { get; }
        public Object Target { get; }
        public string AssetPath { get; }
        public string ObjectPath { get; }

        public static AuditLocationResult Success(
            Object target,
            string assetPath,
            string objectPath)
        {
            return new AuditLocationResult(
                AuditLocationResultStatus.Success,
                "Located '" + objectPath + "'.",
                target,
                assetPath,
                objectPath);
        }

        public static AuditLocationResult Failure(
            AuditLocationResultStatus status,
            string message,
            string assetPath = null,
            string objectPath = null)
        {
            return new AuditLocationResult(
                status,
                message,
                null,
                assetPath,
                objectPath);
        }
    }
}
