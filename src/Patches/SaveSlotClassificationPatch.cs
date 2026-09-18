using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SteelSoulRecovery.Patches
{
    // 只读诊断: 把游戏自己对每个槽位的判定写进日志.
    //
    // 界面注入出问题时, 需要先分清是"游戏根本没把这个槽位当成钢魂阵亡"还是"我们没找到入口",
    // 这两件事在这里一次就能看出来. 只在 Diagnostics/DumpSaveProfileUi 打开时生效.
    [HarmonyPatch(typeof(SaveSlotButton), "ShowRelevantModeForSaveFileState")]
    internal static class SaveSlotClassificationPatch
    {
        private static readonly FieldInfo SaveStatsField =
            typeof(SaveSlotButton).GetField("saveStats", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        private static void Postfix(SaveSlotButton __instance)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || __instance == null || !plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            try
            {
                plugin.Log.LogInfo(Describe(__instance));
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("记录槽位判定失败: " + exception.Message);
            }
        }

        internal static string Describe(SaveSlotButton slot)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("槽位 {0}: saveFileState={1}, State={2}", slot.SaveSlotIndex, slot.saveFileState, slot.State);
            builder.AppendFormat(
                ", interactable={0}, myCanvasGroup(i={1},b={2}), parentBlocker(b={3}), clearSaveButton(i={4},b={5})",
                slot.interactable,
                Interactable(slot.myCanvasGroup),
                Blocks(slot.myCanvasGroup),
                Blocks(slot.parentBlocker),
                Interactable(slot.clearSaveButton),
                Blocks(slot.clearSaveButton));

            SaveStats stats = SaveStatsField != null ? SaveStatsField.GetValue(slot) as SaveStats : null;
            if (stats == null)
            {
                builder.Append(", saveStats=(null)");
                return builder.ToString();
            }

            builder.AppendFormat(
                ", version={0}, permadeathMode={1}, bossRushMode={2}, blackThread={3}, blank={4}",
                stats.Version,
                stats.PermadeathMode,
                stats.BossRushMode,
                stats.IsBlackThreadInfected,
                stats.IsBlank);
            return builder.ToString();
        }

        private static string Interactable(CanvasGroup group)
        {
            return group != null ? group.interactable.ToString() : "n/a";
        }

        private static string Blocks(CanvasGroup group)
        {
            return group != null ? group.blocksRaycasts.ToString() : "n/a";
        }
    }
}
