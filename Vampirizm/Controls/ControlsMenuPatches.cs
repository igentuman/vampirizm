using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace Vampirizm.Controls
{
    // Adds the vampire actions to Valheim's own Controls tab, so they use the native rebinding dialog.
    // Other mods (for example JustVault) add rows the same way; every step works on the current row count.
    [HarmonyPatch]
    internal static class ControlsMenuPatches
    {
        private static readonly AccessTools.FieldRef<KeyboardMouseSettings, List<KeySetting>> Keys =
            AccessTools.FieldRefAccess<KeyboardMouseSettings, List<KeySetting>>("m_keys");
        private static readonly AccessTools.FieldRef<KeyboardMouseSettings, int> Rows =
            AccessTools.FieldRefAccess<KeyboardMouseSettings, int>("m_keyRows");
        private static readonly AccessTools.FieldRef<KeyboardMouseSettings, int> Columns =
            AccessTools.FieldRefAccess<KeyboardMouseSettings, int>("m_keyCols");

        [HarmonyPostfix, HarmonyPatch(typeof(ZInput), nameof(ZInput.Save))]
        private static void AfterInputSave() => VampireInput.MirrorRebinds();

        [HarmonyPrefix, HarmonyPatch(typeof(KeyboardMouseSettings), nameof(KeyboardMouseSettings.Initialize))]
        private static void BeforeInitialize(KeyboardMouseSettings __instance)
        {
            List<KeySetting> keys = Keys(__instance);
            if (keys == null || keys.Count < 2) return;
            var missing = new List<VampireBinding>();
            foreach (VampireBinding binding in VampireInput.Bindings)
                if (!keys.Exists(key => key.m_keyName == binding.Button.Name)) missing.Add(binding);
            if (missing.Count == 0) return;

            // The grid fills columns top to bottom with ceil(count / columns) rows, in sibling order.
            int columns = Mathf.Max(1, Columns(__instance));
            int oldRows = Mathf.Clamp(Rows(__instance), 1, keys.Count);
            int newRows = Mathf.CeilToInt((float)(keys.Count + missing.Count) / columns);
            int intoFirst = Mathf.Clamp(newRows - oldRows, 0, missing.Count);

            KeySetting firstTemplate = keys[oldRows - 1];
            KeySetting firstPrevious = keys[Mathf.Max(0, oldRows - 2)];
            for (int i = 0; i < intoFirst; i++)
            {
                KeySetting row = CloneRow(firstTemplate, firstPrevious, missing[i], i + 1);
                if (row != null) keys.Insert(oldRows + i, row);
            }
            for (int i = intoFirst; i < missing.Count; i++)
            {
                KeySetting row = CloneRow(keys[keys.Count - 1], keys[keys.Count - 2], missing[i], 1, appendAtEnd: true);
                if (row != null) keys.Add(row);
            }
            Rows(__instance) = Mathf.CeilToInt((float)keys.Count / columns);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(KeyboardMouseSettings), nameof(KeyboardMouseSettings.Initialize))]
        private static void AfterInitialize(KeyboardMouseSettings __instance)
        {
            // The tab is initialized while inactive, where Unity cannot run its coroutines; the plugin can.
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(ScrollAfterLayout(__instance));
        }

        private static KeySetting CloneRow(KeySetting template, KeySetting previous, VampireBinding binding, int offset,
            bool appendAtEnd = false)
        {
            RectTransform templateRect = template.m_keyTransform;
            RectTransform previousRect = previous.m_keyTransform;
            if (templateRect == null || previousRect == null) return null;
            Vector2 step = templateRect.anchoredPosition - previousRect.anchoredPosition;
            GameObject clone = Object.Instantiate(templateRect.gameObject, templateRect.parent);
            clone.name = "VampirizmControl_" + binding.Action;
            var rect = (RectTransform)clone.transform;
            rect.anchoredPosition = templateRect.anchoredPosition + step * offset;
            // Layout follows sibling order, not m_keys order. Rows inserted into the first
            // column go beside that column's template; overflow rows extend the final column.
            if (appendAtEnd) rect.SetAsLastSibling();
            else rect.SetSiblingIndex(templateRect.GetSiblingIndex() + offset);

            Button button = clone.GetComponentInChildren<Button>(true);
            foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                if (button != null && text.transform.IsChildOf(button.transform)) continue;
                text.text = global::Localization.instance.Localize(binding.LabelToken);
                break;
            }
            return new KeySetting
            {
                m_keyName = binding.Button.Name,
                m_keyTransform = rect,
                m_blockedButtons = template.m_blockedButtons ?? new KeyCode[0]
            };
        }

        private static IEnumerator ScrollAfterLayout(KeyboardMouseSettings settings)
        {
            while (settings != null && !settings.gameObject.activeInHierarchy) yield return null;
            if (settings == null) yield break;
            yield return null;
            Canvas.ForceUpdateCanvases();
            MakeScrollable(settings);
            // ScrollRect recomputes its bounds in the post-layout pass; apply once more after it.
            yield return new WaitForEndOfFrame();
            if (settings != null) MakeScrollable(settings);
        }

        private static void MakeScrollable(KeyboardMouseSettings settings)
        {
            List<KeySetting> keys = Keys(settings);
            RectTransform first = keys != null && keys.Count > 0 ? keys[0].m_keyTransform : null;
            if (first == null) return;
            ScrollRect scroll = first.GetComponentInParent<ScrollRect>();
            RectTransform content = scroll != null ? scroll.content : first.parent as RectTransform;
            if (content == null) return;

            float rowHeight = 1f;
            float required = content.rect.height;
            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                rowHeight = Mathf.Max(1f, grid.cellSize.y + grid.spacing.y);
                int visible = 0;
                for (int i = 0; i < content.childCount; i++)
                {
                    Transform child = content.GetChild(i);
                    LayoutElement layout = child.GetComponent<LayoutElement>();
                    if (child.gameObject.activeSelf && (layout == null || !layout.ignoreLayout)) visible++;
                }
                int rows = Mathf.CeilToInt((float)visible / Mathf.Max(1, Columns(settings)));
                required = grid.padding.vertical + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
            }
            float viewport = scroll != null && scroll.viewport != null ? scroll.viewport.rect.height : content.rect.height;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(required, viewport + rowHeight));
            if (scroll == null) return;

            scroll.enabled = true;
            scroll.vertical = true;
            scroll.scrollSensitivity = Mathf.Max(scroll.scrollSensitivity, rowHeight);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            if (scroll.verticalScrollbar != null)
            {
                scroll.verticalScrollbar.transform.SetAsLastSibling();
                scroll.verticalScrollbar.gameObject.SetActive(true);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }
}
