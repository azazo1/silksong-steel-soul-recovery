using System.Collections.Generic;
using GlobalEnums;

namespace SteelSoulRecovery.Recovery
{
    // 把一份"已阵亡"的钢魂存档改回可继续游玩的状态.
    //
    // 这里改的是从磁盘读出来的 SaveGameData, 不是运行时那个 PlayerData.instance,
    // 所以直接写字段就够了, 不需要走游戏那套 Get/Set 变量函数.
    internal static class SteelSoulRevival
    {
        // maxHealth 理论上不会缺, 真缺了也得给个能站起来的血量.
        private const int FallbackMaxHealth = 5;

        internal static RevivalReport Apply(SaveGameData saveData, bool restoreHealth)
        {
            if (saveData == null || saveData.playerData == null)
            {
                return new RevivalReport(false, "存档里没有 playerData");
            }

            PlayerData playerData = saveData.playerData;
            if (playerData.permadeathMode != PermadeathModes.Dead)
            {
                return new RevivalReport(
                    false,
                    string.Format("这个槽位不是碎掉的钢魂存档 (permadeathMode = {0})", playerData.permadeathMode));
            }

            List<string> changes = new List<string>();

            playerData.permadeathMode = PermadeathModes.On;
            changes.Add("permadeathMode Dead -> On");

            if (restoreHealth)
            {
                int target = playerData.maxHealth > 0 ? playerData.maxHealth : FallbackMaxHealth;
                if (playerData.health < target)
                {
                    changes.Add(string.Format("health {0} -> {1}", playerData.health, target));
                    playerData.health = target;
                }
            }
            else if (playerData.health < 1)
            {
                changes.Add("health 0 -> 1");
                playerData.health = 1;
            }

            // 死亡流程里会给 PlayerData.disablePause 置 true, 它是要写进存档的.
            // 正常进场景时游戏会自己清掉, 但存档里留着这个值不正常, 一并抹平.
            if (playerData.disablePause)
            {
                changes.Add("disablePause true -> false");
                playerData.disablePause = false;
            }

            return new RevivalReport(true, string.Join(", ", changes.ToArray()));
        }
    }
}
