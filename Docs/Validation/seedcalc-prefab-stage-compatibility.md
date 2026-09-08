# SeedCalc：旧版 Prefab 定位兼容修复
日期：2026-09-08

## 原因与修改
SeedCalc 使用 Unity 2020.3.30f1c1。原定位代码调用此版本未公开的 PrefabStageUtility.OpenPrefab，并遗漏 StageUtility 的稳定命名空间，导致 CS0117 / CS0103。
修复以远程 main d7917cf9f961bbefd860412a61bb55db1cd9aac2 为基础：
- StageUtility 使用 UnityEditor.SceneManagement；旧版 PrefabStage 使用 Experimental 命名空间。
- Unity 2021.2 及以上仍调用公开 OpenPrefab；较旧版本使用 AssetDatabase.OpenAsset，并核对实际打开的 Stage 路径。
- 兼容旧版 Stage 路径属性；为旧 C# 编译器显式转换 GameObject/Component 到 UnityEngine.Object。
- 不返回已经卸载的 LoadPrefabContents 临时对象；保留成功定位、Stage 复用和失败恢复行为。

参考：[Unity 2020.3 官方源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2020.3/Editor/Mono/SceneManagement/StageManager/PrefabStage/PrefabStageUtility.cs)。

## 实际验证
### SeedCalc / Unity 2020.3.30f1c1
在原来的验证副本内修改嵌入包，Unity 重新导入后自动退出 Safe Mode。
一次性 Editor 回归入口执行六项检查：
1. 子对象、组件、属性解析与 Selection。
2. 返回目标仍存活且属于实际打开的 Prefab Stage。
3. 重复定位复用同一个 Stage。
4. 失败后恢复主 Stage。
5. 失败后恢复原 Prefab Stage。
6. Prefab 文件字节未改变。
六项均通过。临时入口、对应 meta 和测试资源已移出或清理。

通过真实窗口点击 Refresh / Run Audit：4 个目标（1 Scene、3 Prefab），Completed，2 个 TMP_FONT_COVERAGE NotVerified，0 个 Error。
Console 显示 0 个错误，保留了 3 个历史警告及 API Updater 旧日志，未清空控制台。
临时 PublicProjectScanRunner 的混合换行已统一；此脚本不属于远程正式包。

### Unity 6000.5.10f1
在本地验证宿主执行完整 EditMode 测试：
- 总计 170，169 Passed，0 Failed，1 Skipped。
- 新增 LocatorCompatibilityRegressionTests 的回归测试通过，与旧版 Editor 入口检查相同的六项行为。
- 无图形 batchmode 的 GUI smoke 跳过，不当作 GUI 验证通过。
- 本地证据：SeedCalc 验证副本 PublicAuditResults/ 下的 locator-compatibility-regression.txt、unity6-locator-regression-full.xml 和 .log。

## 范围限制
这是定位代码的定向修复，不是全部旧版 Unity 的正式支持发布。
SeedCalc 内嵌包已有其他兼容调整；本次没有把未审查的旧版工作分支整体合并，也没有降低 package.json 的最低版本。
完整 Unity 6 测试针对本地宿主，而非重新执行远程 Git 安装。
本次没有试玩 SeedCalc、没有完整测量扫描只读性/性能，也没有验证字体覆盖。
