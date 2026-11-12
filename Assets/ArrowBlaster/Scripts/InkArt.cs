using UnityEngine;

namespace ArrowBlaster
{
    // Texturas de tinta generadas por código (papel, montañas a la aguada, trazos de pincel, manchas).
    // Es arte procedural escrito a mano, no generado por IA; sustituible por texturas dibujadas si se quiere.
    public static class InkArt
    {
        private static Material _spriteMaterial;
        private static Sprite _paper, _ground, _stroke, _blob, _soft, _ring, _roundRect;
        private static Sprite[] _mountains = new Sprite[3];
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

        // ───────── Montañas a la aguada (sumi-e) ─────────
        // Cresta oscura que se difumina hacia abajo en niebla; se repite en horizontal.
        public static Sprite Mountains(int index)
        {
            if (_mountains[index]) return _mountains[index];
            const int w = 1024, h = 320;
            float seed = 11f + index * 7.7f;
            var tex = NewTex(w, h, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float ridge = Fbm(u, 3f + index, seed, 5) ;
                ridge = Mathf.Pow(ridge, 1.6f);
                float top = Mathf.Lerp(0.25f, 0.95f, ridge) * h;
                for (int y = 0; y < h; y++)
                {
                    float a = 0f;
                    if (y <= top)
                    {
                        float depth = (top - y) / Mathf.Max(1f, top);       // 0 en la cresta, 1 abajo
                        float edge = Mathf.Clamp01((top - y) / 6f);          // borde suave
                        float wash = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 0.85f, depth));
                        float bleed = Mathf.PerlinNoise(x * 0.02f + seed, y * 0.05f) * 0.35f;
                        float dry = Mathf.PerlinNoise(x * 0.3f, y * 0.08f + seed) > 0.72f ? 0.75f : 1f;
                        a = edge * Mathf.Clamp01(wash + bleed * wash) * dry;
                    }
                    byte g = (byte)(255 * a);
                    px[y * w + x] = new Color32(255, 255, 255, g);
                }
            }
            tex.SetPixels32(px);
            return _mountains[index] = ToSprite(tex, new Vector2(0.5f, 0f), 64f);
        }

        // ───────── Suelo: trazo de tinta con borde irregular ─────────
        public static Sprite Ground
        {
            get
            {
                if (_ground) return _ground;
                const int w = 512, h = 160;
                var tex = NewTex(w, h, TextureWrapMode.Clamp);
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float edge = (0.72f + Fbm(u, 8f, 5f, 4) * 0.22f) * h;
                    for (int y = 0; y < h; y++)
                    {
                        float a = Mathf.Clamp01((edge - y) / 3f);
                        float streak = Mathf.PerlinNoise(x * 0.01f, y * 0.6f);
                        if (y > edge - 18 && streak > 0.68f) a *= 0.55f; // pincel seco cerca del borde
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                }
                tex.SetPixels32(px);
                return _ground = ToSprite(tex, new Vector2(0.5f, 1f), 64f);
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

        // Círculo de bordes suaves (brillos, sol, niebla)
        public static Sprite Soft
        {
            get
            {
                if (_soft) return _soft;
                const int s = 128;
                var tex = NewTex(s, s, TextureWrapMode.Clamp);
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s * 0.5f + 0.5f) / (s * 0.5f), dy = (y - s * 0.5f + 0.5f) / (s * 0.5f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((1f - d) * 24f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                return _soft = ToSprite(tex, new Vector2(0.5f, 0.5f), s);
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

        // Rectángulo redondeado con borde algo irregular (pilares, paneles)
        public static Sprite RoundRect
        {
            get
            {
                if (_roundRect) return _roundRect;
                const int s = 64;
                var tex = NewTex(s, s, TextureWrapMode.Clamp);
                var px = new Color32[s * s];
                const float r = 14f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float qx = Mathf.Max(Mathf.Abs(x - s * 0.5f + 0.5f) - (s * 0.5f - r), 0f);
                        float qy = Mathf.Max(Mathf.Abs(y - s * 0.5f + 0.5f) - (s * 0.5f - r), 0f);
                        float d = Mathf.Sqrt(qx * qx + qy * qy) - r + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 1.5f;
                        float a = Mathf.Clamp01(-d);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                // Bordes de 9-slice para poder estirarlo sin deformar las esquinas
                tex.Apply(false, true);
                return _roundRect = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            }
        }
    }
}
