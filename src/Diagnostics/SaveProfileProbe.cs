using System;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteelSoulRecovery.Diagnostics
{
    // 把存档界面与"清除存档"确认框的 UI 层级写进日志.
    //
    // 界面注入依赖游戏预制体的结构 (按钮在哪一层, 有没有布局组, 导航是谁在管),
    // 这些静态反编译看不到, 所以留一个开关把运行时层级打出来.
    internal static class SaveProfileProbe
    {
        private const int ProfileScreenDepth = 4;

        private const int PromptDepth = 10;

        private const int NodeBudget = 400;

        private static bool _dumped;

        internal static void MaybeDump(SaveSlotButton slot)
        {
            SteelSoulRecoveryPlugin plugin = SteelSoulRecoveryPlugin.Instance;
            if (plugin == null || _dumped || slot == null)
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
                StringBuilder builder = new StringBuilder();
                UIManager ui = UIManager.instance;

                builder.AppendLine("=== 存档界面 saveProfileScreen ===");
                if (ui != null && ui.saveProfileScreen != null)
                {
                    int budget = NodeBudget;
                    Dump(ui.saveProfileScreen.transform, 0, ProfileScreenDepth, builder, ref budget);
                }
                else
                {
                    builder.AppendLine("(拿不到 UIManager.saveProfileScreen)");
                }

                builder.AppendLine();
                builder.AppendFormat("=== 槽位 {0} 的清除存档确认框 clearSavePrompt ===", slot.SaveSlotIndex);
                builder.AppendLine();
                if (slot.clearSavePrompt != null)
                {
                    int budget = NodeBudget;
                    Dump(slot.clearSavePrompt.transform, 0, PromptDepth, builder, ref budget);
                }
                else
                {
                    builder.AppendLine("(拿不到 clearSavePrompt)");
                }

                plugin.Log.LogInfo(builder.ToString());
            }
            catch (Exception exception)
            {
                plugin.Log.LogWarning("dump 存档界面层级失败: " + exception);
            }
        }

        internal static string PathOf(Transform node)
        {
            if (node == null)
            {
                return "(null)";
            }

            StringBuilder builder = new StringBuilder(node.name);
            Transform parent = node.parent;
            while (parent != null)
            {
                builder.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return builder.ToString();
        }

        private static void Dump(Transform node, int depth, int maxDepth, StringBuilder builder, ref int budget)
        {
            if (node == null || budget <= 0)
            {
                return;
            }

            budget--;

            builder.Append(' ', depth * 2);
            builder.Append(node.name);
            if (!node.gameObject.activeSelf)
            {
                builder.Append(" [inactive]");
            }

            builder.Append(Describe(node));
            builder.AppendLine();

            if (depth >= maxDepth)
            {
                if (node.childCount > 0)
                {
                    builder.Append(' ', (depth + 1) * 2);
                    builder.AppendFormat("... 还有 {0} 个子节点没有展开", node.childCount);
                    builder.AppendLine();
                }

                return;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Dump(node.GetChild(i), depth + 1, maxDepth, builder, ref budget);
                if (budget <= 0)
                {
                    builder.AppendLine("... 节点太多, 后面省略");
                    return;
                }
            }
        }

        private static string Describe(Transform node)
        {
            StringBuilder builder = new StringBuilder(" {");

            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                CanvasGroup canvasGroup = component as CanvasGroup;
                if (canvasGroup != null)
                {
                    builder.AppendFormat(" CanvasGroup(a={0:0.##},i={1},b={2})", canvasGroup.alpha, canvasGroup.interactable, canvasGroup.blocksRaycasts);
                    continue;
                }

                TMP_Text tmp = component as TMP_Text;
                if (tmp != null)
                {
                    builder.AppendFormat(" TMP(\"{0}\")", tmp.text);
                    continue;
                }

                Text legacy = component as Text;
                if (legacy != null)
                {
                    builder.AppendFormat(" Text(\"{0}\")", legacy.text);
                    continue;
                }

                Image image = component as Image;
                if (image != null)
                {
                    builder.AppendFormat(" Image({0})", image.sprite != null ? image.sprite.name : "none");
                    continue;
                }

                string typeName = component.GetType().Name;
                switch (typeName)
                {
                    case "MenuButton":
                    case "MenuButtonList":
                    case "MenuButtonListCondition":
                    case "Animator":
                    case "LayoutGroup":
                    case "VerticalLayoutGroup":
                    case "HorizontalLayoutGroup":
                    case "GridLayoutGroup":
                    case "PreselectOption":
                    case "SaveSlotButton":
                    case "RestoreSaveButton":
                        builder.Append(' ').Append(typeName);
                        break;
                }
            }

            builder.Append(" }");

            Selectable selectable = node.GetComponent<Selectable>();
            if (selectable != null)
            {
                Navigation navigation = selectable.navigation;
                builder.AppendFormat(
                    " nav[{0} up={1} down={2} left={3} right={4}]",
                    navigation.mode,
                    NameOf(navigation.selectOnUp),
                    NameOf(navigation.selectOnDown),
                    NameOf(navigation.selectOnLeft),
                    NameOf(navigation.selectOnRight));
            }

            return builder.ToString();
        }

        private static string NameOf(Selectable selectable)
        {
            return selectable != null ? selectable.name : "-";
        }
    }
}
