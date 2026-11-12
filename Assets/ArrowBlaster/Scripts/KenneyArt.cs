using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Sprites de Kenney (CC0) en Resources/Kenney, pasados a tinta con el shader ArrowBlaster/InkSprite.
    // Packs: Scribble Platformer, Scribble Platformer Expansion y Shooting Gallery.
    public static class KenneyArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Material _material;
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int KeepId = Shader.PropertyToID("_KeepColor");

        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                var shader = Resources.Load<Shader>("ArrowBlasterInkSprite");
                _material = shader != null && shader.isSupported ? new Material(shader) { name = "AB_InkSprite" } : InkArt.SpriteMaterial;
                return _material;
            }
        }

        // Sprite con el pivote en (pivotX, pivotY) normalizado; 1 unidad = ppu píxeles
        public static Sprite Get(string name, Vector2 pivot, float ppu = 128f)
        {
            string key = name + pivot + ppu;
            if (Cache.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Kenney/" + name);
            if (tex == null) { Debug.LogWarning("[ArrowBlaster] Falta Resources/Kenney/" + name + ".png"); tex = Texture2D.whiteTexture; }
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            Cache[key] = s;
            return s;
        }

        // Sprite de Kenney en tinta. height = alto en unidades de mundo. keepColor = conservar el color saturado.
        public static SpriteRenderer Shape(string name, Transform parent, Vector2 pos, float height, Color ink, int order, bool keepColor = false, Vector2? pivot = null)
        {
            var sprite = Get(name, pivot ?? new Vector2(0.5f, 0.5f));
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = ink;
            sr.sortingOrder = order;
            sr.sharedMaterial = Material;
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(SeedId, Random.Range(0f, 100f));
            mpb.SetFloat(KeepId, keepColor ? 1f : 0f);
            sr.SetPropertyBlock(mpb);
            return sr;
        }
    }
}
