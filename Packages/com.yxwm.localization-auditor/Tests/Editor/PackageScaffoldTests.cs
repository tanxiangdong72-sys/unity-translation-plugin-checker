using NUnit.Framework;
using UnityEditor.PackageManager;
using Yxwm.LocalizationAuditor;

namespace Yxwm.LocalizationAuditor.Tests
{
    public sealed class PackageScaffoldTests
    {
        [Test]
        public void EmbeddedPackageMetadataIsDiscoverable()
        {
            var package = PackageInfo.FindForAssetPath(
                "Packages/com.yxwm.localization-auditor/package.json");

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.yxwm.localization-auditor"));
            Assert.That(package.version, Is.EqualTo("0.1.0"));
        }

        [Test]
        public void EditorAssemblyExposesStablePackageId()
        {
            Assert.That(
                PackageAssemblyMarker.PackageId,
                Is.EqualTo("com.yxwm.localization-auditor"));
        }
    }
}
