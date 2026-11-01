param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.5.1')
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$buildPath = Join-Path $projectPath 'Builds\Skybreach'
$archivePath = Join-Path $projectPath ('Builds\Skybreach-v' + $Version + '-Windows.zip')
if (!(Test-Path -LiteralPath (Join-Path $buildPath 'Skybreach.exe'))) { throw 'Build the Windows player first.' }
@"
SKYBREACH v$Version - Windows 2.5D combat prototype

解压整个文件夹后运行 Skybreach.exe。请保留程序旁边的数据目录和 DLL。

A / D 或左右方向键：移动
空格 / W / 上方向键：跳跃，可在空中再跳一次
鼠标：瞄准
左键：火箭射击，可按住；默认一个火箭位，拾取补给可增加到最多三个
右键：附近挖掘
Esc：暂停或继续
R：重新开始
暂停或结算时 Q：退出

v0.5.1：前三波改为1、2、3架敌机，普通机前六波每隔4至6秒随机沿航线投弹。保留浅弹坑，让玩家有更多时间挖掘和调整站位。
偶数波有补给机，击落才掉货。EMP拾取后击落视野内敌机；跳跳靴解锁连续空中跳跃。捕获飞行员加命，最多六条。补给和飞行员靠近自动领取，土墙阻挡领取；复活保留升级，重开清空。
保留v0.4.1的浅弹坑规则。补给机只有一次飞越机会；箱子和飞行员会过期，不会阻止下一波。
当前使用占位美术。键鼠手感、声音及长时间性能请以实际试玩为准。

源码：https://github.com/Yixuan-Tao/Airstrike
"@ | Set-Content -LiteralPath (Join-Path $buildPath '游玩说明.txt') -Encoding UTF8
Compress-Archive -LiteralPath $buildPath -DestinationPath $archivePath -CompressionLevel Optimal -Force
[pscustomobject]@{ archive=$archivePath; bytes=(Get-Item -LiteralPath $archivePath).Length; sha256=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash }
