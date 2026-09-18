using BepInEx.Configuration;
using UnityEngine;

namespace SteelSoulRecovery.Config
{
    // 插件配置, 落在 BepInEx/config/silksong.steel-soul-recovery.cfg.
    internal sealed class RecoveryConfig
    {
        internal RecoveryConfig(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "总开关, 关掉后不响应任何恢复请求");

            AddPromptOption = config.Bind(
                "General",
                "AddPromptOption",
                true,
                "在存档界面的\"清除存档\"确认框里补一个恢复选项");

            RestoreHealth = config.Bind(
                "General",
                "RestoreHealth",
                true,
                "复活时把血量补满; 关掉则只在血量为 0 时补到 1");

            KeepBackup = config.Bind(
                "General",
                "KeepBackup",
                true,
                "改写存档之前把原始文件备份到 BepInEx/plugins/SteelSoulRecovery/backup/");

            ButtonLabel = config.Bind(
                "General",
                "ButtonLabel",
                string.Empty,
                "恢复选项的按钮文字, 留空则按游戏当前语言自动选");

            HotkeyEnabled = config.Bind(
                "General",
                "HotkeyEnabled",
                true,
                "逃生通道: 界面注入万一失效, 还可以在存档界面选中槽位后按快捷键恢复");

            Hotkey = config.Bind(
                "General",
                "Hotkey",
                new KeyboardShortcut(KeyCode.F8),
                "上面那个快捷键");

            DumpSaveProfileUi = config.Bind(
                "Diagnostics",
                "DumpSaveProfileUi",
                false,
                "把存档界面与清除存档确认框的 UI 层级写进日志 (排查界面注入问题时打开)");
        }

        internal ConfigEntry<bool> Enabled { get; private set; }

        internal ConfigEntry<bool> AddPromptOption { get; private set; }

        internal ConfigEntry<bool> RestoreHealth { get; private set; }

        internal ConfigEntry<bool> KeepBackup { get; private set; }

        internal ConfigEntry<string> ButtonLabel { get; private set; }

        internal ConfigEntry<bool> HotkeyEnabled { get; private set; }

        internal ConfigEntry<KeyboardShortcut> Hotkey { get; private set; }

        internal ConfigEntry<bool> DumpSaveProfileUi { get; private set; }
    }
}
