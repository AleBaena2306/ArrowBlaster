using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Colores de una zona. Cada zona del juego cambia de paleta (estilo hipercasual: dos tonos vivos y contraste).
    [System.Serializable]
    public struct ZonePalette
    {
        public Color skyTop, skyBottom;     // degradado del fondo
        public Color hills;                 // montañas lejanas (con transparencia)
        public Color ground, groundLip;     // suelo y su borde superior
        public Color wall, wallSide;        // paredes y su cara lateral (relieve)
        public Color pointer, trail;        // relleno del puntero; contorno y estela
        public Color coin, coinRing;        // monedas
        public Color enemy;                 // enemigos, disparos y jefe
    }

    // Arte hipercasual: figuras geométricas generadas con ShapeForge (Art~/shapes.json → Resources/Shapes).
    // Los sprites blancos se tiñen con la paleta de la zona; la diana y la estrella llevan su propio color.
    public static class Art
    {
        public static ZonePalette P;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Material _lineMaterial;
        private static Font _font;

        public static Sprite Get(string name, Vector2? pivot = null, float ppu = 256f, float border = 0f)
        {
            Vector2 pv = pivot ?? new Vector2(0.5f, 0.5f);
            string key = name + pv + ppu + border;
            if (Cache.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Shapes/" + name);
            if (tex == null) { Debug.LogWarning("[ArrowBlaster] Falta Resources/Shapes/" + name + ".png"); tex = Texture2D.whiteTexture; }
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pv, ppu, 0, SpriteMeshType.FullRect, Vector4.one * border);
            Cache[key] = s;
            return s;
        }

        public static Sprite Circle => Get("circle");
        public static Sprite Ring => Get("ring");
        public static Sprite Heart => Get("heart");
        // Píldora de UI (9-slice): los extremos siempre redondos, se estire lo que se estire
        public static Sprite Pill => Get("pill", null, 100f, 62f);
        // Losa de esquinas redondeadas para paredes y suelo (9-slice, 1 unidad = 128 px)
        public static Sprite Slab => Get("slab", null, 128f, 34f);

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // Material plano para líneas y estelas (sin él, LineRenderer/TrailRenderer salen en rosa)
        public static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null) _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "AB_Flat" };
                return _lineMaterial;
            }
        }

        // Figura teñida. height = alto en unidades de mundo.
        public static SpriteRenderer Shape(string name, Transform parent, Vector2 pos, float height, Color color, int order, Vector2? pivot = null)
        {
            var sprite = Get(name, pivot);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // Losa 9-slice de tamaño libre (paredes, suelo)
        public static SpriteRenderer Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Slab;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static LineRenderer Line(string name, Transform parent, int points, float width, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 4;
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = LineMaterial;
            return lr;
        }

        // Imagen de UI con píldora 9-slice
        public static void UsePill(UnityEngine.UI.Image img)
        {
            img.sprite = Pill;
            img.type = UnityEngine.UI.Image.Type.Sliced;
        }

        // Degradado vertical para el fondo
        public static Sprite Gradient(Color top, Color bottom)
        {
            var tex = new Texture2D(2, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 128; y++)
            {
                Color c = Color.Lerp(bottom, top, y / 127f);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
        }
    }
}
