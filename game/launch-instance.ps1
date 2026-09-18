# 启动 game/ 下的丝之歌隔离子实例.
#
# 用法:
#   pwsh -File game/launch-instance.ps1          # 后台启动, 立即返回
#   pwsh -File game/launch-instance.ps1 -Wait    # 等游戏退出, 然后打印 BepInEx 日志尾部
#
# 实例已经做过三层隔离 (实例目录 = 仓库里的 game\Hollow Knight Silksong, 层级说明见 game/README.md):
#   - 存档与设置: 由实例里的 InstanceTools 插件接管 Application.persistentDataPath, 落在 <实例目录>\savedata,
#     与源安装完全分开; PlayerPrefs 也一并重定向到那个目录下的文本文件.
#   - 引擎日志: 由下面的 -logFile 参数指到 <实例目录>\Player.log.
#   - Steam 接入: steam_api64.dll 被改名为 .disabled 时, 游戏不会连 Steam,
#     测试期间的成就/云存档/游戏时长都不会落到你的账号上; 需要恢复就用 prepare-instance.ps1 -KeepSteam.

[CmdletBinding()]
param(
    [string]$Target = (Join-Path $PSScriptRoot 'Hollow Knight Silksong'),
    [switch]$Wait,
    [int]$LogTailLines = 40
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $Target 'Hollow Knight Silksong.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "找不到实例: $exe, 先运行 pwsh -File game/prepare-instance.ps1"
}

$dataDirectory = Get-ChildItem -LiteralPath $Target -Force -Directory |
    Where-Object { $_.Name -like '*_Data' } |
    Select-Object -First 1

Write-Host "[instance] 存档目录: $(Join-Path $Target 'savedata\default')"

$steamDll = Join-Path $dataDirectory.FullName 'Plugins\x86_64\steam_api64.dll'
if (Test-Path -LiteralPath $steamDll) {
    Write-Host '[instance] Steam 接入: 启用 (steam_api64.dll 在位)'
}
else {
    Write-Host '[instance] Steam 接入: 已断开 (steam_api64.dll 被改名)'
}

# Unity 的 Player.log 是引擎在托管代码跑起来之前写的, 位置由构建时的公司名决定.
# 用引擎自带的 -logFile 参数把它指到实例目录里, 这样受限环境下也能写得进去, 不必去动 app.info.
# 路径里有空格, 必须自己带引号, 否则 Unity 的命令行解析会在空格处截断.
$unityLog = Join-Path $Target 'Player.log'

Write-Host "[instance] 启动: $exe"
$process = Start-Process -FilePath $exe -ArgumentList @('-logFile', "`"$unityLog`"") -WorkingDirectory $Target -PassThru

if (-not $Wait) {
    Write-Host "[instance] 进程号 $($process.Id), 引擎日志: $unityLog"
    Write-Host "[instance] BepInEx 日志: $(Join-Path $Target 'BepInEx\LogOutput.log')"
    return
}

Write-Host '[instance] 等待游戏退出 ...'
$process.WaitForExit()
Write-Host "[instance] 游戏已退出 (exit=$($process.ExitCode))"

$log = Join-Path $Target 'BepInEx\LogOutput.log'
if (Test-Path -LiteralPath $log) {
    Write-Host "[instance] BepInEx 日志尾部 ($log):"
    Get-Content -LiteralPath $log -Tail $LogTailLines | ForEach-Object { "  $_" }
}
else {
    Write-Host "[instance] 没有找到 BepInEx 日志: $log"
}
