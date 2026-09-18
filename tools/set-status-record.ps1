# 修改游戏"跨存档的持久化设置"里的一个整数值.
#
# 这类设置存在存档目录的 shared.dat 里 (与 user<N>.dat 是两回事), 格式和存档一样:
# 加密后经 BinaryFormatter 包一层字符串, 解密出来是 {"pairs":[{"Key":"...","Value":"..."}]}.
#
# 常用的几个键:
#   RecPermadeathMode  1 = 在新建存档时出现"钢魂"模式选项
#   RecBossRushMode    1 = 在新建存档时出现"Boss Rush"选项
#   lastProfileIndex   上一次选中的槽位 (本地设置, 走 PlayerPrefs, 不在这里)
#
# 用法:
#   pwsh -File tools/set-status-record.ps1 -Key RecPermadeathMode -Value 1
#   pwsh -File tools/set-status-record.ps1 -Key RecPermadeathMode -Value 1 -SaveDir '<存档目录>'
#
# -SaveDir 缺省取隔离子实例的存档目录 (读实例 app.info 现算), 不会碰真存档.
# 改的时候游戏必须关掉, 否则游戏退出时会用自己的内存状态把文件覆盖回去.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Key,
    [Parameter(Mandatory)][int]$Value,
    [string]$SaveDir
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SaveCodec.ps1')
. (Join-Path $PSScriptRoot 'instance-paths.ps1')

function Write-Step {
    param([string]$Message)

    Write-Host "[status-record] $Message"
}

if (-not $SaveDir) {
    $SaveDir = Get-InstanceSaveDirectory
    Write-Step "存档目录 (实例的 savedata): $SaveDir"
}

function ConvertTo-PairsJson {
    param([array]$Pairs)

    # 这些键和值都是简单的标识符与整数, 不需要 JSON 转义, 所以直接拼字符串,
    # 免得 ConvertTo-Json 的排版差异影响到游戏那边的 JsonUtility.
    $items = foreach ($pair in $Pairs) {
        '{"Key":"' + $pair.Key + '","Value":"' + $pair.Value + '"}'
    }
    return '{"pairs":[' + ($items -join ',') + ']}'
}

$primary = Join-Path $SaveDir 'shared.dat'
if (Test-Path -LiteralPath $primary) {
    $json = ConvertFrom-GameSaveBytes -Bytes ([System.IO.File]::ReadAllBytes($primary))
    Write-Step "原内容: $json"
}
else {
    # 全新实例还没写过 shared.dat, 那就从空表开始建一份.
    $json = '{"pairs":[]}'
    Write-Step "还没有 shared.dat, 从空表开始: $primary"
    if (-not (Test-Path -LiteralPath $SaveDir)) {
        New-Item -ItemType Directory -Force -Path $SaveDir | Out-Null
    }
}

$pairs = @()
if ($json -match '"pairs"') {
    $parsed = $json | ConvertFrom-Json
    if ($parsed.pairs) {
        $pairs = @($parsed.pairs)
    }
}

$updated = @()
$replaced = $false
foreach ($pair in $pairs) {
    if ($pair.Key -eq $Key) {
        $updated += [pscustomobject]@{ Key = $pair.Key; Value = "$Value" }
        $replaced = $true
    }
    else {
        $updated += [pscustomobject]@{ Key = $pair.Key; Value = $pair.Value }
    }
}
if (-not $replaced) {
    $updated += [pscustomobject]@{ Key = $Key; Value = "$Value" }
}

$newJson = ConvertTo-PairsJson -Pairs $updated
Write-Step "新内容: $newJson"

$bytes = ConvertTo-GameSaveBytes -Json $newJson
[System.IO.File]::WriteAllBytes($primary, $bytes)
Write-Step "已写入: $primary"

# 游戏读主文件失败时会退回 .bak, 一起写掉免得两边不一致.
$backup = "$primary.bak"
if (Test-Path -LiteralPath $backup) {
    [System.IO.File]::WriteAllBytes($backup, $bytes)
    Write-Step "已同步: $backup"
}
