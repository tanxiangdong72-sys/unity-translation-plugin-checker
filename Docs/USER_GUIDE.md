# 使用指南
[返回首页](../README.md)

## 1. 安装和打开
先备份项目，再使用 [首页的 Git 安装地址](../README.md#快速开始)。
安装后等待编译完成，打开 Window（窗口）→ Localization Auditor。包管理器的 Manage 菜单不是插件操作入口。

不要把整个仓库复制进 Assets：实际插件位于 Packages/com.yxwm.localization-auditor。

## 2. 运行一次检查
1. 保存你的工作，确保项目没有编译错误。
2. 点击 Refresh / 刷新目标：从 Assets 发现 Scene 和 Prefab。
3. 确认目标数量不为 0，点击 Run Audit / 运行审计。
4. 先看运行状态，再看问题与诊断，不要只看错误数量。
5. 通过筛选查看问题详情、资源路径、规则。
6. 手动修复，再次运行；如果资源集合有变化，先刷新目标。

| 窗口文字 | 含义 |
| --- | --- |
| Completed | 本次检查完成，不表示没有问题 |
| Failed / Cancelled | 失败 / 取消，不能当作完整通过 |
| Error / Warning | 错误 / 警告，需要结合详情处理 |
| NotVerified | 资料不足或能力边界，尚未验证 |
| Diagnostics | 执行或读取过程的诊断，应单独检查 |
| Locate | 对支持的对象问题进行定位 |

## 3. 定位问题
Scene/Prefab 问题需有有效资源路径和对象路径。场景对象需要其场景已加载；预制体定位可能进入 Prefab 编辑环境。先保存未提交的编辑。

翻译表问题的定位按钮不可用时，按报告中的资源路径在 Project 中找到翻译表，手动检查对应词条。这不是自动修复工具。

## 4. 常见问题
### 包已安装，但菜单没有出现
先查看 Console 的第一条红色错误。项目未完成编译或处于 Safe Mode 时，不能仅凭包已安装就认为插件可以使用。

### 出现 CS0246 或 missing meta / immutable folder
这类错误曾由远程包缺文件、缺 .meta 或 GUID 冲突引起。记录第一条完整错误和实际安装提交，再与首页固定基线比较。不要直接修改 Library/PackageCache 中的文件。

可以在 Packages/packages-lock.json 中检查该包的 hash；PackageCache 目录后缀不能直接当成 Git 提交号。更新前备份 manifest 和 lock。不要先删除整个 Library 来掩盖根因。

### 刷新后没有目标
检查 Assets 下是否有保存的 .unity 或 .prefab。当前窗口没有针对“只有翻译表”的独立启动入口，这是已知限制。

### 为什么扫描 0 个问题？
可能确实没问题，也可能没有相关本地化数据。请用 [预置错误验证](VALIDATION_GUIDE.md) 证明检查能发现问题。

### 为什么字体未验证？
当前窗口没有完整的语言到字体映射配置。未验证不能用于证明字体一定能显示全部文字。

### 窗口内容显示不全
先拉宽窗口查看完整报告；提交反馈时附截图和显示缩放比例。
