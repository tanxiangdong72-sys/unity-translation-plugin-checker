namespace Yxwm.LocalizationAuditor
{
    // 保留一个稳定的内部入口，避免 Editor 程序集在只有配置文件时变成空程序集。
    internal static class PackageAssemblyMarker
    {
        public const string PackageId = "com.yxwm.localization-auditor";
    }
}
