using HarmonyLib;
using SteelSoulRecovery.Diagnostics;

namespace SteelSoulRecovery.Patches
{
    // 存档界面每次打开都会走 UIManager.GoToProfileMenu, 借这个时机打一次存档目录探针.
    [HarmonyPatch(typeof(UIManager), "GoToProfileMenu")]
    internal static class SaveStoreProbePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            SaveStoreProbe.MaybeDump();
        }
    }
}
