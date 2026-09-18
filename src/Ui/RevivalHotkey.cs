using BepInEx.Configuration;
using SteelSoulRecovery.Config;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SteelSoulRecovery.Ui
{
    // 界面注入万一在这个游戏版本上失效, 还能在存档界面选中槽位后按快捷键恢复.
    // 只在存档界面打开时生效, 不会误触.
    internal sealed class RevivalHotkey
    {
        private readonly SteelSoulRecoveryPlugin _plugin;

        private readonly RecoveryConfig _config;

        internal RevivalHotkey(SteelSoulRecoveryPlugin plugin, RecoveryConfig config)
        {
            _plugin = plugin;
            _config = config;
        }

        internal void Tick()
        {
            if (!_config.Enabled.Value || !_config.HotkeyEnabled.Value)
            {
                return;
            }

            KeyboardShortcut shortcut = _config.Hotkey.Value;
            if (shortcut.MainKey == KeyCode.None || !shortcut.IsDown())
            {
                return;
            }

            if (!IsSaveProfileMenuOpen())
            {
                return;
            }

            SaveSlotButton slot = FindSelectedSlot();
            if (slot == null)
            {
                _plugin.Log.LogInfo("快捷键恢复: 存档界面里没有选中的槽位");
                return;
            }

            if (!PromptOption.IsDefeatedSteelSoul(slot))
            {
                _plugin.Log.LogInfo(string.Format("快捷键恢复: 槽位 {0} 不是碎掉的钢魂存档", slot.SaveSlotIndex));
                return;
            }

            _plugin.Revival.Revive(slot);
        }

        private static bool IsSaveProfileMenuOpen()
        {
            UIManager ui = UIManager.instance;
            return ui != null && ui.saveProfileScreen != null && ui.saveProfileScreen.alpha > 0.5f;
        }

        private static SaveSlotButton FindSelectedSlot()
        {
            EventSystem current = EventSystem.current;
            if (current == null || current.currentSelectedGameObject == null)
            {
                return null;
            }

            return current.currentSelectedGameObject.GetComponentInParent<SaveSlotButton>();
        }
    }
}
