using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace SteelSoulRecovery.Diagnostics
{
    // 排查用: 打印"游戏认为的槽位文件路径"以及它到底在不在.
    //
    // 恢复之后槽位偶尔会显示成"空槽位", 症状是点进去其实还能继续玩 (说明存档写成功了),
    // 只是刷新那一刻游戏判定这个槽位没有文件 (saveFileState = Empty) 而画面被切到了新游戏.
    // 所以这里把路径、文件长度、以及判定结果一起记下来.
    internal static class SaveSlotAccessProbe
    {
        internal static void LogState(ManualLogSource log, SaveSlotButton slot, int slotIndex, string stage)
        {
            if (log == null || slot == null)
            {
                return;
            }

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("槽位 {0} [{1}]: saveFileState={2}, State={3}", slotIndex, stage, slot.saveFileState, slot.State);

                string directory = SaveStoreProbe.GetSaveDirectory();
                builder.AppendFormat(", saveDirPath={0}", directory ?? "(反射不到)");

                if (!string.IsNullOrEmpty(directory))
                {
                    string name = slotIndex == 0 ? "user.dat" : string.Format("user{0}.dat", slotIndex);
                    string path = Path.Combine(directory, name);
                    FileInfo info = new FileInfo(path);
                    builder.AppendFormat(", {0}: exists={1}", name, info.Exists);
                    if (info.Exists)
                    {
                        builder.AppendFormat(", {0} 字节", info.Length);
                    }
                }

                log.LogInfo(builder.ToString());
            }
            catch (Exception exception)
            {
                log.LogWarning("探查槽位文件失败: " + exception.Message);
            }
        }
    }
}
