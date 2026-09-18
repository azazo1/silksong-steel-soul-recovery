namespace SteelSoulRecovery.Recovery
{
    // 一次就地复活改动了什么, 便于写日志和给玩家看.
    internal sealed class RevivalReport
    {
        internal RevivalReport(bool applied, string summary)
        {
            Applied = applied;
            Summary = summary;
        }

        // 是否真的做了改动 (存档不是碎掉的钢魂存档时为 false).
        internal bool Applied { get; private set; }

        internal string Summary { get; private set; }
    }
}
