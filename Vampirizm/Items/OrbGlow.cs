using UnityEngine;

namespace Vampirizm.Items
{
    // Slow spin and light pulse of the orb model. A dropped orb (inside an ItemDrop) also bobs gently.
    internal sealed class OrbGlow : MonoBehaviour
    {
        [SerializeField] private Transform _orb;
        [SerializeField] private Light _light;
        private Vector3 _rest;
        private bool _dropped;
        private float _phase;

        internal void Setup(Transform orb, Light light)
        {
            _orb = orb;
            _light = light;
        }

        private void Start()
        {
            if (_orb != null) _rest = _orb.localPosition;
            _dropped = GetComponentInParent<ItemDrop>() != null;
            _phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            float t = Time.time + _phase;
            if (_light != null) _light.intensity = 1.5f + 0.4f * Mathf.Sin(t * 2.2f);
            if (_orb == null) return;
            _orb.Rotate(0f, 25f * Time.deltaTime, 0f, Space.Self);
            if (_dropped) _orb.localPosition = _rest + Vector3.up * (0.03f * Mathf.Sin(t * 1.6f));
        }
    }
}
