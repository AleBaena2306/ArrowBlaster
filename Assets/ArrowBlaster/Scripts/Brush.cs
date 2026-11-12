using UnityEngine;

namespace ArrowBlaster
{
    // Trazos de pincel: líneas con grosor de pincelada (entra fino, engorda y se seca al final)
    // y el shader de tinta. Todo lo que se ve en el juego se dibuja con estos trazos.
    public static class Brush
    {
        // Grosor a lo largo del trazo: entrada, cuerpo y salida seca
        private static readonly AnimationCurve Taper = new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.12f, 1f), new Keyframe(0.75f, 0.85f), new Keyframe(1f, 0.15f));

        public static LineRenderer Stroke(string name, Transform parent, Vector3[] points, float width, Color color, int order, float dry = 0.7f, AnimationCurve curve = null, float? wobble = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.widthCurve = curve ?? Taper;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 3;
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.View;
            InkArt.Apply(lr, dry: dry, wobble: wobble, bristles: 30f);
            return lr;
        }

        // Trazo recto con una ligera curva de mano
        public static LineRenderer Line(string name, Transform parent, Vector2 from, Vector2 to, float width, Color color, int order, float bend = 0.06f, float dry = 0.7f)
        {
            const int n = 8;
            var pts = new Vector3[n];
            Vector2 dir = to - from;
            Vector2 normal = new Vector2(-dir.y, dir.x).normalized * dir.magnitude * bend * Random.Range(-1f, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                pts[i] = from + dir * t + normal * Mathf.Sin(t * Mathf.PI);
            }
            return Stroke(name, parent, pts, width, color, order, dry);
        }

        // Ensō: círculo de un solo trazo que no llega a cerrarse
        public static LineRenderer Enso(string name, Transform parent, Vector2 center, float radius, float width, Color color, int order, float sweep = 320f, float dry = 0.8f)
        {
            const int n = 32;
            var pts = new Vector3[n];
            float start = Random.Range(0f, 360f);
            float wob = Random.Range(0f, 10f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float a = (start + sweep * t) * Mathf.Deg2Rad;
                float r = radius * (1f + (Mathf.PerlinNoise(wob + t * 3f, 0f) - 0.5f) * 0.12f);
                pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return Stroke(name, parent, pts, width, color, order, dry);
        }

        // Toque de pincel: trazo muy corto y gordo (puntos, gotas, monedas)
        public static LineRenderer Dab(string name, Transform parent, Vector2 center, float size, Color color, int order)
        {
            float a = Random.Range(0f, Mathf.PI);
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 0.35f;
            return Stroke(name, parent, new Vector3[] { center - d, center, center + d }, size, color, order, 0.2f);
        }
    }
}
