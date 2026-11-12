using UnityEngine;

namespace ArrowBlaster
{
    // Material de tinta y texturas generadas por código para la interfaz (papel, trazo de pincel, mancha, ensō).
    // Es arte procedural escrito a mano, no generado por IA; sustituible por texturas dibujadas si se quiere.
    public static class InkArt
    {
        private static Material _spriteMaterial, _ink;
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int WorldId = Shader.PropertyToID("_WorldSpace");
        private static readonly int DryId = Shader.PropertyToID("_Dry");

        // Material del shader de tinta (Resources/ArrowBlasterInk.shader). Si no está disponible, sprites normales.
        public static Material Ink
        {
            get
            {
                if (_ink != null) return _ink;
                var shader = Resources.Load<Shader>("ArrowBlasterInk");
                if (shader != null && shader.isSupported) _ink = new Material(shader) { name = "AB_Ink" };
                else
                {
                    Debug.LogWarning("[ArrowBlaster] No se pudo cargar el shader de tinta; se usan sprites normales.");
                    _ink = SpriteMaterial;
                }
                return _ink;
            }
        }

        // Pone el material de tinta con una semilla propia, para que cada pincelada sea distinta.
        // worldSpace: el ruido usa coordenadas del mundo (formas que siguen a la cámara, como el suelo).
        // dry: cuánto pincel seco (null = el valor del material).
        public static void Apply(Renderer r, bool worldSpace = false, float? dry = null)
        {
            r.sharedMaterial = Ink;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(SeedId, Random.Range(0f, 100f));
            mpb.SetFloat(WorldId, worldSpace ? 1f : 0f);
            if (dry.HasValue) mpb.SetFloat(DryId, dry.Value);
            r.SetPropertyBlock(mpb);
        }
        private static Sprite _paper, _stroke, _blob, _ring;
        private static Font _font;

        // Material por defecto de los sprites del proyecto (sirve también para LineRenderer y TrailRenderer)
        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial != null) return _spriteMaterial;
                var go = new GameObject("tmp");
                _spriteMaterial = go.AddComponent<SpriteRenderer>().sharedMaterial;
                Object.Destroy(go);
                return _spriteMaterial;
            }
        }

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/Shojumaru-Regular");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // ───────── Ruido periódico (para texturas que se repiten en horizontal) ─────────

        private static float LoopNoise(float u, float freq, float seed, float y = 0f)
        {
            float a = u * Mathf.PI * 2f;
            float r = freq / (Mathf.PI * 2f);
            return Mathf.PerlinNoise(seed + Mathf.Cos(a) * r + y, seed * 0.37f + Mathf.Sin(a) * r + y);
        }

        private static float Fbm(float u, float freq, float seed, int octaves, float y = 0f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += LoopNoise(u, freq, seed + i * 13.1f, y) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        private static Sprite ToSprite(Texture2D tex, Vector2 pivot, float ppu)
        {
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        private static Texture2D NewTex(int w, int h, TextureWrapMode wrap)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear, name = "AB_Ink" };
        }

        // ───────── Papel ─────────

        // Grano del papel de arroz (blanco con fibras); se tiñe con el color del papel. 1 unidad = 128 px.
        public static Sprite Paper
        {
            get
            {
                if (_paper) return _paper;
                const int s = 256;
                var tex = NewTex(s, s, TextureWrapMode.Repeat);
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = x / (float)s, v = y / (float)s;
                        float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.5f + Fbm(u, 6f, 3f, 3, v * 6f) * 0.5f;
                        float fiber = Mathf.PerlinNoise(x * 0.04f + 10f, y * 0.9f);
                        float k = 1f - (grain * 0.06f + Mathf.SmoothStep(0.62f, 0.8f, fiber) * 0.05f);
                        byte b = (byte)(255 * k);
                        px[y * s + x] = new Color32(b, b, b, 255);
                    }
                tex.SetPixels32(px);
                return _paper = ToSprite(tex, new Vector2(0.5f, 0.5f), 64f);
            }
        }

        // ───────── Trazo de pincel horizontal (barras, botones, paredes) ─────────
        public static Sprite Stroke
        {
            get
            {
                if (_stroke) return _stroke;
                const int w = 256, h = 64;
                var tex = NewTex(w, h, TextureWrapMode.Clamp);
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float taper = Mathf.Clamp01(Mathf.Min(u / 0.06f, (1f - u) / 0.1f));
                    float top = h * (0.5f + 0.42f * taper + (Mathf.PerlinNoise(u * 9f, 1f) - 0.5f) * 0.08f);
                    float bot = h * (0.5f - 0.42f * taper + (Mathf.PerlinNoise(u * 9f, 7f) - 0.5f) * 0.08f);
                    for (int y = 0; y < h; y++)
                    {
                        float a = Mathf.Clamp01(Mathf.Min(top - y, y - bot) / 2f);
                        float bristle = Mathf.PerlinNoise(u * 3f, y * 0.45f);
                        if (u > 0.7f && bristle > 0.6f + (1f - u) * 1.2f) a *= 0.3f; // final seco del trazo
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                }
                tex.SetPixels32(px);
                return _stroke = ToSprite(tex, new Vector2(0.5f, 0.5f), 256f);
            }
        }

        // ───────── Mancha de tinta (salpicaduras, cuerpos) ─────────
        public static Sprite Blob
        {
            get
            {
                if (_blob) return _blob;
                const int s = 96;
                var tex = NewTex(s, s, TextureWrapMode.Clamp);
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s * 0.5f) / (s * 0.5f), dy = (y - s * 0.5f) / (s * 0.5f);
                        float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                        float r = 0.86f + (Fbm(ang, 5f, 2f, 3) - 0.5f) * 0.22f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((r - d) * s * 0.25f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                return _blob = ToSprite(tex, new Vector2(0.5f, 0.5f), s);
            }
        }

        // Anillo de tinta tipo ensō (pincelada circular que no se cierra del todo)
        public static Sprite Ring
        {
            get
            {
                if (_ring) return _ring;
                const int s = 160;
                var tex = NewTex(s, s, TextureWrapMode.Clamp);
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s * 0.5f) / (s * 0.5f), dy = (y - s * 0.5f) / (s * 0.5f);
                        float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.3f, 1f);
                        float thick = Mathf.Lerp(0.16f, 0.03f, Mathf.SmoothStep(0.55f, 0.97f, ang));
                        float wob = (Fbm(ang, 4f, 9f, 2) - 0.5f) * 0.05f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float center = 0.8f + wob;
                        float a = Mathf.Clamp01((thick - Mathf.Abs(d - center)) * s * 0.3f);
                        if (ang > 0.97f) a = 0f;
                        if (ang > 0.75f && Mathf.PerlinNoise(ang * 30f, d * 20f) > 0.65f) a *= 0.4f;
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                return _ring = ToSprite(tex, new Vector2(0.5f, 0.5f), s);
            }
        }

    }
}
