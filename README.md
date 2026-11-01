# Airstrike / SKYBREACH v0.3.0

Unity 6.3 侧视 2.5D 空袭生存原型。控制地面士兵移动、跳跃和挖掘，用火箭击落敌机，通过地形掩护躲避轰炸。

![运行场景预览](Docs/Images/skybreach.png)

画面为自制占位简模。预览由真实运行场景离屏生成，调整过敌机与坑洞位置，不是人工实玩录像。

## v0.3 的新增内容

- 高速投弹机：速度更快，接近玩家时连续投下三枚炸弹。
- 火箭直升机：移动到玩家附近悬停，显示约 0.85 秒瞄准线后向预警时的位置发射火箭；火箭不会追踪玩家。
- 火箭位补给：增加一枚可同时在空中的火箭，上限三个。
- 穿透补给：火箭可贯穿第一架敌机，在第二个目标或土墙处爆炸。第一架不会被这次末端爆炸重复伤害，土墙仍能挡住穿透弹。
- 补给箱缓降，靠近后自动拾取；复活保留本局升级，重新开始清空。已满级的同类补给转为 150 分。

前六波按固定组合逐步引入机制，第七波起进入混合无尽波次：

| 波次 | 名称 | 敌人组成 | 清空奖励 |
|---|---|---|---|
| 1 | FIRST CONTACT | 3 架普通机 | — |
| 2 | FAST MOVERS | 2 架普通机、2 架高速机 | 火箭位 |
| 3 | ROCKET LOCK | 2 架普通机、1 架高速机、1 架直升机 | — |
| 4 | HARD TARGETS | 1 架装甲机、1 架普通机、2 架高速机、1 架直升机 | 穿透弹 |
| 5 | CROSS FIRE | 1 架普通机、3 架高速机、2 架直升机 | — |
| 6 | HOLD THE SECTOR | 1 架装甲机、1 架普通机、3 架高速机、2 架直升机 | 火箭位 |

装甲机与直升机需要多次命中。无尽波每波最多 12 架敌机。每逢偶数波提供一箱补给，未拾取补给 35 秒后消失，场上最多保留三箱；补给不会阻止进入下一波。

## 移动与掩体机制

- **掩体参与伤害判定**：爆炸先考虑头部、躯干、腿部方向上的土层遮挡，再破坏地形。足够厚的土墙可以挡住当前爆炸；下一次轰炸可能穿过已经炸开的缺口。露出身体的一部分仍可能受伤。
- **跳跃容错**：离地后 0.12 秒内仍可使用地面起跳；提前 0.14 秒按下跳跃，可在落地时执行。离地宽限结束后保留一次空中跳跃。
- **小台阶辅助**：正常行走可以跨上一个 0.5 单位地形格，头顶空间不足时不会自动抬升。
- **贴墙射击**：发射前检查整个枪管路径，防止枪口越过薄墙生成火箭。枪口受阻会提示，射击仍可用来爆破近处土墙。
- **反馈**：加入掩体吸收提示、受伤方向、敌机受击闪光、命中标记和枪口闪光。

完整变更见 [CHANGELOG.md](CHANGELOG.md)。更多敌人、坠机残骸、完整设置及最终美术仍待开发。

## 试玩

本机已有构建时双击根目录 `试玩.cmd`。源码克隆不含编译产物，请先在 Unity 中运行或生成 Windows 构建。

1. 使用已激活适用许可的 Unity **6000.3.27f2** 打开工程根目录。
2. 等待依赖导入，打开 `Assets/Scenes/Prototype.unity`。
3. 进入 Play Mode。

| 操作 | 按键 |
|---|---|
| 移动 | A / D 或左右方向键 |
| 跳跃、空中再跳 | 空格、W 或上方向键 |
| 瞄准 | 鼠标 |
| 火箭射击 | 鼠标左键，可按住 |
| 附近挖掘 | 鼠标右键，可按住 |
| 暂停 / 继续 | Esc |
| 重新开始 | R |
| 退出独立程序 | 暂停或结算时按 Q，或关闭窗口 |

默认只有一个火箭位，拾取补给可增至最多三个；落地恢复两次跳跃。初始三条生命，复活时短暂保护。清空本波敌机和炸弹后进入下一波，飞出画面的敌机会循环飞越战场。第 4 波起引入需要多次命中的装甲飞机。

地形为 0.5 单位网格的三维剖面，最底层基岩保留；土层吸收是基于网格的游戏规则近似。界面暂用英文，音效由代码合成。

## Windows 构建和后台验收

环境：Universal RP **17.3.0**、Pipeline **0.9.1-exp.1**、Windows x64 / Mono。开发使用 Unity CLI **1.0.0-beta.15**。

安装官方 Unity CLI 后，在工程根目录运行 PowerShell：

```powershell
$cli = 'unity'
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Force -Path 'Logs' | Out-Null
$editorArgs = '-logFile "' + (Join-Path $projectPath 'Logs/BackgroundEditor.log') + '" -job-worker-count 4'
& $cli open $projectPath --editor-version 6000.3.27f2 --batchmode --until-ready --timeout 60 --args $editorArgs --format json
& $cli command build run --project-path $projectPath --format json -- --target StandaloneWindows64 --outputPath Builds/Skybreach/Skybreach.exe --scenes '["Assets/Scenes/Prototype.unity"]' --confirm true
& $cli command build status --project-path $projectPath --format json
```

等待 `completed` 和 `Succeeded` 后执行：

```powershell
& './AgentScripts/VerifyStandalone.ps1'
```

通过后可运行 `./AgentScripts/PackageWindows.ps1` 生成带游玩说明的 `Builds/Skybreach-v0.3.0-Windows.zip`。

独立程序与依赖位于 `Builds/Skybreach`；复制给其他电脑时需要整个文件夹。Unity 缓存、构建输出和本机日志不入库。

验收使用游戏内部控制输入，不发送系统键鼠事件。后台模式忽略用户在其他软件中的操作。检查覆盖基础战斗、移动与掩体回归，以及高速机连投、直升机预警和非追踪火箭、拾取升级、双火箭位、穿透命中、升级复活保留、前六波推进与奖励。

结果见 [验收摘要](Docs/Validation/README.md)、[独立程序记录](Docs/Validation/standalone.json)和[构建汇总](Docs/Validation/acceptance.json)。本机完整日志位于 `Logs/`。

验收使用无图形、无声音模式，射击阶段固定目标位置以保证重复，正常飞行投弹另行检查。人工键鼠手感、声音、长时间稳定性和图形性能仍需实际试玩；后台帧数不能代表游戏帧率。

## 工程结构

- `Assets/Scenes/Prototype.unity`：游戏入口，运行时生成战场。
- `Assets/Scripts/`：操作、敌机、战斗、掩体判定、地形和界面。
- `GameplayAutomation.cs`、`CombatFeelChecks.cs`、`BattleVarietyChecks.cs`：仅通过验收参数启用的自动检查。
- `Assets/Art/Prototype/`：构建使用的材质。
- `AgentScripts/BuildPrototype.cs`：通过 Unity API 重建入口和材质。
- `AgentScripts/ArrangePreview.cs`、`CapturePrototype.cs`：后台场景预览。
- `AgentScripts/VerifyStandalone.ps1`：独立程序验收入口。
- `Packages/`、`ProjectSettings/`：依赖与项目设置。
- `Bootstrap.unity` 和对应脚本：早期环境验证，非游戏入口。

CLI 重启编辑器后可能进入空场景；开始 Play Mode 前明确打开 `Assets/Scenes/Prototype.unity`。修改持久脚本后先确认编译完成，运行前开启后台推进并用 `job wait` 验证帧数确实变化，结束后还原覆盖设置。场景和材质通过 Unity API 保存。

## 素材和历史

本原型依据游戏规则独立实现，没有导入原 APK 的代码、图像或音频。用户提供的素材库作为后续候选，本版未导入外部模型，详见 [ASSET_SOURCES.md](ASSET_SOURCES.md)。

主分支已切换为 Unity，原 Godot 工程保留在[历史提交](https://github.com/Yixuan-Tao/Airstrike/tree/8a4cc94ae6e95015b24b92bd49b9a5f1b46c8091)。沿用仓库原有 [MIT License](LICENSE)，Unity 和依赖包遵循各自许可。
