using System;
using HarmonyLib;
using SteelSoulRecovery.Ui;
using UnityEngine.UI;

namespace SteelSoulRecovery.Patches
{
    // 在存档界面上按下一个"已阵亡"的钢魂存档槽位时, 游戏会走 ClearSavePrompt 弹确认框.
    // 跟着这个时机把恢复选项补进确认框, 同时把非钢魂槽位上的选项藏起来.
    [HarmonyPatch(typeof(SaveSlotButton), "ClearSavePrompt")]
    internal static class ClearSavePromptPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SaveSlotButton __instance)
        {
            try
            {
                PromptOption.Sync(__instance);
            }
            catch (Exception exception)
            {
                SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
                if (plugin != null)
                {
                    plugin.Log.LogError("挂载恢复选项时出错 (不影响游戏原有流程): " + exception);
                }
            }
        }
    }
}
