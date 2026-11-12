using UnityEngine;

namespace ArrowBlaster
{
    // Objetivo del lanzamiento: diana colgada de una cuerda, que sube y baja.
    // Atravesar el centro = PERFECT; el resto de la diana = GOOD.
    public sealed class Lantern : MonoBehaviour
    {
        private ArrowConfig _cfg;
        private Transform _body;
        private LineRenderer _rope;
        private float _time, _speedMultiplier = 1f, _swing, _swingVel, _punch;
        private bool _hit;

        public float Y => transform.position.y;
        public float X => transform.position.x;

        public static Lantern Create(Transform parent, ArrowConfig cfg, float speedMultiplier)
        {
            var go = new GameObject("Lantern");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Lantern>();
            l._cfg = cfg;
            l._speedMultiplier = speedMultiplier;
            l._time = Random.value;
            l.Build();
            l.Update();
            return l;
        }

        private void Build()
        {
            float r = _cfg.lanternRadius;
            Color red = new Color32(0xC8, 0x10, 0x2E, 0xFF);

            // Cuerda: una línea de tinta
            _rope = new GameObject("Rope").AddComponent<LineRenderer>();
            _rope.transform.SetParent(transform.parent, false);
            _rope.positionCount = 2;
            _rope.widthMultiplier = 0.045f;
            _rope.startColor = _rope.endColor = _cfg.ink;
            _rope.sortingOrder = 2;
            InkArt.Apply(_rope);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // Objetivo: diana de Kenney (Shooting Gallery «target_red1»); conserva el rojo de los anillos.
            // La diana ocupa todo el sprite: su radio exterior coincide con lanternRadius.
            KenneyArt.Shape("target_red1", _body, Vector2.zero, r * 2f, _cfg.ink, 5, keepColor: true);
        }

        // El acierto hace que el farolillo se balancee y dé un pequeño bote
        public void Hit(bool perfect)
        {
            _hit = true;
            _swingVel += perfect ? -260f : -150f;
            _punch = 1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!_hit) _time += dt * _cfg.lanternSpeed * _speedMultiplier;
            float y = _cfg.lanternMidY + Mathf.Sin(_time * Mathf.PI * 2f) * _cfg.lanternTravel;
            transform.position = new Vector3(_cfg.lanternX, y, 0f);

            // Balanceo amortiguado alrededor del punto de la cuerda
            _swingVel += (-_swing * 60f - _swingVel * 4f) * dt;
            _swing += _swingVel * dt;
            _body.localRotation = Quaternion.Euler(0f, 0f, _swing);
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 3f);
            float k = Mathf.Sin(_punch * Mathf.PI * 3f) * _punch * 0.15f;
            _body.localScale = new Vector3(1f + k, 1f - k, 1f);

            float top = _cfg.cameraSize + 1.5f;
            _rope.SetPosition(0, new Vector3(_cfg.lanternX, top, 0f));
            _rope.SetPosition(1, new Vector3(_cfg.lanternX, y + _cfg.lanternRadius * 1.1f, 0f));
        }
    }
}
