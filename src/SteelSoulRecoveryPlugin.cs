using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SteelSoulRecovery.Config;
using SteelSoulRecovery.Recovery;
using SteelSoulRecovery.Ui;

namespace SteelSoulRecovery
{
    // 钢魂模式碎掉的存档恢复插件.
    //
    // 游戏里钢魂角色死亡时, HeroController.Die 只把 playerData.permadeathMode 从 On 改成 Dead,
    // 随后 GameManager.PlayerDead 立刻把这份数据落盘 -- 存档并没有被删, 进度还在,
    // 只是存档界面上这个槽位变成"已阵亡"状态, 除了清除存档以外点不动.
    //
    // 本插件在存档界面的"清除存档"确认框里补一个恢复选项, 把 Dead 改回 On 让这个槽位能继续玩,
    // 顺带把死亡现场残留的血量之类修一下. 详细取舍见 README.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SteelSoulRecoveryPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "silksong.steel-soul-recovery";

        public const string PluginName = "Steel Soul Recovery";

        public const string PluginVersion = "0.1.0";

        private Harmony _harmony;

        private RecoveryConfig _config;

        private SlotRevivalService _revival;

        private RevivalHotkey _hotkey;

        internal static SteelSoulRecoveryPlugin Instance { get; private set; }

        internal ManualLogSource Log
        {
            get { return Logger; }
        }

        internal RecoveryConfig Settings
        {
            get { return _config; }
        }

        internal SlotRevivalService Revival
        {
            get { return _revival; }
        }

        private void Awake()
        {
            Instance = this;
            _config = new RecoveryConfig(Config);
            _revival = new SlotRevivalService(this, _config);
            _hotkey = new RevivalHotkey(this, _config);

            // 补丁只负责找出"什么时候该出现恢复入口", 具体做什么由上面几个对象决定.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo(string.Format(
                "{0} {1} 已加载. 确认框选项: {2}; 快捷键: {3}.",
                PluginName,
                PluginVersion,
                _config.AddPromptOption.Value ? "开启" : "关闭",
                _config.HotkeyEnabled.Value ? _config.Hotkey.Value.ToString() : "关闭"));
        }

        private void Update()
        {
            if (_hotkey != null)
            {
                _hotkey.Tick();
            }

            Diagnostics.SaveStoreProbe.MaybeDumpLocation();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
