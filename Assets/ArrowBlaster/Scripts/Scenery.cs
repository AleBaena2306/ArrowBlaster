using UnityEngine;

namespace ArrowBlaster
{
    // Fondo mínimo: papel liso (color de fondo de la cámara) y el suelo como un gran trazo de pincel.
    // El suelo es una sola forma que sigue a la cámara; el shader de tinta usa coordenadas del mundo
    // para que el trazo no «resbale» ni se vea construirse a trozos.
    public sealed class Scenery : MonoBehaviour
    {
        private Camera _cam;
        private ArrowConfig _cfg;
        private Transform _ground, _groundEcho;

        // Decorado de Kenney (árboles y nubes) en tinta clara, con parallax: se recicla al salir por la izquierda
        private sealed class Decor { public Transform T; public float LayerX; public float Follow; public float Y; public float Spacing; public int Count; }
        private readonly System.Collections.Generic.List<Decor> _decor = new System.Collections.Generic.List<Decor>();

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

            // Trazo principal del suelo (su borde superior queda en groundY + 0.25)
            var ground = Gfx.Shape("Ground", Gfx.Square, transform, Vector2.zero, new Vector2(width, 3f), _cfg.ink, 1);
            InkArt.Apply(ground, worldSpace: true, dry: 0.9f);
            _ground = ground.transform;

            // Segundo trazo, fino y claro, por encima: da la sensación de pincelada doble
            var echo = Gfx.Shape("GroundEcho", Gfx.Square, transform, Vector2.zero, new Vector2(width, 0.28f),
                new Color(_cfg.ink.r, _cfg.ink.g, _cfg.ink.b, 0.35f), 0);
            InkArt.Apply(echo, worldSpace: true, dry: 1f);
            _groundEcho = echo.transform;

            Color wash = new Color(_cfg.ink.r, _cfg.ink.g, _cfg.ink.b, 0.45f);
            Color faint = new Color(_cfg.ink.r, _cfg.ink.g, _cfg.ink.b, 0.25f);
            AddDecor(new[] { "background_treeLarge", "background_tree" }, 5, 9f, 0.55f, _cfg.groundY + 0.15f, 3.2f, wash, -20, new Vector2(0.5f, 0f));
            AddDecor(new[] { "background_cloudA", "background_cloudB" }, 4, 13f, 0.85f, 3.2f, 1.4f, faint, -30, new Vector2(0.5f, 0.5f));
        }

        private void AddDecor(string[] names, int count, float spacing, float follow, float y, float height, Color color, int order, Vector2 pivot)
        {
            for (int i = 0; i < count; i++)
            {
                float jitter = Random.Range(-0.3f, 0.3f) * spacing;
                var sr = KenneyArt.Shape(names[i % names.Length], transform, Vector2.zero, height * Random.Range(0.8f, 1.15f), color, order, pivot: pivot);
                _decor.Add(new Decor { T = sr.transform, LayerX = i * spacing + jitter - spacing, Follow = follow, Y = y + Random.Range(-0.3f, 0.3f), Spacing = spacing, Count = count });
            }
        }

        private void LateUpdate()
        {
            Vector3 c = _cam.transform.position;
            _ground.position = new Vector3(c.x, _cfg.groundY - 1.25f, 0f);
            _groundEcho.position = new Vector3(c.x, _cfg.groundY + 0.6f, 0f);

            float halfW = _cam.orthographicSize * _cam.aspect;
            foreach (var d in _decor)
            {
                // Posición en pantalla = posición en su capa + desplazamiento de cámara atenuado
                float x = d.LayerX + c.x * d.Follow;
                if (x < c.x - halfW - d.Spacing) { d.LayerX += d.Spacing * d.Count; x += d.Spacing * d.Count; }
                d.T.position = new Vector3(x, d.Y, 0f);
            }
        }
    }
}
