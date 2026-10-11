using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vampirizm.Core;

namespace Vampirizm.UI
{
    // Lives on the Hud object, so it is rebuilt with the HUD and never exists on a dedicated server.
    internal sealed class BloodHud : MonoBehaviour
    {
        private const float Height = 20f;
        private const float HungryFraction = 0.2f;
        private static readonly Color FillColor = new Color(0.62f, 0.04f, 0.06f, 0.95f);
        private static readonly Color HungryColor = new Color(1f, 0.15f, 0.1f, 1f);

        private Hud _hud;
        private RectTransform _root;
        private RectTransform _fill;
        private Image _fillImage;
        private TextMeshProUGUI _text;
        private float _shownBlood = -1f;
        private float _shownCapacity = -1f;
        private int _shownLevel = -1;
        private bool _shownSun;

        private void Awake()
        {
            _hud = GetComponent<Hud>();
            Build();
        }

        private void Build()
        {
            RectTransform parent = _hud.m_healthPanel != null ? _hud.m_healthPanel
                : _hud.m_healthBarRoot != null ? _hud.m_healthBarRoot.parent as RectTransform : null;
            if (parent == null) return;
            _root = CreateRect("VampirizmBloodBar", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 0f);
            var background = _root.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;

            _fill = CreateRect("Fill", _root);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = new Vector2(2f, 2f);
            _fill.offsetMax = new Vector2(-2f, -2f);
            _fillImage = _fill.gameObject.AddComponent<Image>();
            _fillImage.color = FillColor;
            _fillImage.raycastTarget = false;

            RectTransform label = CreateRect("Text", _root);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            _text = label.gameObject.AddComponent<TextMeshProUGUI>();
            if (_hud.m_healthText != null) _text.font = _hud.m_healthText.font;
            _text.fontSize = 14f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = Color.white;
            _text.raycastTarget = false;
            _root.gameObject.SetActive(false);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private void LateUpdate()
        {
            // Hud.Awake may run before health panel references are assigned on some UI load paths.
            // Retry until the panel exists instead of permanently losing the blood bar.
            if (_root == null) Build();
            if (_root == null) return;
            var controller = VampireController.Local;
            bool visible = controller != null && controller.IsActiveVampire;
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            var settings = Plugin.Instance.Settings;
            _root.sizeDelta = new Vector2(settings.HudWidth.Value, Height);
            _root.anchoredPosition = new Vector2(settings.HudOffsetX.Value, settings.HudOffsetY.Value);

            float blood = controller.State.Blood;
            float capacity = controller.Capacity;
            float fraction = capacity > 0f ? Mathf.Clamp01(blood / capacity) : 0f;
            _fill.anchorMax = new Vector2(fraction, 1f);
            // The inset would give a negative width near zero.
            _fillImage.enabled = fraction * settings.HudWidth.Value > 4f;
            _fillImage.color = fraction < HungryFraction
                ? Color.Lerp(FillColor, HungryColor, 0.5f + Mathf.Sin(Time.time * 6f) * 0.5f)
                : FillColor;

            int level = Mathf.FloorToInt(controller.Level);
            float roundedBlood = Mathf.Floor(blood);
            bool sun = controller.SunExposed;
            if (roundedBlood == _shownBlood && capacity == _shownCapacity && level == _shownLevel && sun == _shownSun) return;
            _shownBlood = roundedBlood;
            _shownCapacity = capacity;
            _shownLevel = level;
            _shownSun = sun;
            string text = string.Format(CultureInfo.InvariantCulture,
                global::Localization.instance.Localize("$vampirizm_hud"), roundedBlood, Mathf.Floor(capacity), level);
            _text.text = sun ? text + global::Localization.instance.Localize("$vampirizm_hud_sun") : text;
        }
    }
}
