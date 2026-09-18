using System;
using System.Collections;
using SteelSoulRecovery.Config;
using SteelSoulRecovery.Diagnostics;
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
                CoreLoop.InvokeNext(delegate { OnSlotWritten(slot, slotIndex, written, report, updated); });
            });
        }

        private void OnSlotWritten(SaveSlotButton slot, int slotIndex, bool written, RevivalReport report, byte[] expected)
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
                slot.StartCoroutine(RefreshSlot(slot, slotIndex, expected));
            }
        }

        // 保险: 写完之后游戏自己的清除存档流程可能还会跑一次, 把文件删掉或写回旧内容.
        // 隔一帧核验一遍, 发现不对就补写.
        private IEnumerator VerifyWrittenBytes(int slotIndex, byte[] expected)
        {
            if (expected == null)
            {
                yield break;
            }

            yield return null;

            bool finished = false;
            bool intact = false;
            _gateway.Read(slotIndex, delegate(byte[] bytes)
            {
                intact = bytes != null && bytes.Length == expected.Length;
                finished = true;
            });

            yield return WaitForFlag(delegate { return finished; }, slotIndex, "读回存档做核验");

            if (intact)
            {
                yield break;
            }

            _plugin.Log.LogWarning(string.Format("槽位 {0} 写完之后又被改动了, 补写一次", slotIndex));

            bool rewritten = false;
            finished = false;
            _gateway.Write(slotIndex, expected, delegate(bool ok)
            {
                rewritten = ok;
                finished = true;
            });

            yield return WaitForFlag(delegate { return finished; }, slotIndex, "补写存档");

            if (!rewritten)
            {
                _plugin.Log.LogError(string.Format("槽位 {0} 补写失败", slotIndex));
            }
        }

        private IEnumerator WaitForFlag(Func<bool> flag, int slotIndex, string what)
        {
            float deadline = Time.realtimeSinceStartup + RefreshTimeout;
            while (!flag())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    _plugin.Log.LogWarning(string.Format("槽位 {0} 等 {1} 超时", slotIndex, what));
                    yield break;
                }

                yield return null;
            }
        }

        // 让槽位重新读一遍磁盘上的存档, 再按新状态刷新显示.
        // 玩家此刻还停在"清除存档"确认框里, 所以是从 ClearPrompt 切回 SavePresent, 游戏自己有这条转移.
        private IEnumerator RefreshSlot(SaveSlotButton slot, int slotIndex, byte[] expected)
        {
            yield return VerifyWrittenBytes(slotIndex, expected);

            GameManager gameManager = GameManager.instance;
            if (gameManager == null)
            {
                _plugin.Log.LogWarning("GameManager 不在了, 槽位显示要等重新进存档界面才会更新");
                yield break;
            }

            SaveSlotAccessProbe.LogState(_plugin.Log, slot, slotIndex, "刷新前");

            // 游戏的 AnimateToSlotState 里, 从"空槽位/已死亡/损坏/不兼容"这几个状态出发没有到
            // SavePresent 的转移. 要是槽位画面正停在这几个状态上, 直接刷新会什么都不发生
            // (内部状态已经是正常存档, 画面还写着"新游戏"), 所以先退回 Hidden 绕一下.
            if (IsDeadEndState(slot.State))
            {
                _plugin.Log.LogInfo(string.Format(
                    "槽位 {0} 的画面停在 {1}, 先退回隐藏态再刷新",
                    slotIndex,
                    slot.State));
                slot.HideSaveSlot(false);
                yield return WaitForState(slot, SaveSlotButton.SlotState.Hidden, slotIndex);
            }

            slot.Prepare(gameManager, true, false, false);
            yield return WaitForSlotRead(slot, slotIndex);

            if (slot.saveFileState == SaveSlotButton.SaveFileStates.Empty)
            {
                // 刚写完盘却被读成空槽位: 写入与读盘的时序撞上了. 隔一帧再读一次.
                _plugin.Log.LogWarning(string.Format(
                    "槽位 {0} 刚写完盘却被读成空槽位, 隔一帧重读一次",
                    slotIndex));
                yield return null;
                slot.Prepare(gameManager, true, false, false);
                yield return WaitForSlotRead(slot, slotIndex);
            }

            SaveSlotAccessProbe.LogState(_plugin.Log, slot, slotIndex, "刷新后");

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

        private IEnumerator WaitForSlotRead(SaveSlotButton slot, int slotIndex)
        {
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
        }

        private IEnumerator WaitForState(
            SaveSlotButton slot,
            SaveSlotButton.SlotState wanted,
            int slotIndex)
        {
            float deadline = Time.realtimeSinceStartup + RefreshTimeout;
            while (slot.State != wanted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    _plugin.Log.LogWarning(string.Format(
                        "槽位 {0} 等 {1} 状态超时 (当前 {2})",
                        slotIndex,
                        wanted,
                        slot.State));
                    yield break;
                }

                yield return null;
            }
        }

        private static bool IsDeadEndState(SaveSlotButton.SlotState state)
        {
            return state == SaveSlotButton.SlotState.EmptySlot ||
                   state == SaveSlotButton.SlotState.Defeated ||
                   state == SaveSlotButton.SlotState.Corrupted ||
                   state == SaveSlotButton.SlotState.Incompatible;
        }
    }
}
