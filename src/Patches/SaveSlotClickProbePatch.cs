using System;
using HarmonyLib;
using UnityEngine.UI;

namespace SteelSoulRecovery.Patches
{
    // 只读诊断: 记录"玩家到底点没点到槽位".
    //
    // 鼠标点击走 OnPointerClick, 手柄/键盘的确认走 OnSubmit, 两个都记下来就能分清
    // 是点击根本没命中 (画面上没有接收点击的图元), 还是命中了但 OnSubmit 提前返回了.
    // 只在 Diagnostics/DumpSaveProfileUi 打开时生效.
    [HarmonyPatch(typeof(SaveSlotButton), "OnPointerClick")]
    internal static class SaveSlotPointerClickProbePatch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveSlotButton __instance)
        {
            Log(__instance, "OnPointerClick");
        }

        private static void Log(SaveSlotButton slot, string source)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || !plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            try
            {
                plugin.Log.LogInfo(string.Format(
                    "{0} 到达槽位 {1}: {2}",
                    source,
                    slot.SaveSlotIndex,
                    SaveSlotClassificationPatch.Describe(slot)));
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("记录点击失败: " + exception.Message);
            }
        }
    }

    [HarmonyPatch(typeof(SaveSlotButton), "OnSubmit")]
    internal static class SaveSlotSubmitProbePatch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveSlotButton __instance)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || !plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            try
            {
                plugin.Log.LogInfo(string.Format(
                    "OnSubmit 到达槽位 {0}: {1}",
                    __instance.SaveSlotIndex,
                    SaveSlotClassificationPatch.Describe(__instance)));
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("记录提交失败: " + exception.Message);
            }
        }
    }
}
