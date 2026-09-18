# 丝之歌隔离子实例

`game/Hollow Knight Silksong/` 是从 Steam 安装复制出来的游戏子实例, 用来单独启动和测试插件, 不动源安装.
源安装目录由 `-Source` 或环境变量 `SILKSONG_GAME_DIR` 指定, 不写死在脚本里.

## 隔离了什么

| 隔离项 | 做法 | 效果 |
| --- | --- | --- |
| 游戏本体 | 只读数据目录做目录联接, 其余走真实副本 | 往实例装插件, 改配置, 写日志都不碰源安装 |
| 存档与游戏设置 | 实例的 `_Data/app.info` 里公司名改成 `Team Cherry Mod` | 存档与设置落在 `%USERPROFILE%\AppData\LocalLow\Team Cherry Mod\Hollow Knight Silksong`, 真存档不会被读也不会被写 |
| Steam 接入 | 实例的 `_Data/Plugins/x86_64/steam_api64.dll` 改名为 `steam_api64.dll.disabled` | 游戏连不上 Steam, 测试期间的成就/云存档/游戏时长都不落账 |

共享 (目录联接, 不占额外空间): `<exe>_Data` 下的 `Managed`, `Resources`, `StreamingAssets`, 以及根目录的 `MonoBleedingEdge`, `D3D12`.

独立 (真实副本): 可执行文件, `doorstop_config.ini`, `BepInEx/`, `<exe>_Data` 下的小数据文件 (`app.info`, `globalgamemanagers` 等) 与 `Plugins`.

## 常用操作

```shell
# 首次创建, 之后重跑只补齐缺失内容
pwsh -File game/prepare-instance.ps1 -Source 'D:\games\steam\common\Hollow Knight Silksong'

# 源安装更新后, 覆盖刷新可执行文件与 BepInEx 基础文件
pwsh -File game/prepare-instance.ps1 -Source '<源安装>' -RefreshBinaries

# 恢复 Steam 接入 (只测存档隔离时用)
pwsh -File game/prepare-instance.ps1 -Source '<源安装>' -KeepSteam

# 连数据目录也完整复制 (约 7.8 GB), 隔离最彻底
pwsh -File game/prepare-instance.ps1 -Source '<源安装>' -FullCopy

# 删掉重建
pwsh -File game/prepare-instance.ps1 -Source '<源安装>' -Force

# 启动实例
pwsh -File game/launch-instance.ps1
```

`-Source` 缺省读环境变量 `SILKSONG_GAME_DIR`.

## 日志

- BepInEx: `game/Hollow Knight Silksong/BepInEx/LogOutput.log`
- Unity `Debug.Log`: 存档目录下的 `Player.log`

BepInEx 启动时会报 `Unable to start Unity log writer`, 这时 `Debug.Log` 不会进 `LogOutput.log`, 调试插件要同时看 `Player.log`.

## 注意

- 断开 Steam 之后, 游戏内的成就, 云存档, 联机相关功能不可用; 想恢复就加 `-KeepSteam` 重跑一遍准备脚本.
- 数据目录是共享的, 只适合读. 如果某个插件会往 `Hollow Knight Silksong_Data` 里写文件, 请用 `-FullCopy` 重建.
- 副本的可执行文件不随 Steam 自动更新, 版本落后时用 `-RefreshBinaries` 刷新.
- 这个实例的存档目录与真存档目录是分开的, 要在里面测"碎掉的钢魂存档", 得先在这个实例里真的打一份钢魂存档出来 (或者从真存档目录复制一份 `user<N>.dat` 过去).
