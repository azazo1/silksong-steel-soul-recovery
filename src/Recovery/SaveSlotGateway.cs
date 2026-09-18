using System;
using System.IO;
using BepInEx;
using SteelSoulRecovery.Config;

namespace SteelSoulRecovery.Recovery
{
    // 存档槽的读写全部走游戏自己的 Platform / GameManager 接口,
    // 加密, 版本备份, 云端分支这些就都交给游戏处理, 插件只关心 JSON 里的字段.
    internal sealed class SaveSlotGateway
    {
        private const string BackupFolderName = "backup";

        private readonly SteelSoulRecoveryPlugin _plugin;

        private readonly RecoveryConfig _config;

        internal SaveSlotGateway(SteelSoulRecoveryPlugin plugin, RecoveryConfig config)
        {
            _plugin = plugin;
            _config = config;
        }

        internal void Read(int slot, Action<byte[]> callback)
        {
            Platform.Current.ReadSaveSlot(slot, callback);
        }

        internal bool TryDecode(byte[] raw, out SaveGameData saveData, out string error)
        {
            saveData = null;
            error = null;

            GameManager gameManager = GameManager.instance;
            if (gameManager == null)
            {
                error = "GameManager 还没准备好";
                return false;
            }

            try
            {
                string json = gameManager.GetJsonForSaveBytes(raw);
                saveData = SaveDataUtility.DeserializeSaveData<SaveGameData>(json);
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }

            if (saveData == null || saveData.playerData == null)
            {
                error = "存档解析出来是空的";
                return false;
            }

            return true;
        }

        internal byte[] Encode(SaveGameData saveData)
        {
            return GameManager.instance.GetBytesForSaveData(saveData);
        }

        internal void Write(int slot, byte[] bytes, Action<bool> callback)
        {
            Platform.Current.WriteSaveSlot(slot, bytes, callback);
        }

        // 改写之前留一份原始字节. 失败不算致命, 记一条警告继续.
        internal string Backup(int slot, byte[] raw)
        {
            if (!_config.KeepBackup.Value)
            {
                return null;
            }

            try
            {
                string directory = Path.Combine(Paths.PluginPath, "SteelSoulRecovery", BackupFolderName);
                Directory.CreateDirectory(directory);
                string name = string.Format("user{0}-{1:yyyyMMdd-HHmmss}.dat", slot, DateTime.Now);
                string path = Path.Combine(directory, name);
                File.WriteAllBytes(path, raw);
                return path;
            }
            catch (Exception exception)
            {
                _plugin.Log.LogWarning("备份原始存档失败: " + exception.Message);
                return null;
            }
        }
    }
}
