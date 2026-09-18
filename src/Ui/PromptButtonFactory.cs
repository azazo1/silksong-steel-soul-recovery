using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SteelSoulRecovery.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        private const float Gap = 6f;

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

            // 克隆会把模板上原有的持久化监听一起带过来, 整个换掉最干净.
            button.OnSubmitPressed = new UnityEvent();
            button.OnSubmitPressed.AddListener(onClick);
            button.interactable = true;
            return button;
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

            RectTransform reference = template != null ? template.transform as RectTransform : null;
            if (reference == null)
            {
                reference = FindBottomButton(root);
            }

            if (reference == null)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(FallbackWidth, FallbackHeight);
                rect.anchoredPosition = new Vector2(0f, 12f);
                return;
            }

            rect.anchorMin = reference.anchorMin;
            rect.anchorMax = reference.anchorMax;
            rect.pivot = reference.pivot;
            if (template != null)
            {
                rect.sizeDelta = reference.sizeDelta;
            }

            float referenceBottom = reference.anchoredPosition.y - reference.pivot.y * reference.sizeDelta.y;
            rect.anchoredPosition = new Vector2(
                reference.anchoredPosition.x,
                referenceBottom - Gap - (1f - rect.pivot.y) * rect.sizeDelta.y);
        }

        private static RectTransform FindBottomButton(Transform root)
        {
            RectTransform lowest = null;
            float lowestEdge = float.MaxValue;

            foreach (MenuButton button in root.GetComponentsInChildren<MenuButton>(true))
            {
                if (button.GetComponent<PromptOptionMarker>() != null)
                {
                    continue;
                }

                RectTransform rect = button.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                float edge = rect.anchoredPosition.y - rect.pivot.y * rect.sizeDelta.y;
                if (edge < lowestEdge)
                {
                    lowestEdge = edge;
                    lowest = rect;
                }
            }

            return lowest;
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
