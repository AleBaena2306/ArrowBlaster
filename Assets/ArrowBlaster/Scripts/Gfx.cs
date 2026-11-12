using UnityEngine;

namespace ArrowBlaster
{
    // Formas de prueba (bloques grises) generadas por código. Se sustituyen por el arte definitivo el día 4.
    public static class Gfx
    {
        private static Sprite _square, _circle, _triangle;

        public static Sprite Square => _square ? _square : (_square = Make(4, (x, y) => true));
        public static Sprite Circle => _circle ? _circle : (_circle = Make(64, (x, y) =>
        {
            float dx = x - 31.5f, dy = y - 31.5f;
            return dx * dx + dy * dy <= 31.5f * 31.5f;
        }));
        // Triángulo que apunta a la derecha (punta de la flecha)
        public static Sprite Triangle => _triangle ? _triangle : (_triangle = Make(64, (x, y) =>
        {
            float t = x / 63f;
            return Mathf.Abs(y - 31.5f) <= (1f - t) * 31.5f;
        }));

        private static Sprite Make(int size, System.Func<int, int, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "AB_Shape"
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // pixelsPerUnit = size: cada forma mide 1 × 1 unidad y se escala con transform.localScale
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // Sprite de 9-slice estirado sin deformar las esquinas (p. ej. InkArt.RoundRect)
        public static SpriteRenderer Sliced(string name, Sprite sprite, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = Shape(name, sprite, parent, pos, Vector2.one, color, order);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }

        public static SpriteRenderer Shape(string name, Sprite sprite, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
