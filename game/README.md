# 丝之歌隔离子实例

`game/Hollow Knight Silksong/` 是从 Steam 安装复制出来的游戏子实例, 用来单独启动和测试插件, 不动源安装.
源安装目录由 `-Source` 或环境变量 `SILKSONG_GAME_DIR` 指定, 不写死在脚本里.

## 隔离了什么

| 隔离项 | 做法 | 效果 |
| --- | --- | --- |
| 游戏本体 | 只读数据目录做目录联接, 其余走真实副本 | 往实例装插件, 改配置, 写日志都不碰源安装 |
| 存档与设置 | 实例里的 `InstanceTools` 插件在代码层接管 `Application.persistentDataPath` | 存档与设置落在 `<实例目录>/savedata`, 真存档不会被读也不会被写 |
| PlayerPrefs | 同上插件把读写重定向到 `savedata/instance-prefs.txt` | 不写注册表 `HKCU\Software\<公司名>\<产品名>` |
| 引擎日志 | 启动脚本用引擎自带的 `-logFile` 指定 | `Player.log` 落在 `<实例目录>/Player.log` |
| Steam 接入 | 实例的 `_Data/Plugins/x86_64/steam_api64.dll` 改名为 `steam_api64.dll.disabled` | 游戏连不上 Steam, 测试期间的成就/云存档/游戏时长都不落账 |

共享 (目录联接, 不占额外空间): `<exe>_Data` 下的 `Managed`, `Resources`, `StreamingAssets`, 以及根目录的 `MonoBleedingEdge`, `D3D12`.

独立 (真实副本): 可执行文件, `doorstop_config.ini`, `BepInEx/`, `<exe>_Data` 下的小数据文件与 `Plugins`.

`app.info` 与 `globalgamemanagers` 都保持源安装的原样, 不改动.

## 为什么隔离都在插件侧做

一开始试过"改游戏身份"的老办法 (改 `app.info`, 甚至等长替换 `globalgamemanagers` 里烘焙的公司名),
但 Unity 6 的 `Application.persistentDataPath` 取的是烘焙值, `app.info` 只管 `Player.log`,
于是出现"日志在隔离目录, 存档却写进真存档目录"的假隔离; 而动 `globalgamemanagers` 又要迁就定长字符串.

改成在代码层接管就简单了: 路径是游戏自己算出来的, 那就把算出来的结果换掉.
`persistentDataPath` 一改, `DesktopPlatform.saveDirPath` 和所有存档文件自然跟着走.

配套的两处也是同一个思路:

- PlayerPrefs 在 Windows 上写注册表, 在受限环境下会抛 `PlayerPrefsException`, 表现是卡在语言选择界面点不动,
  所以整体重定向到实例目录下的文本文件;
- 游戏写存档用的是"写 `.new` 再 `File.Replace` 换名", `File.Replace` 底层是 ReplaceFile,
  在受限环境下会被拒 (存档写不进去, 游戏弹"无法保存进度"), 所以换成 `Copy`/`Delete`/`Move` 的等价实现.

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

# 启动实例 (存档与日志都落在实例目录里)
pwsh -File game/launch-instance.ps1
```

## 日志

- 引擎日志: `game/Hollow Knight Silksong/Player.log`
- BepInEx: `game/Hollow Knight Silksong/BepInEx/LogOutput.log`
- 实例存档: `game/Hollow Knight Silksong/savedata/default/`

BepInEx 启动时会报 `Unable to start Unity log writer`, 这时 `Debug.Log` 不会进 `LogOutput.log`, 调试插件要同时看 `Player.log`.

## 注意

- 断开 Steam 之后, 游戏内的成就, 云存档, 联机相关功能不可用; 想恢复就加 `-KeepSteam` 重跑一遍准备脚本.
- 数据目录是共享的, 只适合读. 如果某个插件会往 `Hollow Knight Silksong_Data` 里写文件, 请用 `-FullCopy` 重建.
- 副本的可执行文件不随 Steam 自动更新, 版本落后时用 `-RefreshBinaries` 刷新.
- 换了公司名等于换了一套 PlayerPrefs; 现在 PlayerPrefs 落在实例的 `savedata/instance-prefs.txt`, 删掉它就会重新走一次语言选择.
- `game/instance-tools/` 里的插件是给实例用的, 不属于 mod 本体, 打包发布时不要带上.
