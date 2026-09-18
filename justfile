[private]
default:
    @just --list

# 编译插件到 bin/
build:
    pwsh -File build.ps1

# 编译并安装到游戏目录 (默认取 SilksongPath.props 里的路径)
install:
    pwsh -File build.ps1 -Install

# 编译并安装到指定游戏目录
# just install-to 'D:\games\steam\common\Hollow Knight Silksong'
install-to gamedir:
    pwsh -File build.ps1 -Install -GameDir '{{gamedir}}'

# 创建或补齐隔离子实例, 需要 -Source 指向源安装
# just instance 'D:\games\steam\common\Hollow Knight Silksong'
instance source:
    pwsh -File game/prepare-instance.ps1 -Source '{{source}}'

# 启动隔离子实例
launch:
    pwsh -File game/launch-instance.ps1

# 解锁新建存档时的钢魂模式选项
steel-soul-on:
    pwsh -File tools/set-status-record.ps1 -Key RecPermadeathMode -Value 1

# 往实例的 1 号槽位写一份"碎掉的钢魂存档"用于测试
# just test-save 1
test-save slot='1':
    pwsh -File tools/make-test-save.ps1 -Slot {{slot}} -Force

# 启动隔离子实例并等它退出, 退出后打印 BepInEx 日志尾部
launch-wait:
    pwsh -File game/launch-instance.ps1 -Wait

# 查看隔离子实例的 BepInEx 日志尾部
# just log 80
log lines='40':
    pwsh -Command "Get-Content -LiteralPath 'game/Hollow Knight Silksong/BepInEx/LogOutput.log' -Tail {{lines}}"
