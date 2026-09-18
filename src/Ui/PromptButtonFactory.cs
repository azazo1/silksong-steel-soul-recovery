using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SteelSoulRecovery.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SteelSoulRecovery.Ui
{
    // 往游戏的"清除存档"确认框里补一个按钮.
    //
    // 优先克隆提示框里已有的按钮: 外观, 音效, 选中动画都跟游戏原生的一致, 只需要换掉文字和点击回调.
    // 克隆不到模板时退化成自己搭一个按钮, 保证功能可用, 外观会朴素一些.
    internal static class PromptButtonFactory
    {
        internal const string OptionName = "SteelSoulRecoveryOption";

        // 量不出按钮间距时, 用按钮高度的这个比例当间距.
        private const float GapRatio = 0.35f;

        private const float FallbackWidth = 260f;

        private const float FallbackHeight = 36f;

        private static readonly Color FallbackColor = new Color(1f, 1f, 1f, 0.12f);

        // MenuButtonList.entries 是私有序列化字段, 想把自己的按钮登记进去只能反射.
        private static readonly FieldInfo EntriesField =
            typeof(MenuButtonList).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static MenuButton Ensure(SaveSlotButton slot, string label, UnityAction onClick)
        {
            Transform promptRoot = slot.clearSavePrompt.transform;

            MenuButton existing = FindExisting(promptRoot);
            if (existing != null)
            {
                ApplyLabel(existing, label);
                return existing;
            }

            // 先把原始层级记下来, 之后界面不对可以照着调.
            SaveProfileProbe.MaybeDump(slot);

            MenuButton template = FindTemplate(promptRoot);
            MenuButton created = template != null ? CloneTemplate(template, onClick) : BuildFallback(promptRoot, onClick);
            if (created == null)
            {
                return null;
            }

            created.name = OptionName;
            created.gameObject.AddComponent<PromptOptionMarker>();

            if (!ApplyLabel(created, label))
            {
                SteelSoulRecoveryPlugin.Instance.Log.LogWarning("恢复选项按钮上没有找到文本组件, 按钮会是空的");
            }

            Place(created, template, promptRoot);
            LinkNavigation(promptRoot, created);

            if (template != null)
            {
                SteelSoulRecoveryPlugin.Instance.Log.LogInfo(
                    "恢复选项: 克隆自提示框里的按钮 " + SaveProfileProbe.PathOf(template.transform));
            }
            else
            {
                SteelSoulRecoveryPlugin.Instance.Log.LogWarning(
                    "恢复选项: 提示框里没找到可以克隆的按钮, 改用自建按钮");
            }

            return created;
        }

        private static MenuButton FindExisting(Transform root)
        {
            PromptOptionMarker marker = root.GetComponentInChildren<PromptOptionMarker>(true);
            return marker != null ? marker.GetComponent<MenuButton>() : null;
        }

        private static MenuButton FindTemplate(Transform root)
        {
            MenuButton[] buttons = root.GetComponentsInChildren<MenuButton>(true);
            foreach (MenuButton button in buttons)
            {
                if (button.GetComponent<PromptOptionMarker>() != null)
                {
                    continue;
                }

                if (FindLabel(button.transform) != null)
                {
                    return button;
                }
            }

            return null;
        }

        private static MenuButton CloneTemplate(MenuButton template, UnityAction onClick)
        {
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            MenuButton button = clone.GetComponent<MenuButton>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            // 克隆会把模板上原有的持久化监听一起带过来, 包括 EventTrigger 里那些直接指向槽位
            // (比如"是"按钮上挂着的 ClearSaveConfirmPrompt), 只换 OnSubmitPressed 是不够的 --
            // 那样按我们的按钮会同时触发游戏自己的清除存档流程. 先全部清掉, 再挂我们自己的.
            int stripped = StripListeners(clone);

            // 光清监听还不够保险: EventTrigger 组件本身留着, 以后谁往里加东西都会重新接上游戏的动作.
            // 我们的按钮只依赖 MenuButton 的点击处理, 所以整个组件删掉.
            int triggersRemoved = 0;
            foreach (EventTrigger trigger in clone.GetComponentsInChildren<EventTrigger>(true))
            {
                UnityEngine.Object.Destroy(trigger);
                triggersRemoved++;
            }

            button.OnSubmitPressed = new UnityEvent();
            button.OnSubmitPressed.AddListener(onClick);
            button.interactable = true;

            SteelSoulRecoveryPlugin.Instance.Log.LogInfo(string.Format(
                "恢复选项: 克隆按钮时清掉了 {0} 处游戏原有的监听, 删掉 {1} 个 EventTrigger",
                stripped,
                triggersRemoved));

            return button;
        }

        // 把克隆体上所有 UnityEvent 型监听清空 (含 EventTrigger 的每个条目).
        private static int StripListeners(GameObject clone)
        {
            int stripped = 0;

            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                EventTrigger trigger = component as EventTrigger;
                if (trigger != null)
                {
                    foreach (EventTrigger.Entry entry in trigger.triggers)
                    {
                        if (entry != null && entry.callback != null)
                        {
                            entry.callback.RemoveAllListeners();
                            stripped++;
                        }
                    }
                }

                foreach (FieldInfo field in component.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    UnityEventBase unityEvent = null;
                    try
                    {
                        unityEvent = field.GetValue(component) as UnityEventBase;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (unityEvent != null)
                    {
                        unityEvent.RemoveAllListeners();
                        stripped++;
                    }
                }
            }

            return stripped;
        }

        private static MenuButton BuildFallback(Transform parent, UnityAction onClick)
        {
            GameObject root = new GameObject(OptionName, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            Image background = root.AddComponent<Image>();
            background.color = FallbackColor;
            background.raycastTarget = true;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(root.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.raycastTarget = false;

            MenuButton button = root.AddComponent<MenuButton>();
            button.OnSubmitPressed = new UnityEvent();
            button.OnSubmitPressed.AddListener(onClick);

            PromptOptionHighlight highlight = root.AddComponent<PromptOptionHighlight>();
            highlight.Bind(background);
            return button;
        }

        private static bool ApplyLabel(MenuButton button, string label)
        {
            Component target = FindLabel(button.transform);
            if (target == null)
            {
                return false;
            }

            TMP_Text tmp = target as TMP_Text;
            if (tmp != null)
            {
                tmp.text = label;
                return true;
            }

            Text legacy = target as Text;
            if (legacy != null)
            {
                legacy.text = label;
                return true;
            }

            return false;
        }

        private static Component FindLabel(Transform root)
        {
            TMP_Text tmp = root.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                return tmp;
            }

            return root.GetComponentInChildren<Text>(true);
        }

        // 放到提示框里最靠下那个按钮的下面.
        //
        // 基准必须取"当前最靠下那个按钮"而不是克隆来源: 克隆源可能排在中间 (比如"是"),
        // 按它算位置会把新按钮直接叠到下一个按钮上. 尺寸用世界坐标算, 因为这类按钮的实际大小
        // 往往来自 anchor 而不是 sizeDelta.
        private static void Place(MenuButton created, MenuButton template, Transform root)
        {
            RectTransform rect = created.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            Transform parent = created.transform.parent;
            if (parent != null && parent.GetComponent<LayoutGroup>() != null)
            {
                // 有布局组就交给布局组排, 我们只管排在最后.
                created.transform.SetAsLastSibling();
                return;
            }

            RectTransform templateRect = template != null ? template.transform as RectTransform : null;
            if (templateRect != null)
            {
                rect.anchorMin = templateRect.anchorMin;
                rect.anchorMax = templateRect.anchorMax;
                rect.pivot = templateRect.pivot;
                rect.sizeDelta = templateRect.sizeDelta;
            }

            List<RectTransform> siblings = GetButtonRects(root);
            if (siblings.Count == 0)
            {
                // 提示框里一个可克隆的按钮都没有: 自己搭一个, 尺寸按提示框本身的尺寸折算,
                // 免得写死的像素值碰上被缩放的画布就变成一个小点.
                RectTransform promptRect = root as RectTransform;
                float width = promptRect != null && promptRect.rect.width > 0f ? promptRect.rect.width * 0.6f : FallbackWidth;
                float height = promptRect != null && promptRect.rect.height > 0f ? promptRect.rect.height * 0.18f : FallbackHeight;

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            siblings.Sort(CompareByWorldYDescending);
            RectTransform lowest = siblings[siblings.Count - 1];
            float lowestBottom = GetWorldBottom(lowest);

            float ourHeight = GetWorldTop(rect) - GetWorldBottom(rect);
            if (ourHeight <= 0f)
            {
                ourHeight = GetWorldTop(lowest) - lowestBottom;
            }

            float gap = EstimateGap(siblings, ourHeight);

            float ourCenter = (GetWorldBottom(rect) + GetWorldTop(rect)) * 0.5f;
            float desiredCenter = lowestBottom - gap - ourHeight * 0.5f;

            created.transform.position += new Vector3(0f, desiredCenter - ourCenter, 0f);

            SteelSoulRecoveryPlugin.Instance.Log.LogInfo(string.Format(
                "恢复选项: 放到 {0} 下面 (间距 {1:0.###}, 高度 {2:0.###}, 摆好后 y {3:0.###} ~ {4:0.###})",
                lowest.name,
                gap,
                ourHeight,
                GetWorldBottom(rect),
                GetWorldTop(rect)));
        }

        private static List<RectTransform> GetButtonRects(Transform root)
        {
            List<RectTransform> result = new List<RectTransform>();
            foreach (MenuButton button in root.GetComponentsInChildren<MenuButton>(true))
            {
                if (button.GetComponent<PromptOptionMarker>() != null)
                {
                    continue;
                }

                RectTransform rect = button.transform as RectTransform;
                if (rect != null)
                {
                    result.Add(rect);
                }
            }

            return result;
        }

        private static int CompareByWorldYDescending(RectTransform left, RectTransform right)
        {
            return right.position.y.CompareTo(left.position.y);
        }

        // 用最下面两个按钮的间距当间距, 这样新按钮和原有按钮的疏密一致.
        // 这里的数值是画布内部的世界单位 (会被画布缩放), 所以判断标准必须相对按钮高度, 不能用绝对像素:
        // 之前用过 "间距 > 0.5 才算数", 结果把缩放后真实间距不到 0.5 的情况当成无效, 退回到写死的 6,
        // 于是按钮被推到很下面是去了.
        private static float EstimateGap(List<RectTransform> orderedByYDescending, float buttonHeight)
        {
            float fallback = buttonHeight > 0f ? buttonHeight * GapRatio : GapRatio;

            if (orderedByYDescending.Count < 2)
            {
                return fallback;
            }

            RectTransform upper = orderedByYDescending[orderedByYDescending.Count - 2];
            RectTransform lower = orderedByYDescending[orderedByYDescending.Count - 1];
            float gap = GetWorldBottom(upper) - GetWorldTop(lower);
            if (gap > 0f && gap < buttonHeight * 4f)
            {
                return gap;
            }

            return fallback;
        }

        private static float GetWorldBottom(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        }

        private static float GetWorldTop(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        }

        // 让新按钮进入提示框的上下(或左右)导航链.
        private static void LinkNavigation(Transform root, MenuButton created)
        {
            MenuButtonList owner = FindOwningList(root);
            if (owner != null && TryRegisterInList(owner, created))
            {
                SteelSoulRecoveryPlugin.Instance.Log.LogInfo(
                    "恢复选项: 已登记进提示框的 MenuButtonList, 导航交给游戏自己串");
                return;
            }

            List<MenuButton> buttons = root.GetComponentsInChildren<MenuButton>(true).ToList();
            if (buttons.Count < 2)
            {
                return;
            }

            bool horizontal = IsHorizontal(buttons);
            List<MenuButton> ordered = horizontal
                ? buttons.OrderBy(button => button.transform.position.x).ToList()
                : buttons.OrderByDescending(button => button.transform.position.y).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                Navigation navigation = ordered[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                MenuButton previous = ordered[(i - 1 + ordered.Count) % ordered.Count];
                MenuButton next = ordered[(i + 1) % ordered.Count];
                if (horizontal)
                {
                    navigation.selectOnLeft = previous;
                    navigation.selectOnRight = next;
                }
                else
                {
                    navigation.selectOnUp = previous;
                    navigation.selectOnDown = next;
                }

                ordered[i].navigation = navigation;
            }

            SteelSoulRecoveryPlugin.Instance.Log.LogInfo(string.Format(
                "恢复选项: 提示框里没有管导航的 MenuButtonList, 已手动把 {0} 个按钮串成{1}链",
                ordered.Count,
                horizontal ? "左右" : "上下"));
        }

        private static bool IsHorizontal(List<MenuButton> buttons)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (MenuButton button in buttons)
            {
                Vector3 position = button.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            return (maxX - minX) > (maxY - minY);
        }

        private static MenuButtonList FindOwningList(Transform root)
        {
            List<Selectable> promptButtons = root
                .GetComponentsInChildren<MenuButton>(true)
                .Cast<Selectable>()
                .ToList();

            foreach (MenuButtonList list in root.GetComponentsInChildren<MenuButtonList>(true))
            {
                if (ContainsAny(list, promptButtons))
                {
                    return list;
                }
            }

            Transform parent = root.parent;
            while (parent != null)
            {
                MenuButtonList list = parent.GetComponent<MenuButtonList>();
                if (list != null && ContainsAny(list, promptButtons))
                {
                    return list;
                }

                parent = parent.parent;
            }

            return null;
        }

        private static bool ContainsAny(MenuButtonList list, List<Selectable> selectables)
        {
            Array entries = GetEntries(list);
            if (entries == null)
            {
                return false;
            }

            foreach (object entry in entries)
            {
                Selectable selectable = GetEntrySelectable(entry);
                if (selectable != null && selectables.Contains(selectable))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryRegisterInList(MenuButtonList list, MenuButton created)
        {
            Array entries = GetEntries(list);
            if (entries == null)
            {
                return false;
            }

            Type entryType = entries.GetType().GetElementType();
            if (entryType == null)
            {
                return false;
            }

            FieldInfo selectableField = entryType.GetField("selectable", BindingFlags.Instance | BindingFlags.NonPublic);
            if (selectableField == null)
            {
                return false;
            }

            object entry;
            try
            {
                entry = Activator.CreateInstance(entryType);
            }
            catch (Exception)
            {
                return false;
            }

            if (entry == null)
            {
                return false;
            }

            selectableField.SetValue(entry, created);

            Array extended = Array.CreateInstance(entryType, entries.Length + 1);
            Array.Copy(entries, extended, entries.Length);
            extended.SetValue(entry, entries.Length);

            try
            {
                EntriesField.SetValue(list, extended);
                list.SetupActive();
            }
            catch (Exception exception)
            {
                SteelSoulRecoveryPlugin.Instance.Log.LogWarning("登记 MenuButtonList 失败: " + exception.Message);
                return false;
            }

            return true;
        }

        private static Array GetEntries(MenuButtonList list)
        {
            if (EntriesField == null || list == null)
            {
                return null;
            }

            try
            {
                return EntriesField.GetValue(list) as Array;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Selectable GetEntrySelectable(object entry)
        {
            if (entry == null)
            {
                return null;
            }

            PropertyInfo property = entry.GetType().GetProperty("Selectable");
            if (property == null)
            {
                return null;
            }

            return property.GetValue(entry, null) as Selectable;
        }
    }
}
