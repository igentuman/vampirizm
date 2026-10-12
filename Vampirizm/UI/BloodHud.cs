using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vampirizm.Core;

namespace Vampirizm.UI
{
    // Lives on the Hud object, so it is rebuilt with the HUD and never exists on a dedicated server.
    internal sealed class BloodHud : MonoBehaviour
    {
        private const float Gap = 6f;
        // Power names such as "The Queen" overflow the slot, so the bar keeps extra distance from it.
        private const float PowerGap = 14f;
        // The measured health bar root sits above the visible bar art; this lines the bottoms up.
        private const float BottomNudge = -18f;
        // Drop center below the bar bottom, as a fraction of bar width, level with the vanilla heart icon.
        private const float IconDrop = 0.45f;
        private const float Inset = 2f;
        private const float IconScale = 0.8f;
        private const int IconResolution = 64;
        private const float HungryFraction = 0.2f;
        private static readonly Color FillColor = new Color(0.62f, 0.04f, 0.06f, 0.95f);
        private static readonly Color HungryColor = new Color(1f, 0.15f, 0.1f, 1f);
        private static readonly Vector3[] Corners = new Vector3[4];
        private static Sprite _dropSprite;

        private Hud _hud;
        private RectTransform _root;
        private RectTransform _fill;
        private Image _fillImage;
        private TextMeshProUGUI _text;
        private RectTransform _icon;
        private int _shownBlood = -1;

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
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0f, 0f);
            var background = _root.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;

            _fill = CreateRect("Fill", _root);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = new Vector2(Inset, Inset);
            _fill.offsetMax = new Vector2(-Inset, -Inset);
            _fillImage = _fill.gameObject.AddComponent<Image>();
            _fillImage.color = FillColor;
            _fillImage.raycastTarget = false;

            RectTransform label = CreateRect("Value", _root);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            _text = label.gameObject.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.raycastTarget = false;

            _icon = CreateRect("Drop", _root);
            _icon.anchorMin = _icon.anchorMax = new Vector2(0.5f, 0f);
            _icon.pivot = new Vector2(0.5f, 0.5f);
            var icon = _icon.gameObject.AddComponent<Image>();
            icon.sprite = DropSprite();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
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
            bool visible = controller != null && controller.IsActiveVampire && _hud.m_healthBarRoot != null;
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            // The vanilla health bar is a horizontal GuiBar rotated upright and resized with max health,
            // so its on-screen bounds are measured every frame instead of reading sizeDelta.
            var parent = (RectTransform)_root.parent;
            Bounds(_hud.m_healthBarRoot, parent, out Vector2 min, out Vector2 max);
            // The forsaken power slot sits right of the health bar. It is measured even while hidden,
            // so the blood bar does not jump when a power is selected or cleared.
            float left = max.x + Gap;
            if (_hud.m_gpRoot != null)
            {
                Bounds(_hud.m_gpRoot, parent, out _, out Vector2 powerMax);
                left = Mathf.Max(left, powerMax.x + PowerGap);
            }

            var settings = Plugin.Instance.Settings;
            Vector2 size = max - min;
            _root.sizeDelta = new Vector2(size.x + 1f, size.y);
            // Anchored to the parent's center, while the measured bounds are relative to its pivot.
            _root.anchoredPosition = new Vector2(left + settings.HudOffsetX.Value, min.y + BottomNudge + settings.HudOffsetY.Value)
                - parent.rect.center;
            _icon.sizeDelta = new Vector2(size.x * IconScale, size.x * IconScale);
            _icon.anchoredPosition = new Vector2(0f, -size.x * IconDrop);

            if (_hud.m_healthText != null)
            {
                if (_text.font != _hud.m_healthText.font) _text.font = _hud.m_healthText.font;
                if (_text.fontSharedMaterial != _hud.m_healthText.fontSharedMaterial)
                    _text.fontSharedMaterial = _hud.m_healthText.fontSharedMaterial;
                if (_text.fontSize != _hud.m_healthText.fontSize) _text.fontSize = _hud.m_healthText.fontSize;
                if (_text.fontStyle != _hud.m_healthText.fontStyle) _text.fontStyle = _hud.m_healthText.fontStyle;
                if (_text.color != _hud.m_healthText.color) _text.color = _hud.m_healthText.color;
            }

            float blood = controller.State.Blood;
            float capacity = controller.Capacity;
            float fraction = capacity > 0f ? Mathf.Clamp01(blood / capacity) : 0f;
            _fill.anchorMax = new Vector2(1f, fraction);
            // The inset would give a negative height near zero.
            _fillImage.enabled = fraction * size.y > Inset * 2f;
            _fillImage.color = fraction < HungryFraction
                ? Color.Lerp(FillColor, HungryColor, 0.5f + Mathf.Sin(Time.time * 6f) * 0.5f)
                : FillColor;
            int shownBlood = Mathf.CeilToInt(blood);
            if (shownBlood != _shownBlood)
            {
                _shownBlood = shownBlood;
                _text.text = shownBlood.ToString();
            }
        }

        private static void Bounds(RectTransform rect, Transform space, out Vector2 min, out Vector2 max)
        {
            rect.GetWorldCorners(Corners);
            min = max = space.InverseTransformPoint(Corners[0]);
            for (int i = 1; i < Corners.Length; i++)
            {
                Vector2 corner = space.InverseTransformPoint(Corners[i]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }

        private static Sprite DropSprite()
        {
            if (_dropSprite != null) return _dropSprite;
            const int samples = 4;
            const float centerY = 0.36f, radius = 0.3f, tipY = 0.96f;
            float distance = tipY - centerY;
            float tangentY = centerY + radius * radius / distance;
            float slope = radius / Mathf.Sqrt(distance * distance - radius * radius);
            var light = new Color(0.95f, 0.25f, 0.25f);
            var dark = new Color(0.45f, 0.01f, 0.03f);

            var texture = new Texture2D(IconResolution, IconResolution, TextureFormat.RGBA32, false)
            {
                name = "vampirizm_blood_drop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[IconResolution * IconResolution];
            for (int py = 0; py < IconResolution; py++)
            for (int px = 0; px < IconResolution; px++)
            {
                int covered = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / samples) / IconResolution - 0.5f;
                    float y = (py + (sy + 0.5f) / samples) / IconResolution;
                    bool inCircle = x * x + (y - centerY) * (y - centerY) <= radius * radius;
                    bool inTip = y >= tangentY && y <= tipY && Mathf.Abs(x) <= (tipY - y) * slope;
                    if (inCircle || inTip) covered++;
                }

                float u = (px + 0.5f) / IconResolution - 0.5f;
                float v = (py + 0.5f) / IconResolution;
                // Light falls from the upper left; a small specular spot sells the liquid look.
                float shade = Mathf.Clamp01(0.55f - u * 0.9f + (v - centerY) * 0.6f);
                Color color = Color.Lerp(dark, light, shade);
                float spot = new Vector2(u + 0.11f, v - 0.42f).magnitude;
                color = Color.Lerp(color, Color.white, Mathf.Clamp01((0.07f - spot) / 0.03f) * 0.8f);
                color.a = (float)covered / (samples * samples);
                pixels[py * IconResolution + px] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _dropSprite = Sprite.Create(texture, new Rect(0f, 0f, IconResolution, IconResolution), new Vector2(0.5f, 0.5f));
            _dropSprite.name = texture.name;
            return _dropSprite;
        }
    }
}
