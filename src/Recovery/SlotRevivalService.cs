using System.Collections;
using SteelSoulRecovery.Config;
using UnityEngine;
using UnityEngine.UI;

namespace SteelSoulRecovery.Recovery
{
    // 编排一次"就地复活":
    // 读槽位原始字节 -> 改数据 -> 备份 -> 写回 -> 让槽位重新读盘并切回可用状态.
    internal sealed class SlotRevivalService
    {
        // 等槽位重新读盘的上限, 超时只记警告, 不阻塞界面.
        private const float RefreshTimeout = 8f;

        private readonly SteelSoulRecoveryPlugin _plugin;

        private readonly RecoveryConfig _config;

        private readonly SaveSlotGateway _gateway;

        internal SlotRevivalService(SteelSoulRecoveryPlugin plugin, RecoveryConfig config)
        {
            _plugin = plugin;
            _config = config;
            _gateway = new SaveSlotGateway(plugin, config);
        }

        internal void Revive(SaveSlotButton slot)
        {
            if (slot == null)
            {
                return;
            }

            if (!_config.Enabled.Value)
            {
                _plugin.Log.LogInfo("恢复功能已在配置里关闭");
                return;
            }

            int slotIndex = slot.SaveSlotIndex;
            _plugin.Log.LogInfo(string.Format("请求恢复槽位 {0} 的钢魂存档", slotIndex));

            _gateway.Read(slotIndex, delegate(byte[] raw)
            {
                CoreLoop.InvokeNext(delegate { OnSlotRead(slot, slotIndex, raw); });
            });
        }

        private void OnSlotRead(SaveSlotButton slot, int slotIndex, byte[] raw)
        {
            if (slot == null)
            {
                return;
            }

            if (raw == null || raw.Length == 0)
            {
                _plugin.Log.LogError(string.Format("槽位 {0} 读不到存档内容, 放弃恢复", slotIndex));
                return;
            }

            SaveGameData saveData;
            string error;
            if (!_gateway.TryDecode(raw, out saveData, out error))
            {
                _plugin.Log.LogError(string.Format("槽位 {0} 的存档解析失败: {1}", slotIndex, error));
                return;
            }

            RevivalReport report = SteelSoulRevival.Apply(saveData, _config.RestoreHealth.Value);
            if (!report.Applied)
            {
                _plugin.Log.LogWarning(string.Format("槽位 {0} 不需要恢复: {1}", slotIndex, report.Summary));
                return;
            }

            string backupPath = _gateway.Backup(slotIndex, raw);
            if (backupPath != null)
            {
                _plugin.Log.LogInfo("原存档已备份: " + backupPath);
            }

            byte[] updated;
            try
            {
                updated = _gateway.Encode(saveData);
            }
            catch (System.Exception exception)
            {
                _plugin.Log.LogError("重新编码存档失败, 没有写入任何东西: " + exception);
                return;
            }

            _gateway.Write(slotIndex, updated, delegate(bool written)
            {
                CoreLoop.InvokeNext(delegate { OnSlotWritten(slot, slotIndex, written, report); });
            });
        }

        private void OnSlotWritten(SaveSlotButton slot, int slotIndex, bool written, RevivalReport report)
        {
            if (slot == null)
            {
                return;
            }

            if (!written)
            {
                _plugin.Log.LogError(string.Format("槽位 {0} 写盘失败, 存档保持原样", slotIndex));
                return;
            }

            _plugin.Log.LogInfo(string.Format("槽位 {0} 已恢复: {1}", slotIndex, report.Summary));

            if (slot.gameObject.activeInHierarchy)
            {
                slot.StartCoroutine(RefreshSlot(slot, slotIndex));
            }
        }

        // 让槽位重新读一遍磁盘上的存档, 再按新状态刷新显示.
        // 玩家此刻还停在"清除存档"确认框里, 所以是从 ClearPrompt 切回 SavePresent, 游戏自己有这条转移.
        private IEnumerator RefreshSlot(SaveSlotButton slot, int slotIndex)
        {
            GameManager gameManager = GameManager.instance;
            if (gameManager == null)
            {
                _plugin.Log.LogWarning("GameManager 不在了, 槽位显示要等重新进存档界面才会更新");
                yield break;
            }

            slot.Prepare(gameManager, true, false, false);

            float deadline = Time.realtimeSinceStartup + RefreshTimeout;
            while (slot.saveFileState == SaveSlotButton.SaveFileStates.OperationInProgress ||
                   slot.saveFileState == SaveSlotButton.SaveFileStates.NotStarted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    _plugin.Log.LogWarning(string.Format("槽位 {0} 刷新超时, 显示的存档信息可能还是旧的", slotIndex));
                    yield break;
                }

                yield return null;
            }

            if (slot.saveFileState != SaveSlotButton.SaveFileStates.LoadedStats)
            {
                _plugin.Log.LogWarning(string.Format(
                    "槽位 {0} 重新读盘后状态是 {1}, 不是预期的 LoadedStats",
                    slotIndex,
                    slot.saveFileState));
                yield break;
            }

            slot.ShowRelevantModeForSaveFileState();
            _plugin.Log.LogInfo(string.Format("槽位 {0} 已回到可继续游玩的状态", slotIndex));
        }
    }
}
