using UnityEngine;

namespace Vampirizm.Abilities.Orb
{
    internal enum BatState { Launch, Circle, Seek, Orbit, Dive, Return, Leave }

    // Per-bat state on a summoned bat. Only the caster's client (the ZDO owner) steers it through BatSwarm;
    // other clients see vanilla position and animation sync.
    internal sealed class SwarmBat : MonoBehaviour
    {
        // A bat whose swarm stopped updating it (caster gone, swarm lost) despawns on its own.
        private const float OrphanTimeout = 3f;
        private ZNetView _view;
        private float _lastClaim;

        internal Character Character { get; private set; }
        internal BatState State { get; private set; }
        internal float StateTime => Time.time - _stateStart;
        private float _stateStart;
        internal int Index;
        internal float Phase;
        internal float Radius;
        internal float Height;
        internal float Spin;
        internal float NextDive;
        internal float AbsorbReadyAt;
        internal Character DiveTarget;
        internal Vector3 Heading;

        internal bool Alive => Character != null && !Character.IsDead() && _view != null && _view.IsValid();
        internal bool Leaving => State == BatState.Return || State == BatState.Leave;
        internal Vector3 Position => Character != null ? Character.GetCenterPoint() : transform.position;

        private void Awake()
        {
            Character = GetComponent<Character>();
            _view = GetComponent<ZNetView>();
            _lastClaim = Time.time;
        }

        // Runs on every client: each client simulates its own player's body, so each must drop bat-player contacts.
        private void Start()
        {
            Collider[] own = GetComponentsInChildren<Collider>(true);
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                foreach (Collider other in player.GetComponentsInChildren<Collider>(true))
                foreach (Collider collider in own)
                    Physics.IgnoreCollision(collider, other, true);
            }
        }

        private void Update()
        {
            if (Time.time - _lastClaim > OrphanTimeout && _view != null && _view.IsValid() && _view.IsOwner())
                Despawn(true);
        }

        internal void Claim() => _lastClaim = Time.time;

        internal void SetState(BatState state)
        {
            State = state;
            _stateStart = Time.time;
        }

        // Vanilla flying applies moveDir * fly speed, so the requested velocity is scaled into a unit move direction.
        internal void Steer(Vector3 velocity)
        {
            if (Character == null) return;
            float max = Mathf.Max(0.1f, Character.m_flyFastSpeed);
            Vector3 move = velocity / max;
            if (move.sqrMagnitude > 1f) move.Normalize();
            Character.SetRun(true);
            Character.SetMoveDir(move);
            if (velocity.sqrMagnitude > 0.01f) Character.SetLookDir(velocity.normalized);
        }

        internal void Despawn(bool effect)
        {
            if (effect) OrbEffects.Poof(Position);
            if (_view != null && _view.IsValid() && _view.IsOwner() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
