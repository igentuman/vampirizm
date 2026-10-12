using UnityEngine;
using Vampirizm.Abilities;
using Vampirizm.Controls;
using Vampirizm.Effects;
using Vampirizm.Feeding;
using Vampirizm.Networking;
using Vampirizm.Persistence;
using Vampirizm.Skills;

namespace Vampirizm.Core
{
    public sealed class VampireController : MonoBehaviour
    {
        private Player _player;
        private ZNetView _view;
        private bool _loaded;
        private bool _preserveSave;
        private float _refreshTimer;
        private float _regenTimer;
        private readonly SunlightController _sun = new SunlightController();
        private readonly SilverContactController _silver = new SilverContactController();
        internal VampireState State { get; private set; } = new VampireState();
        internal DrainAbility Drain { get; private set; }
        internal DrainVisual DrainVisual { get; private set; }
        internal DashAbility Dash { get; private set; }
        internal InvisibilityAbility Invisibility { get; private set; }
        internal BatFormAbility BatForm { get; private set; }
        internal bool CanChange => _loaded && !_preserveSave && IsLocalOwner && !_player.IsDead();
        // Vampire rules (food ban, blood stats, HUD) apply only to the loaded local character.
        internal bool IsActiveVampire => _loaded && State.IsVampire && IsLocalOwner && Plugin.Instance != null;
        internal float Level => VampirismSkill.Level(_player);
        internal float Capacity => Plugin.Instance.Settings.Capacity(Level);
        internal bool SunExposed => IsActiveVampire && _sun.Exposed;
        internal static VampireController Local
            => Player.m_localPlayer != null ? Player.m_localPlayer.GetComponent<VampireController>() : null;
        private bool IsLocalOwner => _player == Player.m_localPlayer && _view != null &&
            _view.IsValid() && _view.IsOwner();

        internal static VampireController Ensure(Player player)
            => player.GetComponent<VampireController>() ?? player.gameObject.AddComponent<VampireController>();

        private void Awake()
        {
            _player = GetComponent<Player>();
            _view = GetComponent<ZNetView>();
            Drain = new DrainAbility(_player, this);
            DrainVisual = new DrainVisual(_player);
            Dash = new DashAbility(_player, this);
            Invisibility = new InvisibilityAbility(_player, this);
            BatForm = new BatFormAbility(_player, this);
        }

        private void OnDisable()
        {
            Invisibility?.End(false);
        }

        private void OnDestroy()
        {
            Drain?.End();
            Dash?.End();
            Invisibility?.End(false);
            BatForm?.End(false);
        }

        internal void LoadState()
        {
            SaveReadResult result = VampireSaveData.Read(_player.m_customData, out VampireState state);
            State = state;
            _preserveSave = result == SaveReadResult.Invalid || result == SaveReadResult.UnsupportedVersion;
            _loaded = true;
            if (IsLocalOwner)
            {
                VampireNetwork.SetBatForm(_view, false);
                VampireNetwork.SetDrinkTarget(_view, ZDOID.None);
                Invisibility.End();
            }
            if (_preserveSave) Plugin.Warn($"Vampirism save: {result}. Original data preserved; changes disabled for this character.");
            // Do not write here: Load can precede SetLocalPlayer and server config synchronization.
        }

        private void Update()
        {
            if (Plugin.Instance == null) return;
            if (!IsLocalOwner)
            {
                Invisibility.UpdateRemote();
                BatForm.Update(Time.deltaTime);
                DrainVisual.Update();
                return;
            }
            if (!_loaded) LoadState();
            Drain.Update();
            DrainVisual.Update();
            Dash.Update();
            Invisibility.UpdateLocal(Time.unscaledDeltaTime);
            BatForm.Update(Time.deltaTime);
            if (IsActiveVampire) ReadActions();
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 1f;
            SaveState();
        }

        private void LateUpdate()
        {
            // Recover any interrupted camera render; transparency itself is camera-scoped.
            Invisibility?.UpdateVisual();
        }

        // Ordinary characters keep their keys for anything else: vampire actions are read only for an active vampire.
        private void ReadActions()
        {
            if (VampireInput.Pressed(_player, VampireAction.Dash)) Dash.TryStart();
            if (VampireInput.Pressed(_player, VampireAction.Invisibility))
                Invisibility.Toggle();
            if (VampireInput.Pressed(_player, VampireAction.BatForm)) BatForm.Toggle();
        }

        internal void SaveState()
        {
            if (!_loaded || _preserveSave || !IsLocalOwner || Plugin.Instance == null) return;
            // Ordinary characters do not get a new skill record or custom save entry.
            if (State.IsVampire) State.Normalize(Capacity);
            if (State.IsVampire || _player.m_customData.ContainsKey(VampireSaveData.Key))
                _player.m_customData[VampireSaveData.Key] = VampireSaveData.Serialize(State);
            VampireNetwork.Publish(_view, State, State.IsVampire ? Level : 0f);
        }

        internal bool Convert()
        {
            if (!CanChange || State.IsVampire) return false;
            if (Level < 1f) VampirismSkill.SetLevel(_player, 1f);
            State.Convert(Capacity, Plugin.Instance.Settings.InitialBloodFraction.Value);
            _player.ClearFood();
            SaveState();
            return true;
        }

        internal bool Cure()
        {
            if (!CanChange || !State.IsVampire) return false;
            State.Cure();
            Drain.End();
            Dash.End();
            Invisibility.End();
            BatForm.End(false);
            SlimeProtection.Remove(_player);
            _sun.Reset();
            _silver.Reset();
            // Preserve the vanilla skill until the cure progression policy is decided.
            SaveState();
            return true;
        }

        internal bool SpendBlood(float amount)
        {
            if (!CanChange || !State.IsVampire) return false;
            // A free ability is allowed; TrySpend rejects zero.
            if (amount <= 0f) return true;
            if (!State.TrySpend(amount)) return false;
            SaveState();
            return true;
        }

        internal float AddBlood(float amount)
        {
            if (!CanChange) return 0f;
            float accepted = State.AddBlood(amount, Capacity);
            SaveState();
            return accepted;
        }

        // Called before vanilla Player.OnDeath marks the character dead, so CanChange still holds.
        internal void OnDeath()
        {
            if (!IsActiveVampire || !CanChange) return;
            State.LoseOnDeath(Plugin.Instance.Settings.DeathBloodKept.Value);
            SaveState();
        }

        // Called from the vanilla food tick: owner only, alive, not in intro or teleport.
        internal void TickFood(float dt)
        {
            if (!IsActiveVampire || !CanChange)
            {
                _sun.Reset();
                _silver.Reset();
                return;
            }
            SlimeProtection.Tick(_player);
            _sun.Tick(_player, dt);
            _silver.Tick(_player, dt);
            var settings = Plugin.Instance.Settings;
            TickVampireFood(dt, settings.FoodBurnMultiplier.Value);
            State.Drain(BloodRules.PassiveDrain(settings.DrainPerMinute.Value, dt, Game.m_foodRate));
            _regenTimer += dt;
            if (_regenTimer < 10f) return;
            _regenTimer = 0f;
            float heal = BloodRules.StatBonus(State.Blood, Capacity, settings.RegenPerBlood.Value);
            if (heal <= 0f) return;
            float multiplier = 1f;
            _player.GetSEMan().ModifyHealthRegen(ref multiplier);
            _player.Heal(heal * multiplier);
        }

        // Vanilla UpdateFood takes one second off every food per world-rate second; the vampire burns the rest of the
        // multiplier here, and vanilla still removes finished foods.
        private void TickVampireFood(float dt, float burnMultiplier)
        {
            VampireFood.Rewrap(_player);
            var foods = _player.GetFoods();
            float extra = dt * Game.m_foodRate * (burnMultiplier - 1f);
            if (extra <= 0f) return;
            foreach (Player.Food food in foods) food.m_time -= extra;
        }

        // Blood is added on top of the vanilla base and food values.
        internal void ApplyFoodValues(ref float hp, ref float stamina, ref float eitr)
        {
            if (!IsActiveVampire) return;
            var settings = Plugin.Instance.Settings;
            float capacity = Capacity;
            hp += BloodRules.StatBonus(State.Blood, capacity, settings.HealthPerBlood.Value);
            stamina += BloodRules.StatBonus(State.Blood, capacity, settings.StaminaPerBlood.Value);
            eitr += settings.MaxEitr(Level);
        }

        // Runs inside the vanilla stats tick, right before eitr regen; EitrPatches restores the vanilla base afterwards.
        internal void ApplyEitrRegen()
        {
            if (!IsActiveVampire) return;
            float regen = Plugin.Instance.Settings.EitrRegen(Level);
            _player.m_eiterRegen = EitrRules.VanillaRegenBase(regen, _player.GetEitr(), _player.GetMaxEitr());
        }

        internal bool SetLevel(float level)
        {
            if (!CanChange || !State.IsVampire) return false;
            VampirismSkill.SetLevel(_player, level);
            SaveState();
            return true;
        }
    }
}
