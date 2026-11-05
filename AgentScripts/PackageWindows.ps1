param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.6.0')
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

v0.6：按原版预算增长，敌机从两侧错开入场，清完敌机约1.6秒后进入下一波。普通炸弹可以留到下一波。
部分击落敌机会冒烟坠地，留下能走上去和挡弹的低矮残骸。右键挖掘和爆炸能清理残骸，普通弹坑仍较浅。
每四波有补给机，击落才掉货。EMP拾取后击落视野内敌机；跳跳靴解锁连续空中跳跃。捕获飞行员加命，最多六条。补给和飞行员靠近自动领取，土墙阻挡领取；复活保留升级，重开清空。
当前使用占位美术。键鼠手感、声音及长时间性能请以实际试玩为准。

源码：https://github.com/Yixuan-Tao/Airstrike
"@ | Set-Content -LiteralPath (Join-Path $buildPath '游玩说明.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryArchive = $archivePath + '.tmp'
$archiveStream = [IO.File]::Create($temporaryArchive)
try {
    $archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $items = Get-ChildItem -LiteralPath $buildPath | Where-Object { $_.Name -notlike '*DoNotShip*' -and $_.Name -notlike '*DontShip*' }
        foreach ($item in $items) {
            $files = if ($item.PSIsContainer) { Get-ChildItem -LiteralPath $item.FullName -File -Recurse } else { $item }
            foreach ($file in $files) {
                $relative = $file.FullName.Substring($buildPath.Length + 1).Replace('\', '/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, ('Skybreach/' + $relative), 'Optimal') | Out-Null
            }
        }
    }
    finally { $archive.Dispose() }
}
finally { $archiveStream.Dispose() }
Move-Item -LiteralPath $temporaryArchive -Destination $archivePath -Force
[pscustomobject]@{ archive=$archivePath; bytes=(Get-Item -LiteralPath $archivePath).Length; sha256=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash }
