using System.IO;
using Vampirizm.Abilities;
using Jotunn.Utils;
using UnityEngine;

namespace Vampirizm.Effects
{
    internal static class BloodRageEffect
    {
        internal const float Duration = BloodRageRules.Duration, Cost = BloodRageRules.Cost, SkillBonus = BloodRageRules.SkillBonus;
        internal const string EffectName = "VampirizmBloodRage";
        internal static readonly int EffectHash = EffectName.GetStableHashCode();
        private static StatusEffect _template;

        internal static bool IsActive(Player player) => player.GetSEMan().HaveStatusEffect(EffectHash);
        internal static void Remove(Player player) => player.GetSEMan().RemoveStatusEffect(EffectHash, quiet: true);
        internal static bool Apply(Player player)
        {
            if (_template == null)
            {
                _template = ScriptableObject.CreateInstance<StatusEffect>();
                _template.name = EffectName;
                _template.m_name = "$vampirizm_action_bloodrage";
                _template.m_tooltip = "$vampirizm_se_bloodrage_tooltip";
                _template.m_ttl = Duration;
                _template.hideFlags = HideFlags.HideAndDontSave;
                using (Stream stream = typeof(BloodRageEffect).Assembly.GetManifestResourceStream("Vampirizm.Assets.bloodrage.png"))
                using (var buffer = new MemoryStream())
                {
                    if (stream == null) { Clear(); return false; }
                    stream.CopyTo(buffer);
                    Texture2D texture = AssetUtils.LoadImage(buffer.ToArray());
                    _template.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }
            return player.GetSEMan().AddStatusEffect(_template, resetTime: true) != null;
        }

        internal static bool Buffs(global::Skills.SkillType skill)
            => skill == global::Skills.SkillType.Run || skill == global::Skills.SkillType.Jump ||
               skill == global::Skills.SkillType.Dodge || skill == global::Skills.SkillType.Blocking ||
               skill == global::Skills.SkillType.Unarmed || skill == global::Skills.SkillType.Swim ||
               skill == global::Skills.SkillType.Sneak;

        internal static void Clear()
        {
            if (_template == null) return;
            if (_template.m_icon != null)
            {
                Object.Destroy(_template.m_icon.texture);
                Object.Destroy(_template.m_icon);
            }
            Object.Destroy(_template);
            _template = null;
        }
    }
}
