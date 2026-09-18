# 定位隔离子实例的存档目录.
#
# 实例的存档目录由实例里的 InstanceTools 插件接管: 它把 Application.persistentDataPath
# 重定向到 <实例目录>/savedata, 游戏的存档就在那里的 default 子目录下.
# 这里不去读 app.info, 也不做任何联接解析 -- 存档就是仓库里的普通文件.

function Get-InstanceRoot {
    param([string]$InstanceRoot)

    if ($InstanceRoot) {
        return $InstanceRoot
    }

    return (Join-Path (Split-Path -Parent $PSScriptRoot) 'game\Hollow Knight Silksong')
}

function Get-InstanceSaveDirectory {
    param([string]$InstanceRoot)

    $root = Get-InstanceRoot -InstanceRoot $InstanceRoot
    if (-not (Test-Path -LiteralPath $root)) {
        throw "找不到隔离子实例: $root, 先跑 pwsh -File game/prepare-instance.ps1"
    }

    return (Join-Path $root 'savedata\default')
}
