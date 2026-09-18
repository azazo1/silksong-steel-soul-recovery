# 编译插件.
#
# 两个产物:
#   SteelSoulRecovery.dll  -- 本仓库的主角, 钢魂存档恢复
#   InstancePrefs.dll      -- 隔离子实例的工具插件, 把游戏的 PlayerPrefs 重定向到实例存档目录下的文本文件
#                             (源码在 game/instance-prefs/, 不属于 mod 本体, 详情见那里)
#
# 本机没有装 .NET SDK, 因此直接调用 Visual Studio 自带的 Roslyn csc.exe 编译,
# 引用游戏目录中的程序集与 BepInEx 自带的 Harmony.
#
# 用法:
#   pwsh -File build.ps1
#   pwsh -File build.ps1 -Install
#   pwsh -File build.ps1 -Install -GameDir 'D:\games\steam\common\Hollow Knight Silksong'
#
# -Install 会把两个 dll 分别复制到 <游戏目录>/BepInEx/plugins/<名字>/.
# -GameDir 可以给相对路径, 相对本目录解析, 与 MSBuild 导入 SilksongPath.props 时的基准一致.
[CmdletBinding()]
param(
    [string]$GameDir,
    [switch]$Install,
    [string]$Tool
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$outputDir = Join-Path $root 'bin'

# 名字 -> 源码目录. 每个项目编成一个同名 dll, 装到 BepInEx/plugins/<名字>/ 下.
$projects = @(
    [pscustomobject]@{
        Name = 'SteelSoulRecovery'
        SourceDir = Join-Path $root 'src'
    },
    [pscustomobject]@{
        Name = 'InstanceTools'
        SourceDir = Join-Path $root 'game\instance-tools\src'
    }
)

function Write-Step {
    param([string]$Message)

    Write-Host "[steel-soul-recovery] $Message"
}

function Resolve-RelativePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    # 相对路径按本目录解析, 与 MSBuild 导入 SilksongPath.props 时的基准保持一致.
    return [System.IO.Path]::GetFullPath((Join-Path $root $Path))
}

function Resolve-GameDir {
    param([string]$Explicit)

    if ($Explicit) {
        return (Resolve-RelativePath $Explicit)
    }

    $propsPath = Join-Path $root 'SilksongPath.props'
    if (Test-Path -LiteralPath $propsPath) {
        $content = Get-Content -LiteralPath $propsPath -Raw
        $match = [regex]::Match($content, '<GameDir>\s*([^<]+?)\s*</GameDir>')
        if ($match.Success) {
            return (Resolve-RelativePath $match.Groups[1].Value)
        }
    }

    throw '找不到游戏目录: 请用 -GameDir 指定, 或先准备 SilksongPath.props.'
}

function Resolve-Compiler {
    param([string]$Explicit)

    if ($Explicit) {
        return $Explicit
    }

    $candidates = Get-ChildItem -Path 'C:\Program Files\Microsoft Visual Studio', 'C:\Program Files (x86)\Microsoft Visual Studio' -Filter 'csc.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'Roslyn' } |
        Sort-Object FullName -Descending

    if ($candidates.Count -gt 0) {
        return $candidates[0].FullName
    }

    $frameworkCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (Test-Path -LiteralPath $frameworkCompiler) {
        return $frameworkCompiler
    }

    throw '找不到 csc.exe: 需要 Visual Studio (Roslyn) 或 .NET Framework 自带的编译器.'
}

$gameDir = Resolve-GameDir -Explicit $GameDir
$managedDir = Join-Path $gameDir 'Hollow Knight Silksong_Data\Managed'
$bepInExCoreDir = Join-Path $gameDir 'BepInEx\core'

if (-not (Test-Path -LiteralPath $managedDir)) {
    throw "游戏目录看起来不对, 找不到: $managedDir"
}

$compiler = Resolve-Compiler -Explicit $Tool
Write-Step "游戏目录: $gameDir"
Write-Step "编译器: $compiler"

$references = @(
    # 游戏程序集以 netstandard2.1 为目标, 需要 netstandard facade 才能解析基础类型.
    (Join-Path $managedDir 'netstandard.dll'),
    (Join-Path $bepInExCoreDir 'BepInEx.dll'),
    (Join-Path $bepInExCoreDir '0Harmony.dll'),
    (Join-Path $managedDir 'Assembly-CSharp.dll'),
    (Join-Path $managedDir 'TeamCherry.Localization.dll'),
    (Join-Path $managedDir 'Newtonsoft.Json.dll'),
    (Join-Path $managedDir 'UnityEngine.dll'),
    (Join-Path $managedDir 'UnityEngine.CoreModule.dll'),
    (Join-Path $managedDir 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managedDir 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $managedDir 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managedDir 'UnityEngine.UIModule.dll'),
    (Join-Path $managedDir 'UnityEngine.UI.dll'),
    (Join-Path $managedDir 'Unity.TextMeshPro.dll')
)

foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "缺少引用程序集: $reference"
    }
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

foreach ($project in $projects) {
    $sources = Get-ChildItem -LiteralPath $project.SourceDir -Recurse -Filter '*.cs' |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
    if ($sources.Count -eq 0) {
        throw "没有找到源文件: $($project.SourceDir)"
    }

    $outputDll = Join-Path $outputDir "$($project.Name).dll"

    $arguments = @(
        '/nologo'
        '/target:library'
        '/langversion:7.3'
        '/optimize+'
        '/deterministic+'
        "/out:$outputDll"
    )
    foreach ($reference in $references) {
        $arguments += "/r:$reference"
    }
    $arguments += $sources

    Write-Step "编译 $($project.Name): $($sources.Count) 个源文件 ..."
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "csc 编译失败 ($($project.Name), exit=$LASTEXITCODE)"
    }

    Write-Step "产物: $outputDll"

    if ($Install) {
        $pluginDir = Join-Path $gameDir "BepInEx\plugins\$($project.Name)"
        New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
        Copy-Item -LiteralPath $outputDll -Destination (Join-Path $pluginDir "$($project.Name).dll") -Force
        Write-Step "已安装插件: $pluginDir"
    }
}
