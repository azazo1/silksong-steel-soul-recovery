using System.Reflection;
using GlobalEnums;
using UnityEngine.UI;

namespace SteelSoulRecovery.Ui
{
    // 决定恢复选项什么时候出现: 只有"碎掉的钢魂存档"才显示.
    internal static class PromptOption
    {
        // saveStats 是 SaveSlotButton 的私有序列化字段, 只能反射取.
        private static readonly FieldInfo SaveStatsField =
            typeof(SaveSlotButton).GetField("saveStats", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Sync(SaveSlotButton slot)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || slot == null || slot.clearSavePrompt == null)
            {
                return;
            }

            if (!plugin.Settings.AddPromptOption.Value)
            {
                return;
            }

            bool defeated = IsDefeatedSteelSoul(slot);

            // 点击时按 marker 上记的槽位走: 每次弹出确认框都会刷新这个记录.
            PromptOptionMarker marker = null;
            MenuButton button = PromptButtonFactory.Ensure(
                slot,
                Labels.RecoveryOption(plugin.Settings),
                delegate
                {
                    if (marker != null && marker.Slot != null)
                    {
                        plugin.Revival.Revive(marker.Slot);
                    }
                });

            if (button == null)
            {
                plugin.Log.LogError(string.Format("槽位 {0} 的恢复选项没能挂上去", slot.SaveSlotIndex));
                return;
            }

            marker = button.GetComponent<PromptOptionMarker>();
            if (marker != null)
            {
                marker.Slot = slot;
            }

            if (button.gameObject.activeSelf != defeated)
            {
                button.gameObject.SetActive(defeated);
            }

            if (defeated)
            {
                plugin.Log.LogInfo(string.Format(
                    "槽位 {0} 是碎掉的钢魂存档, 清除存档确认框里已备好恢复选项",
                    slot.SaveSlotIndex));
            }
        }

        internal static bool IsDefeatedSteelSoul(SaveSlotButton slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (SaveStatsField != null)
            {
                SaveStats stats = SaveStatsField.GetValue(slot) as SaveStats;
                if (stats != null)
                {
                    return stats.PermadeathMode == PermadeathModes.Dead;
                }
            }

            // 读不到存档统计就退回用界面状态判断.
            return slot.State == SaveSlotButton.SlotState.Defeated;
        }
    }
}
