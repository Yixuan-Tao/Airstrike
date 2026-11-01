# 运行验收记录

执行日期：2026-11-01。环境：Windows、Unity 6000.3.27f2、URP 17.3.0、Windows x64 / Mono。

- `standalone.json`：最终独立程序的原始游戏验收结果；23 条检查通过，运行错误为 0。
- `acceptance.json`：构建结果、进程退出码、程序集哈希及帧推进证据。发布前移除了本机绝对路径。

记录来自开发电脑的实际后台执行，不代表全波次覆盖、人工键鼠验收、声音验收或长期性能验证。射击阶段固定目标位置以保证可重复；正常飞行和投弹在独立步骤检查。

独立程序使用 `-batchmode -nographics -noaudio`；输入通过游戏内部控制通道注入，没有操作其他软件窗口。复现入口为 `AgentScripts/VerifyStandalone.ps1`，需要先生成 Windows 构建。

预览图为安排过敌机位置与坑洞的运行场景，单独保存在 `Docs/Images/skybreach.png`，不作为运行推进的唯一证据。
