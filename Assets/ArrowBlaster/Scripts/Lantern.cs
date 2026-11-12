using UnityEngine;

namespace ArrowBlaster
{
    // Objetivo del lanzamiento: farolillo rojo de papel (chōchin) colgado de una cuerda, que sube y baja.
    // Atravesar el emblema central = PERFECT; el resto del farolillo = GOOD.
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

            _rope = new GameObject("Rope").AddComponent<LineRenderer>();
            _rope.transform.SetParent(transform.parent, false);
            _rope.sharedMaterial = InkArt.SpriteMaterial;
            _rope.positionCount = 2;
            _rope.widthMultiplier = 0.05f;
            _rope.startColor = _rope.endColor = _cfg.ink;
            _rope.sortingOrder = 2;

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // Halo cálido detrás
            Gfx.Shape("Glow", InkArt.Soft, _body, Vector2.zero, new Vector2(r * 3.4f, r * 3.8f), new Color(1f, 0.55f, 0.25f, 0.22f), 2);
            // Contorno de tinta y cuerpo rojo (ovalado, más alto que ancho)
            Gfx.Shape("Outline", Gfx.Circle, _body, Vector2.zero, new Vector2(r * 1.72f, r * 2.12f), _cfg.ink, 3);
            Gfx.Shape("Paper", Gfx.Circle, _body, Vector2.zero, new Vector2(r * 1.6f, r * 2f), red, 4);
            // Costillas de bambú
            for (int i = -2; i <= 2; i++)
            {
                float y = i * r * 0.36f;
                float w = Mathf.Sqrt(Mathf.Max(0f, 1f - (y / r) * (y / r))) * r * 1.6f;
                Gfx.Shape("Rib", InkArt.Stroke, _body, new Vector2(0f, y), new Vector2(w, 0.07f), new Color(0f, 0f, 0f, 0.28f), 5);
            }
            // Brillo
            Gfx.Shape("Shine", InkArt.Soft, _body, new Vector2(-r * 0.35f, r * 0.35f), new Vector2(r * 0.5f, r * 0.9f), new Color(1f, 1f, 1f, 0.22f), 6);
            // Emblema central (mon): disco de papel con anillo de tinta = zona de acierto perfecto
            float c = _cfg.lanternCenterRadius;
            Gfx.Shape("MonPaper", Gfx.Circle, _body, Vector2.zero, Vector2.one * c * 2f, _cfg.paper, 7);
            Gfx.Shape("MonRing", InkArt.Ring, _body, Vector2.zero, Vector2.one * c * 2.5f, _cfg.ink, 8);
            Gfx.Shape("MonDot", InkArt.Blob, _body, Vector2.zero, Vector2.one * c * 0.7f, red, 8);
            // Tapas negras arriba y abajo y borla
            Gfx.Sliced("CapTop", InkArt.RoundRect, _body, new Vector2(0f, r * 1.02f), new Vector2(r * 1.05f, 0.3f), _cfg.ink, 9);
            Gfx.Sliced("CapBottom", InkArt.RoundRect, _body, new Vector2(0f, -r * 1.02f), new Vector2(r * 1.05f, 0.3f), _cfg.ink, 9);
            var tassel = Gfx.Shape("Tassel", InkArt.Stroke, _body, new Vector2(0f, -r * 1.45f), new Vector2(0.7f, 0.16f), _cfg.ink, 9);
            tassel.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
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
