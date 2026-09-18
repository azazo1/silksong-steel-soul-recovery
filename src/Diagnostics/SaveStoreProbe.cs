using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace SteelSoulRecovery.Diagnostics
{
    // 排查用: 把游戏真正使用的存档目录, 目录里的文件, 以及游戏自己的解码结果写进日志.
    //
    // 存档界面显示的槽位信息是游戏读盘 + 解密 + 反序列化算出来的,
    // 一旦"文件里明明写着 Dead 但界面按正常存档显示", 就要先确认游戏到底读了哪个目录的哪个文件,
    // 这个探针把三方 (持久化目录 / Platform 实际用的目录 / Platform.ReadSaveSlot 的返回) 一次列出来.
    internal static class SaveStoreProbe
    {
        private static bool _dumped;

        private static bool _locationDumped;

        // 这一步不需要任何界面操作, 只要 Platform 起来了就打一次,
        // 用来确认游戏实际用的是哪个存档目录 (隔离实例最关键的检查点).
        internal static void MaybeDumpLocation()
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || _locationDumped)
            {
                return;
            }

            if (!plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            if (Platform.Current == null)
            {
                return;
            }

            _locationDumped = true;

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("=== 运行位置探查 ===");
                builder.AppendFormat("Application.dataPath: {0}", Application.dataPath).AppendLine();
                builder.AppendFormat("Application.companyName: {0}", Application.companyName).AppendLine();
                builder.AppendFormat("Application.productName: {0}", Application.productName).AppendLine();
                builder.AppendFormat("Application.persistentDataPath: {0}", Application.persistentDataPath).AppendLine();
                builder.AppendFormat("Application.consoleLogPath: {0}", Application.consoleLogPath).AppendLine();
                builder.AppendFormat("Platform 实际用的存档目录: {0}", GetSaveDirectory() ?? "(反射不到)").AppendLine();
                builder.AppendFormat("命令行: {0}", Environment.CommandLine);
                plugin.Log.LogInfo(builder.ToString());
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("运行位置探查失败: " + exception);
            }
        }

        internal static void MaybeDump()
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || _dumped)
            {
                return;
            }

            if (!plugin.Settings.DumpSaveProfileUi.Value)
            {
                return;
            }

            _dumped = true;

            try
            {
                Dump(plugin.Log);
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("存档目录探查失败: " + exception);
            }
        }

        private static void Dump(ManualLogSource log)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("=== 存档目录探查 ===");
            builder.AppendFormat("Application.persistentDataPath: {0}", Application.persistentDataPath).AppendLine();

            string directory = GetSaveDirectory();
            builder.AppendFormat("Platform 实际用的存档目录: {0}", directory ?? "(反射不到)").AppendLine();

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                foreach (string file in Directory.GetFiles(directory, "*.dat"))
                {
                    builder.AppendLine(DescribeFile(file));
                }
            }
            else
            {
                builder.AppendLine("(目录不存在或者拿不到)");
            }

            log.LogInfo(builder.ToString());

            Platform platform = Platform.Current;
            if (platform == null)
            {
                return;
            }

            for (int slot = 1; slot <= 4; slot++)
            {
                int captured = slot;
                platform.ReadSaveSlot(captured, delegate(byte[] bytes)
                {
                    log.LogInfo(string.Format(
                        "Platform.ReadSaveSlot({0}) -> {1}",
                        captured,
                        DescribeBytes(bytes)));
                });
            }
        }

        private static string DescribeFile(string path)
        {
            try
            {
                return string.Format("  {0}: {1}", Path.GetFileName(path), DescribeBytes(File.ReadAllBytes(path)));
            }
            catch (Exception exception)
            {
                return string.Format("  {0}: 读文件失败 ({1})", Path.GetFileName(path), exception.Message);
            }
        }

        private static string DescribeBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return "null";
            }

            try
            {
                GameManager gameManager = GameManager.instance;
                if (gameManager == null)
                {
                    return string.Format("{0} 字节 (GameManager 不在, 没法解码)", bytes.Length);
                }

                string json = gameManager.GetJsonForSaveBytes(bytes);
                SaveGameData saveData = SaveDataUtility.DeserializeSaveData<SaveGameData>(json);
                if (saveData == null || saveData.playerData == null)
                {
                    return string.Format("{0} 字节 (解出来是空的)", bytes.Length);
                }

                return string.Format(
                    "{0} 字节, playerData.version={1}, permadeathMode={2}, health={3}",
                    bytes.Length,
                    saveData.playerData.version,
                    saveData.playerData.permadeathMode,
                    saveData.playerData.health);
            }
            catch (Exception exception)
            {
                return string.Format("{0} 字节, 解码失败: {1}", bytes.Length, exception.Message);
            }
        }

        internal static string GetSaveDirectory()
        {
            Platform platform = Platform.Current;
            if (platform == null)
            {
                return null;
            }

            Type type = platform.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    "saveDirPath",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field.GetValue(platform) as string;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
