using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Fondo hipercasual: degradado de dos colores, montañas lejanas translúcidas con parallax
    // y el suelo como una losa de color con borde vivo (por delante de las paredes, que quedan «plantadas»). Los colores salen de la paleta de la zona.
    public sealed class Scenery : MonoBehaviour
    {
        private Camera _cam;
        private ArrowConfig _cfg;
        private SpriteRenderer _sky, _ground, _lip;

        private sealed class Decor { public SpriteRenderer R; public float LayerX, Follow, Y, Spacing; public int Count; }
        private readonly List<Decor> _decor = new List<Decor>();

        public static Scenery Create(Camera cam, ArrowConfig cfg)
        {
            var go = new GameObject("Scenery");
            var s = go.AddComponent<Scenery>();
            s._cam = cam;
            s._cfg = cfg;
            s.Build();
            return s;
        }

        private void Build()
        {
            float viewH = _cfg.cameraSize * 2f;
            float width = viewH * 4f;

            _sky = new GameObject("Sky").AddComponent<SpriteRenderer>();
            _sky.transform.SetParent(transform, false);
            _sky.sortingOrder = -100;

            _ground = Art.Box("Ground", transform, Vector2.zero, new Vector2(width, 3f), Color.white, 7);
            _lip = Art.Box("GroundLip", transform, Vector2.zero, new Vector2(width, 0.5f), Color.white, 8);

            // Montañas: dos capas con distinto parallax y tamaño
            AddHills(6, 7f, 0.7f, 4.5f, -40);
            AddHills(5, 11f, 0.85f, 7f, -50);
            SetPalette(Art.P);
        }

        private void AddHills(int count, float spacing, float follow, float height, int order)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = Art.Shape("hill", transform, Vector2.zero, height * Random.Range(0.75f, 1.2f), Color.white, order, new Vector2(0.5f, 0f));
                _decor.Add(new Decor
                {
                    R = sr, LayerX = i * spacing + Random.Range(-0.3f, 0.3f) * spacing - spacing, Follow = follow,
                    Y = _cfg.groundY - 0.2f, Spacing = spacing, Count = count
                });
            }
        }

        public void SetPalette(ZonePalette p)
        {
            _sky.sprite = Art.Gradient(p.skyTop, p.skyBottom);
            _ground.color = p.ground;
            _lip.color = p.groundLip;
            foreach (var d in _decor) d.R.color = p.hills;
            if (_cam != null) _cam.backgroundColor = p.skyBottom;
        }

        private void LateUpdate()
        {
            Vector3 c = _cam.transform.position;
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;

            // El cielo cubre siempre la vista (un poco de margen para el temblor de cámara)
            _sky.transform.position = new Vector3(c.x, c.y, 0f);
            _sky.transform.localScale = new Vector3(halfW * 2.2f / _sky.sprite.bounds.size.x, halfH * 2.2f / _sky.sprite.bounds.size.y, 1f);

            _ground.transform.position = new Vector3(c.x, _cfg.groundY - 1.25f, 0f);
            _lip.transform.position = new Vector3(c.x, _cfg.groundY + 0.05f, 0f);

            foreach (var d in _decor)
            {
                float x = d.LayerX + c.x * d.Follow;
                if (x < c.x - halfW - d.Spacing) { d.LayerX += d.Spacing * d.Count; x += d.Spacing * d.Count; }
                d.R.transform.position = new Vector3(x, d.Y, 0f);
            }
        }
    }
}
