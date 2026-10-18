using UnityEngine;

namespace Vampirizm.Building
{
    // The vanilla Bed handles claiming, respawn, shelter, enemies, and sleep eligibility.
    // This component marks our variants and animates their optional lid on every client.
    public sealed class CoffinBed : MonoBehaviour
    {
        private const float CloseDelay = 2f;
        public int Blood;
        public Transform Lid;
        private ZNetView _view;
        private bool _occupied;
        private float _occupiedSince;
        private float _nextPoll;
        private float _angle = 105f;

        private void Awake() => _view = GetComponent<ZNetView>();

        private void Update()
        {
            if (Lid == null) return;
            if (Time.time >= _nextPoll)
            {
                _nextPoll = Time.time + 0.2f;
                bool occupied = IsOccupied();
                if (occupied != _occupied)
                {
                    _occupied = occupied;
                    if (_occupied) _occupiedSince = Time.time;
                }
            }
            bool shouldClose = _occupied && Time.time >= _occupiedSince + CloseDelay;
            _angle = Mathf.MoveTowards(_angle, shouldClose ? 0f : 105f, Time.deltaTime * 100f);
            Lid.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        private bool IsOccupied()
        {
            if (_view == null || !_view.IsValid()) return false;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead() || !player.InBed()) continue;
                if (player == Player.m_localPlayer)
                {
                    if (AttachedTo(player) == this) return true;
                    continue;
                }
                // The vanilla attachment connection identifies the exact bed, even if two are adjacent.
                var view = player.GetComponent<ZNetView>();
                if (view != null && view.IsValid() &&
                    view.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform) == _view.GetZDO().m_uid)
                    return true;
            }
            return false;
        }

        internal static CoffinBed AttachedTo(Player player)
        {
            Transform point = player.GetAttachPoint();
            return point != null ? point.GetComponentInParent<CoffinBed>() : null;
        }
    }
}
