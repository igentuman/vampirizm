using UnityEngine;
using Vampirizm.Combat;

namespace Vampirizm.Abilities.Orb
{
    // Swarm target selection: the aimed cone for a cast, nearest enemy for retargeting and shield guard dives.
    internal static class OrbTargeting
    {
        private static readonly int SightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker");

        // Never tamed creatures, friendly-listed creatures, swarm bats or the caster; players only with PvP on both sides.
        internal static bool IsValid(Player caster, Character target)
        {
            if (caster == null || target == null || target == caster || target.IsDead()) return false;
            if (target.GetComponent<SwarmBat>() != null || target.IsTamed()) return false;
            if (target is Player player) return caster.IsPVPEnabled() && player.IsPVPEnabled();
            if (FriendlyRegistry.IsListed(target) || !BaseAI.IsEnemy(caster, target)) return false;
            return !target.IsBoss() || Plugin.Instance.Settings.OrbTargetBosses.Value;
        }

        // Smallest angle to the aim direction wins; distance breaks near ties.
        internal static Character FindInCone(Player caster, Vector3 origin, Vector3 direction, float range, float cone)
        {
            Character best = null;
            float bestAngle = float.MaxValue, bestDistance = float.MaxValue;
            foreach (Character candidate in Character.GetAllCharacters())
            {
                if (!IsValid(caster, candidate)) continue;
                Vector3 to = candidate.GetCenterPoint() - origin;
                float distance = to.magnitude;
                if (distance > range || distance < 0.01f) continue;
                float angle = Vector3.Angle(direction, to);
                if (!OrbRules.InCone(angle, cone) || !InSight(origin, candidate)) continue;
                bool better = angle < bestAngle - 0.5f || (angle <= bestAngle + 0.5f && distance < bestDistance);
                if (!better) continue;
                best = candidate;
                bestAngle = angle;
                bestDistance = distance;
            }
            return best;
        }

        internal static Character Nearest(Player caster, Vector3 point, float radius, bool hostileOnly)
        {
            Character best = null;
            float bestDistance = radius * radius;
            foreach (Character candidate in Character.GetAllCharacters())
            {
                if (!IsValid(caster, candidate) || (hostileOnly && !IsHostile(caster, candidate))) continue;
                float distance = (candidate.GetCenterPoint() - point).sqrMagnitude;
                if (distance > bestDistance || !InSight(point, candidate)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        // Shield bats only dive at creatures that are actually in a fight, not at grazing deer.
        private static bool IsHostile(Player caster, Character target)
        {
            if (target is Player) return true;
            BaseAI ai = target.GetBaseAI();
            return ai is MonsterAI && BaseAI.IsEnemy(target, caster) && (ai.IsAlerted() || ai.HaveTarget());
        }

        private static bool InSight(Vector3 origin, Character target)
            => !Physics.Linecast(origin, target.GetCenterPoint(), SightMask, QueryTriggerInteraction.Ignore);
    }
}
