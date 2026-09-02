namespace Yxwm.LocalizationAuditor
{
    // 统一保存报告展示和 Unity 资源定位所需的上下文信息。
    public sealed class AuditIssueLocation
    {
        // 没有关联资源时使用共享的空定位对象，避免到处判空。
        public static AuditIssueLocation Empty { get; } = new AuditIssueLocation();

        public string LocaleCode { get; }
        public string TableName { get; }
        public string Key { get; }
        public string AssetPath { get; }
        public string ObjectPath { get; }
        public string ComponentType { get; }
        public string PropertyPath { get; }
        public string FontAssetPath { get; }

        public AuditIssueLocation(
            string localeCode = null,
            string tableName = null,
            string key = null,
            string assetPath = null,
            string objectPath = null,
            string fontAssetPath = null,
            string componentType = null,
            string propertyPath = null)
        {
            LocaleCode = localeCode ?? string.Empty;
            TableName = tableName ?? string.Empty;
            Key = key ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            ComponentType = componentType ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
            FontAssetPath = fontAssetPath ?? string.Empty;
        }
    }
}
