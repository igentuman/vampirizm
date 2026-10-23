using Jotunn.Managers;
using UnityEngine;
using Vampirizm.Items;

namespace Vampirizm.Abilities.Orb
{
    // The summoned bat: a clone of the vanilla Bat creature that keeps Character (health, hits, death effects),
    // ZNetView, animator and flying body. It is a real networked character, so monsters can target and kill it.
    internal static class SwarmBatPrefab
    {
        // Stable prefab ID; bats are never saved (non-persistent ZDOs), but peers resolve the prefab by this name.
        internal const string PrefabName = "VampirizmSwarmBat";
        private const string BasePrefab = "Bat";
        private static GameObject _prefab;

        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += Create;

        // Health is read from config at every spawn; Character.Awake sets max health from the prefab value.
        internal static GameObject Get(float health)
        {
            if (_prefab == null) return null;
            Character character = _prefab.GetComponent<Character>();
            if (character != null) character.m_health = health;
            return _prefab;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            GameObject clone = PrefabManager.Instance.CreateClonedPrefab(PrefabName, BasePrefab);
            Character character = clone != null ? clone.GetComponent<Character>() : null;
            if (character == null)
            {
                Plugin.Warn($"Orb of Vampire: creature prefab {BasePrefab} is unavailable; the orb cannot summon bats.");
                if (clone != null) Object.DestroyImmediate(clone);
                return;
            }
            // Dependents first: Tameable and Procreation reference the AI.
            DestroyAll<Procreation>(clone);
            DestroyAll<Tameable>(clone);
            DestroyAll<CharacterDrop>(clone);
            DestroyAll<Growup>(clone);
            // The AI stays as a disabled component: vanilla EnemyHud and IsEnemy read GetBaseAI() without null
            // checks. Disabled, it never registers for AI updates and never wakes, hunts or attacks on its own.
            foreach (BaseAI ai in clone.GetComponents<BaseAI>())
            {
                if (ai is MonsterAI monster)
                {
                    monster.m_sleeping = false;
                    monster.m_enableHuntPlayer = false;
                }
                ai.m_spawnMessage = "";
                ai.enabled = false;
            }
            ZNetView view = clone.GetComponent<ZNetView>();
            if (view != null) view.m_persistent = false;
            // EnemyHud checks m_hideHud before creating a nameplate and health bar, regardless of the name.
            character.m_hideHud = true;
            character.m_name = "";
            character.m_faction = Character.Faction.Players;
            character.m_boss = false;
            character.m_flying = true;
            character.m_flySlowSpeed = 5f;
            character.m_flyFastSpeed = 14f;
            character.m_flyTurnSpeed = 30f;
            character.m_acceleration = 0.5f;
            character.m_health = Plugin.Instance.Settings.OrbBatHealth.Value;
            ItemVisuals.Tint(clone, new Color(1f, 0.55f, 0.55f));
            clone.AddComponent<SwarmBat>();
            PrefabManager.Instance.AddPrefab(clone);
            _prefab = clone;
        }

        private static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true)) Object.DestroyImmediate(component);
        }
    }
}
