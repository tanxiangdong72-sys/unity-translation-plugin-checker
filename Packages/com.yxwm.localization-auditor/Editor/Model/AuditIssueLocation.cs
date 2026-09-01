namespace Yxwm.LocalizationAuditor
{
    public sealed class AuditIssueLocation
    {
        public static AuditIssueLocation Empty { get; } = new AuditIssueLocation();

        public string LocaleCode { get; }
        public string TableName { get; }
        public string Key { get; }
        public string AssetPath { get; }
        public string ObjectPath { get; }
        public string FontAssetPath { get; }

        public AuditIssueLocation(
            string localeCode = null,
            string tableName = null,
            string key = null,
            string assetPath = null,
            string objectPath = null,
            string fontAssetPath = null)
        {
            LocaleCode = localeCode ?? string.Empty;
            TableName = tableName ?? string.Empty;
            Key = key ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            FontAssetPath = fontAssetPath ?? string.Empty;
        }
    }
}
