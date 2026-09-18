using System;
using HarmonyLib;
using UnityEngine.UI;

namespace SteelSoulRecovery.Patches
{
    // 只读诊断: 谁在清除存档.
    //
    // 恢复过程中游戏自己的"清除存档"流程仍然会跑一次 (Player.log 里能看到 Save file 1 cleared),
    // 说明有个我们没找到的地方在调用它. 这里把调用栈打出来定位.
    // 只在 Diagnostics/DumpSaveProfileUi 打开时生效.
    [HarmonyPatch(typeof(SaveSlotButton), "ClearSaveFile")]
    internal static class ClearSaveFileProbePatch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveSlotButton __instance)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || !plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            plugin.Log.LogWarning(string.Format(
                "ClearSaveFile 被调用 (槽位 {0}):{1}{2}",
                __instance.SaveSlotIndex,
                Environment.NewLine,
                Environment.StackTrace));
        }
    }

    [HarmonyPatch(typeof(SaveSlotButton), "ClearSaveConfirmPrompt")]
    internal static class ClearSaveConfirmProbePatch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveSlotButton __instance)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || !plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            plugin.Log.LogWarning(string.Format(
                "ClearSaveConfirmPrompt 被调用 (槽位 {0}, saveFileState={1}):{2}{3}",
                __instance.SaveSlotIndex,
                __instance.saveFileState,
                Environment.NewLine,
                Environment.StackTrace));
        }
    }
}
