param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.7.0')
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$buildPath = Join-Path $projectPath 'Builds\Skybreach'
$archivePath = Join-Path $projectPath ('Builds\Skybreach-v' + $Version + '-Windows.zip')
if (!(Test-Path -LiteralPath (Join-Path $buildPath 'Skybreach.exe'))) { throw 'Build the Windows player first.' }
if (!(Test-Path -LiteralPath (Join-Path $buildPath 'Skybreach_Data\StreamingAssets\Balance.csv'))) { throw 'The build is missing Balance.csv.' }
@"
SKYBREACH v$Version - Windows 2.5D combat prototype

解压整个文件夹后运行 Skybreach.exe。请保留程序旁边的数据目录和 DLL。

点击主菜单或按 1 进入当前版本，按 2 进入原版数值。原版档以《空袭》4.69 简单模式数值为基线，并在 Unity 战场中运行。
按 F1 或点击主菜单的语言按钮切换简体中文 / English，语言选择会保留。

A / D 或左右方向键：移动
空格 / W / 上方向键：跳跃，可在空中再跳一次
鼠标：瞄准
左键：火箭射击，可按住
右键：附近挖掘
Esc：暂停或继续
R：用当前档位重开
暂停或结算时 M：返回主菜单；Q：退出

数值表在 Skybreach_Data\StreamingAssets\Balance.csv。修改 PC 或 Classic 列并保存为 UTF-8 CSV，回主菜单选择该入口或按 R 重开即可读到新值。请保留 Key 和表头，使用点作为小数分隔符。

Choose a ruleset with 1 or 2. F1 switches Simplified Chinese / English.
During pause or results press M for the menu. R restarts the selected mode.
Edit PC or Classic in Skybreach_Data\StreamingAssets\Balance.csv, then start a new run.

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
