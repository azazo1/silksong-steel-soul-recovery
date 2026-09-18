# 造一份"碎掉的钢魂存档", 用来在没有真存档的情况下测插件.
#
# 做法: 把源存档解成 JSON, 只改 playerData.permadeathMode, 再按游戏格式写回第 N 号槽位.
# 源存档可以是任意一份 .dat, 也可以是 gzip 过的存档 JSON (.json.gz);
# 不给 -Source 就用仓库自带的模板 tools/template/steel-soul-dead.json.gz.
#
# 用法:
#   pwsh -File tools/make-test-save.ps1 -Slot 1
#   pwsh -File tools/make-test-save.ps1 -Source '<一份 user2.dat>' -Slot 2 -Mode On -Force
#
# -SaveDir 缺省取隔离子实例的存档目录 (读实例 app.info 现算), 不会碰真存档.

[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot 'template\steel-soul-dead.json.gz'),
    [string]$SaveDir,
    [int]$Slot = 1,
    [ValidateSet('Dead', 'On', 'Off')][string]$Mode = 'Dead',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# 与 GlobalEnums.PermadeathModes 对应.
$modeValues = @{ Off = 0; On = 1; Dead = 2 }

. (Join-Path $PSScriptRoot 'SaveCodec.ps1')
. (Join-Path $PSScriptRoot 'instance-paths.ps1')

function Write-Step {
    param([string]$Message)

    Write-Host "[test-save] $Message"
}

if (-not $SaveDir) {
    $SaveDir = Get-InstanceSaveDirectory
    Write-Step "存档目录 (实例的 savedata): $SaveDir"
}

if (-not (Test-Path -LiteralPath $Source)) {
    throw "找不到源存档: $Source"
}

if ($Slot -lt 1 -or $Slot -gt 4) {
    throw "槽位号应该在 1..4 之间, 实际是 $Slot"
}

$target = Join-Path $SaveDir "user$Slot.dat"
if ((Test-Path -LiteralPath $target) -and -not $Force) {
    throw "目标已存在, 加 -Force 覆盖: $target"
}

if ($Source -like '*.json.gz') {
    $json = ConvertFrom-GameSaveGzip -Bytes ([System.IO.File]::ReadAllBytes($Source))
}
else {
    $json = ConvertFrom-GameSaveBytes -Bytes ([System.IO.File]::ReadAllBytes($Source))
}

$pattern = '"permadeathMode"\s*:\s*-?\d+'
$matches = [regex]::Matches($json, $pattern)
if ($matches.Count -ne 1) {
    throw "存档 JSON 里的 permadeathMode 字段数量是 $($matches.Count), 不敢乱改."
}

$replacement = '"permadeathMode":' + $modeValues[$Mode]
$json = [regex]::Replace($json, $pattern, $replacement)
Write-Step "permadeathMode -> $Mode ($($modeValues[$Mode]))"

$bytes = ConvertTo-GameSaveBytes -Json $json

if (-not (Test-Path -LiteralPath $SaveDir)) {
    New-Item -ItemType Directory -Force -Path $SaveDir | Out-Null
}

[System.IO.File]::WriteAllBytes($target, $bytes)
Write-Step "已写入: $target ($($bytes.Length) 字节)"
Write-Step '进游戏后这个槽位应该显示成碎掉的钢魂存档 (碎裂的钢球 + 只能清除存档).'
