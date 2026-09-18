# 丝之歌 钢魂存档恢复 (Steel Soul Recovery)

一个 BepInEx 插件, 用来在存档界面恢复钢魂模式碎掉的存档.

## 原理

游戏里钢魂角色死亡时并不会删除存档:

1. `HeroController.Die()` 把 `playerData.permadeathMode` 从 `On` 改成 `Dead`;
2. `GameManager.PlayerDead()` 紧接着 `SaveGame()`, 把这份**死亡现场的数据**落盘.

所以碎掉的存档其实还在, 进度也在, 只是存档界面把这个槽位画成碎裂的钢球, 除了"清除存档"以外点不动.
游戏自己的"恢复存档"功能 (回退到自动存档点) 在正常/损坏/版本不兼容三种状态下都会显示,
唯独在钢魂阵亡状态下被 `restoreSaveButton.Hide()` 藏掉了, 玩家点不到.

本插件在存档界面的"清除存档"确认框里补一个**恢复钢魂存档**选项, 就地改写这个槽位的存档:

- `permadeathMode`: `Dead` -> `On`
- 血量补满 (可关, 见配置)
- `disablePause`: 死亡流程里会被置 `true` 并写进存档, 一并抹平

改完让槽位重新读盘并切回可继续游玩的状态, 当前进度全部保留.

> 想回退到更早的进度 (比如某个自动存档点) 而不是"就地复活"的话, 那是另一件事:
> 游戏自带的恢复存档功能本来就支持, 只是钢魂阵亡时入口被藏了, 本插件不做这部分.

## 用法

打开碎掉的钢魂存档的"清除存档"确认框, 里面会多出**恢复钢魂存档**一项, 选中即可.

- 手柄/键盘: 在存档界面选中该槽位按确认, 确认框就出来了.
- 鼠标: 该状态下游戏把卡片自己的背景关掉了, 卡片本身不接收鼠标 (这是游戏原有行为, 本插件不动它),
  所以要点卡片下方的垃圾桶图标来打开同一个确认框.

如果界面注入在你的游戏版本上失效 (见下文"排查"), 还有一条逃生通道: 在存档界面选中碎掉的槽位, 按 `F8`.

## 配置

首次运行后落在 `BepInEx/config/silksong.steel-soul-recovery.cfg`, 装了 Configuration Manager 可以按 F5 直接改.

| 配置项 | 默认 | 说明 |
| --- | --- | --- |
| `General/Enabled` | `true` | 总开关 |
| `General/AddPromptOption` | `true` | 是否往确认框里补恢复选项 |
| `General/RestoreHealth` | `true` | 复活时补满血量; 关掉则只在血量为 0 时补到 1 |
| `General/KeepBackup` | `true` | 改写前把原始存档备份到 `BepInEx/plugins/SteelSoulRecovery/backup/` |
| `General/ButtonLabel` | 空 | 按钮文字, 留空按游戏语言自动选 (中文界面上是"恢复钢魂存档") |
| `General/HotkeyEnabled` / `Hotkey` | `true` / `F8` | 上面说的逃生通道 |
| `Diagnostics/DumpSaveProfileUi` | `false` | 把存档界面与确认框的 UI 层级写进日志 |

## 目录结构

| 路径 | 内容 |
| --- | --- |
| `src/` | 插件源码, 按职责分层: `Recovery/` 存档读写与改写, `Ui/` 界面注入, `Config/` 配置, `Patches/` Harmony 补丁, `Diagnostics/` 排查用 |
| `build.ps1` | 编译 (调用 Visual Studio 自带的 Roslyn csc), `-Install` 会复制到游戏的 `BepInEx/plugins` |
| `SilksongPath.props` | 本机游戏目录, 不入版本库 |
| `game/` | 隔离子实例: 准备/启动脚本, 以及实例专用的 `instance-tools` 插件 (路径与设置重定向), 详见 `game/README.md` |
| `tools/` | 存档编解码, 自带一份测试用存档模板, 造测试存档 / 改跨存档设置的脚本 |

## 构建与测试

```shell
# 编译并装进隔离子实例
just install

# 启动实例
just launch

# 看日志
just log 120
```

没有现成的钢魂存档时, 可以造一份: 仓库自带一份存档模板, 脚本只改里面的 `permadeathMode` 再写进实例的槽位.

```shell
# 解锁新建存档时的钢魂模式选项
just steel-soul-on

# 往 1 号槽位写一份碎掉的钢魂存档
just test-save 1
```

## 排查

界面注入依赖游戏预制体的实际结构 (确认框里按钮挂在哪一层, 有没有布局组, 导航与 EventTrigger 是谁在管),
这些在静态反编译里看不到. 打开这一个开关, 插件会把排查需要的东西都写进日志:

```ini
[Diagnostics]
DumpSaveProfileUi = true
```

打开后依次会记下:

- 运行位置: `Application.persistentDataPath` / 引擎日志路径 / 实际存档目录 (确认实例隔离是否生效);
- 每个槽位的判定结果: `saveFileState`, `SlotState`, `permadeathMode` 以及几个 CanvasGroup 的开关;
- 玩家点击是否到达槽位 (`OnPointerClick` / `OnSubmit`);
- 清除存档流程的调用栈 (`ClearSaveFile` / `ClearSaveConfirmPrompt`);
- 存档界面与确认框的完整层级, 包括每个 `EventTrigger` 的条目与它指向的物体和方法.

另外, 常规日志里也会写明恢复选项这次是"克隆了游戏按钮"还是"退化成自建按钮", 清掉了多少处游戏原有的监听,
新按钮摆在了哪个按钮下面, 以及导航是怎么串的.
