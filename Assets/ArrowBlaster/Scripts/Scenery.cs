using UnityEngine;

namespace ArrowBlaster
{
    // Fondo de tinta: papel, sol rojo, tres capas de montañas a la aguada con parallax y el suelo.
    public sealed class Scenery : MonoBehaviour
    {
        private sealed class Layer
        {
            public Transform[] Copies;
            public float Width, Follow, Y;
        }

        private Camera _cam;
        private ArrowConfig _cfg;
        private Layer[] _layers;
        private SpriteRenderer _paper;
        private Transform _sun;

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

            _paper = Gfx.Shape("Paper", InkArt.Paper, transform, Vector2.zero, Vector2.one, _cfg.paper, -100);
            _paper.drawMode = SpriteDrawMode.Tiled;
            _paper.size = new Vector2(viewH * 2.5f, viewH * 1.2f);

            var sun = Gfx.Shape("Sun", InkArt.Soft, transform, Vector2.zero, Vector2.one * 2.6f, new Color(0.78f, 0.06f, 0.18f, 0.85f), -95);
            _sun = sun.transform;

            Color ink = _cfg.ink;
            _layers = new[]
            {
                MakeLayer("MountainsFar", InkArt.Mountains(0), 1.7f, 0.92f, _cfg.groundY + 0.2f, new Color(ink.r, ink.g, ink.b, 0.16f), -90),
                MakeLayer("MountainsMid", InkArt.Mountains(1), 1.25f, 0.8f, _cfg.groundY - 0.1f, new Color(ink.r, ink.g, ink.b, 0.28f), -80),
                MakeLayer("MountainsNear", InkArt.Mountains(2), 0.95f, 0.6f, _cfg.groundY - 0.3f, new Color(ink.r, ink.g, ink.b, 0.5f), -70),
                MakeLayer("Ground", InkArt.Ground, 1.2f, 0f, _cfg.groundY + 0.35f, ink, 1),
            };
        }

        private Layer MakeLayer(string name, Sprite sprite, float scale, float follow, float y, Color color, int order)
        {
            var layer = new Layer { Follow = follow, Y = y, Width = sprite.bounds.size.x * scale, Copies = new Transform[3] };
            for (int i = 0; i < 3; i++)
            {
                var sr = Gfx.Shape(name, sprite, transform, Vector2.zero, Vector2.one * scale, color, order);
                layer.Copies[i] = sr.transform;
            }
            return layer;
        }

        private void LateUpdate()
        {
            Vector3 c = _cam.transform.position;
            _paper.transform.position = new Vector3(c.x, c.y, 0f);
            float halfW = _cam.orthographicSize * _cam.aspect;
            _sun.position = new Vector3(c.x + halfW * 0.55f, c.y + _cam.orthographicSize * 0.45f, 0f);

            foreach (var l in _layers)
            {
                // Desplazamiento de la capa respecto a la cámara (Follow 1 = pegada a la cámara, 0 = fija al mundo)
                float offset = c.x * l.Follow;
                float local = c.x - offset;                                   // posición de la cámara en el espacio de la capa
                float start = Mathf.Floor(local / l.Width) * l.Width;         // copia que contiene a la cámara
                for (int i = 0; i < 3; i++)
                {
                    float x = start + (i - 1) * l.Width + offset;
                    l.Copies[i].position = new Vector3(x, l.Y, 0f);
                }
            }
        }
    }
}
