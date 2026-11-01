param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.4.0')
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$buildPath = Join-Path $projectPath 'Builds\Skybreach'
$archivePath = Join-Path $projectPath ('Builds\Skybreach-v' + $Version + '-Windows.zip')
if (!(Test-Path -LiteralPath (Join-Path $buildPath 'Skybreach.exe'))) { throw 'Build the Windows player first.' }
@'
SKYBREACH v0.4 - Windows 2.5D combat prototype

解压整个文件夹后运行 Skybreach.exe。请保留程序旁边的数据目录和 DLL。

A / D 或左右方向键：移动
空格 / W / 上方向键：跳跃，可在空中再跳一次
鼠标：瞄准
左键：火箭射击，可按住；默认一个火箭位，拾取补给可增加到最多三个
右键：附近挖掘
Esc：暂停或继续
R：重新开始
暂停或结算时 Q：退出

v0.4：统一黄昏场景、分节人物动作、地形断面和爆炸烟尘。保留三类敌人、两种补给与前六波配置；升级本局复活后保留，重新开始时清空。
当前使用占位美术。键鼠手感、声音及长时间性能请以实际试玩为准。

源码：https://github.com/Yixuan-Tao/Airstrike
'@ | Set-Content -LiteralPath (Join-Path $buildPath '游玩说明.txt') -Encoding UTF8
Compress-Archive -LiteralPath $buildPath -DestinationPath $archivePath -CompressionLevel Optimal -Force
[pscustomobject]@{ archive=$archivePath; bytes=(Get-Item -LiteralPath $archivePath).Length; sha256=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash }
