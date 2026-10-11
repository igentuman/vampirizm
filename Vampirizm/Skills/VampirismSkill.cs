using Jotunn.Configs;
using Jotunn.Managers;
using Vampirizm.Core;

namespace Vampirizm.Skills
{
    internal static class VampirismSkill
    {
        internal const string Identifier = "com.igentuman.vampirizm.skill";
        internal static global::Skills.SkillType Type { get; private set; }

        internal static void Register()
        {
            Type = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = Identifier,
                Name = "$vampirizm_skill",
                Description = "$vampirizm_skill_description",
                IncreaseStep = 1f
            });
        }

        internal static float Level(Player player) => player.GetSkills().GetSkillLevel(Type);

        internal static void SetLevel(Player player, float level)
        {
            // GetSkillLevel ensures the vanilla skill record exists, even at level zero.
            Level(player);
            foreach (global::Skills.Skill skill in player.GetSkills().GetSkillList())
            {
                if (skill.m_info.m_skill != Type) continue;
                skill.m_level = BloodRules.Clamp(level, 100f);
                skill.m_accumulator = 0f;
                return;
            }
        }
    }
}
