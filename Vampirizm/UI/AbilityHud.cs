using System.IO;
using Jotunn.Utils;
using UnityEngine;
using UnityEngine.UI;
using Vampirizm.Abilities;
using Vampirizm.Core;

namespace Vampirizm.UI
{
    // A child of the vanilla HUD root: follows UI scaling and the game's hide-HUD control.
    internal sealed class AbilityHud : MonoBehaviour
    {
        private const float IconSize = 32f;
        private const float Gap = 8f;
        private Hud _hud;
        private RectTransform _root;
        private Slot _dash;
        private Slot _invisibility;
        private Slot _bat;
        private Slot _rage;

        private void Awake()
        {
            _hud = GetComponent<Hud>();
            Build();
        }

        private void Build()
        {
            if (_hud.m_rootObject == null) return;
            _root = CreateRect("VampirizmAbilities", _hud.m_rootObject.transform);
            _root.anchoredPosition = new Vector2(24f, 22f);
            _root.sizeDelta = new Vector2(IconSize * 4f + Gap * 3f, IconSize);
            _dash = new Slot("dash", _root);
            _invisibility = new Slot("invisibility", _root);
            _bat = new Slot("bat", _root);
            _rage = new Slot("bloodrage_gui", _root);
            _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // Some HUD load paths assign references after Awake.
            if (_root == null) Build();
            if (_root == null) return;
            VampireController controller = VampireController.Local;
            bool visible = controller != null && controller.IsActiveVampire && controller.CanChange;
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            var settings = Plugin.Instance.Settings;
            float level = controller.Level;
            int index = 0;
            _dash.Update(AbilityRules.IsUnlocked(level, settings.DashLevel.Value), controller.Dash.OnCooldown, ref index);
            _invisibility.Update(AbilityRules.IsUnlocked(level, settings.InvisibilityLevel.Value), controller.Invisibility.OnCooldown, ref index);
            _rage.Update(AbilityRules.IsUnlocked(level, settings.BloodRageLevel.Value), controller.BloodRage.OnCooldown, ref index);
            _bat.Update(AbilityRules.IsUnlocked(level, settings.BatFormLevel.Value), controller.BatForm.OnCooldown, ref index);
        }

        private void OnDestroy()
        {
            _dash?.Destroy();
            _invisibility?.Destroy();
            _bat?.Destroy();
            _rage?.Destroy();
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            return rect;
        }

        private sealed class Slot
        {
            private readonly RectTransform _rect;
            private readonly Image _image;
            private readonly Sprite _ready;
            private readonly Sprite _cooldown;

            internal Slot(string name, Transform parent)
            {
                _rect = CreateRect(name, parent);
                _rect.sizeDelta = new Vector2(IconSize, IconSize);
                _image = _rect.gameObject.AddComponent<Image>();
                _image.preserveAspect = true;
                _image.raycastTarget = false;

                using (Stream stream = typeof(AbilityHud).Assembly.GetManifestResourceStream($"Vampirizm.Assets.{name}.png"))
                using (var buffer = new MemoryStream())
                {
                    if (stream == null)
                    {
                        Plugin.Warn($"Ability icon resource is missing: {name}.");
                        _image.enabled = false;
                        return;
                    }
                    stream.CopyTo(buffer);
                    Texture2D texture = AssetUtils.LoadImage(buffer.ToArray());
                    if (texture == null)
                    {
                        _image.enabled = false;
                        Plugin.Warn($"Could not load ability icon: {name}.");
                        return;
                    }
                    _ready = CreateSprite(texture, name);

                    // Multiplying a colored icon by gray only darkens it. Desaturate once,
                    // retaining alpha, so the cooldown artwork is genuinely grayscale.
                    Color[] pixels = texture.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float gray = pixels[i].grayscale * 0.65f;
                        pixels[i] = new Color(gray, gray, gray, pixels[i].a);
                    }
                    var grayTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                    grayTexture.SetPixels(pixels);
                    grayTexture.Apply(false, true);
                    _cooldown = CreateSprite(grayTexture, name + "_cooldown");
                    texture.Apply(false, true);
                }
                _image.sprite = _ready;
            }

            internal void Update(bool unlocked, bool onCooldown, ref int index)
            {
                if (_rect.gameObject.activeSelf != unlocked) _rect.gameObject.SetActive(unlocked);
                if (!unlocked) return;
                _rect.anchoredPosition = new Vector2(index++ * (IconSize + Gap), 0f);
                Sprite sprite = onCooldown ? _cooldown : _ready;
                if (_image.sprite != sprite) _image.sprite = sprite;
            }

            internal void Destroy()
            {
                DestroySprite(_ready);
                DestroySprite(_cooldown);
            }

            private static Sprite CreateSprite(Texture2D texture, string name)
            {
                texture.name = "vampirizm_" + name;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = texture.name;
                return sprite;
            }

            private static void DestroySprite(Sprite sprite)
            {
                if (sprite == null) return;
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
    }
}
