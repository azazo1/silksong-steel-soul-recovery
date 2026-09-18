using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SteelSoulRecovery.Ui
{
    // 自建按钮没有游戏那套选中光标动画, 用颜色变化补一个能看出来的反馈.
    internal sealed class PromptOptionHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.12f);

        private static readonly Color SelectedColor = new Color(1f, 1f, 1f, 0.32f);

        private Image _background;

        internal void Bind(Image background)
        {
            _background = background;
            Apply(NormalColor);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Apply(SelectedColor);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Apply(NormalColor);
        }

        private void Apply(Color color)
        {
            if (_background != null)
            {
                _background.color = color;
            }
        }
    }
}
