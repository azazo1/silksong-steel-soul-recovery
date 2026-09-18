using UnityEngine;
using UnityEngine.UI;

namespace SteelSoulRecovery.Ui
{
    // 贴在我们自己造出来的那个恢复按钮上.
    //
    // 一是用来把"插进去的按钮"和游戏原有按钮区分开;
    // 二是记下当前这个按钮代表哪个槽位 -- 万一游戏把同一个确认框预制体复用给所有槽位,
    // 点击时按这里记的槽位走才不会串到别的存档上.
    internal sealed class PromptOptionMarker : MonoBehaviour
    {
        internal SaveSlotButton Slot { get; set; }
    }
}
