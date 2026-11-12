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
        }

        private void LateUpdate()
        {
            Vector3 c = _cam.transform.position;
            _ground.position = new Vector3(c.x, _cfg.groundY - 1.25f, 0f);
            _groundEcho.position = new Vector3(c.x, _cfg.groundY + 0.6f, 0f);
        }
    }
}
