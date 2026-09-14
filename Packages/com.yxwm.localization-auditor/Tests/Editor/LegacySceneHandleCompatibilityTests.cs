using NUnit.Framework;
namespace Yxwm.LocalizationAuditor.Tests
{
    // Unity 6.0 的 Scene.handle 是 int；较新版本自带同名实例方法并优先使用。
    // 保留全部32位，避免负句柄与正句柄发生映射冲突；仅供测试现场归属跟踪。
    internal static class LegacySceneHandleCompatibility
    {
        internal static ulong GetRawData(this int handle) => unchecked((uint)handle);
    }

    public sealed class LegacySceneHandleCompatibilityTests
    {
        [Test]
        public void PreservesDistinctPositiveNegativeAndZeroHandles()
        {
            Assert.That(0.GetRawData(), Is.EqualTo(0UL));
            Assert.That(1.GetRawData(), Is.EqualTo(1UL));
            Assert.That((-1).GetRawData(), Is.EqualTo(4294967295UL));
            Assert.That(int.MinValue.GetRawData(), Is.EqualTo(2147483648UL));
        }
    }
}
