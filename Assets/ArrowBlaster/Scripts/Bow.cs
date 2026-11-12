using UnityEngine;

namespace ArrowBlaster
{
    // Arco japonés (yumi) dibujado con LineRenderer: asimétrico (empuñadura en el tercio inferior),
    // con animación de tensado, temblor al estar tenso y cuerda que vibra al soltar.
    public sealed class Bow : MonoBehaviour
    {
        private const int Segments = 24;
        private const float Top = 2.05f, Bottom = -1.25f;   // puntas respecto a la empuñadura
        private const float Bulge = 0.28f;                  // curvatura hacia delante en la empuñadura
        private const float RestTipX = -0.32f;              // puntas en reposo (cuerda montada)
        private const float DrawnTipX = -0.62f;             // puntas con el arco tenso
        private const float RestNockX = -0.32f;
        private const float MaxPull = 1.05f;                // cuánto retrocede el culatín de la flecha tenso

        private LineRenderer _string;
        private Transform _limbs;
        private SpriteRenderer _limbsSprite;

        // Imagen del arco: Kenney Scribble Platformer «item_bow» (vertical, abombado hacia delante, sin cuerda).
        // Coordenadas en píxeles de esa imagen (128 × 128): puntas y punto más adelantado (empuñadura).
        private const float ImgTipX = 50f, ImgTopY = 1f, ImgBottomY = 127f, ImgGripX = 83f;
        private float _draw, _releaseTime = -1f, _releaseFrom, _wobble;
        // Ángulo de puntería en grados (el arco gira entero)
        public float Aim;

        public float Draw => _draw;
        private float _scale = 1f;

        // Arco más grande: la geometría escala con el transform, el grosor de las líneas se ajusta aparte
        public void SetScale(float scale)
        {
            _scale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
            _string.widthMultiplier = 0.012f * scale;
        }
        // Posición (local X) del culatín de la flecha en la cuerda
        public float NockX { get; private set; } = RestNockX;

        public static Bow Create(Transform parent, ArrowConfig cfg)
        {
            var go = new GameObject("Bow");
            go.transform.SetParent(parent, false);
            var bow = go.AddComponent<Bow>();
            bow.Build(cfg);
            return bow;
        }

        private void Build(ArrowConfig cfg)
        {
            // Arco: imagen de pincel; si falta, no se dibuja (la cuerda sigue funcionando)
            var tex = Resources.Load<Texture2D>("Kenney/item_bow");
            _limbs = new GameObject("Limbs").transform;
            _limbs.SetParent(transform, false);
            if (tex != null)
            {
                float ppu = (ImgBottomY - ImgTopY) / (Top - Bottom);
                var pivot = new Vector2(ImgGripX / tex.width, 1f - (ImgTopY + ImgBottomY) * 0.5f / tex.height);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
                _limbsSprite = _limbs.gameObject.AddComponent<SpriteRenderer>();
                _limbsSprite.sprite = sprite;
                _limbsSprite.sharedMaterial = KenneyArt.Material;
                _limbsSprite.color = cfg.ink;
                _limbsSprite.sortingOrder = 12;
                // Arco negro: el relleno también en tinta plena
                var mpb = new MaterialPropertyBlock();
                _limbsSprite.GetPropertyBlock(mpb);
                mpb.SetFloat("_Wash", 1f);
                mpb.SetFloat("_Dry", 0.15f);
                _limbsSprite.SetPropertyBlock(mpb);
            }
            else Debug.LogWarning("[ArrowBlaster] Falta Resources/Kenney/item_bow.png");

            // Cuerda: línea fina y limpia, gris oscuro
            _string = MakeLine("String", 3, new Color(0.25f, 0.25f, 0.25f, 1f), 11, dry: 0f, wobble: 0f);
            _string.widthMultiplier = 0.012f;

            Refresh();
        }

        private LineRenderer MakeLine(string name, int points, Color color, int order, float dry, float wobble)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points;
            InkArt.Apply(lr, dry: dry, wobble: wobble, bristles: 30f);
            lr.startColor = lr.endColor = color;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.View;
            return lr;
        }

        // 0 = cuerda en reposo, 1 = arco completamente tenso
        public void SetDraw(float draw)
        {
            _draw = Mathf.Clamp01(draw);
            _releaseTime = -1f;
        }

        public void Release()
        {
            _releaseFrom = _draw;
            _releaseTime = 0f;
            _draw = 0f;
        }

        private void Update()
        {
            if (_releaseTime >= 0f) _releaseTime += Time.deltaTime;
            Refresh();
        }

        private void Refresh()
        {
            if (_limbs == null) return;
            float draw = _draw;
            float vibration = 0f;
            if (_releaseTime >= 0f)
            {
                // La cuerda pasa de largo y vibra amortiguada; el arco da un pequeño latigazo
                float t = _releaseTime;
                vibration = _releaseFrom * 0.45f * Mathf.Exp(-7f * t) * Mathf.Cos(t * 55f);
                _wobble = -6f * _releaseFrom * Mathf.Exp(-6f * t) * Mathf.Sin(t * 30f);
            }
            else
            {
                // Temblor de tensión cuando está casi al máximo
                float tremble = Mathf.SmoothStep(0.85f, 1f, draw) * 0.012f;
                _wobble = (Mathf.PerlinNoise(Time.time * 14f, 0f) - 0.5f) * tremble * 60f;
            }

            transform.localRotation = Quaternion.Euler(0f, 0f, Aim + _wobble);
            // Al tensar, las puntas retroceden: la imagen se estira en profundidad alrededor de la empuñadura
            float gripX = RestTipX + (ImgGripX - ImgTipX) / ((ImgBottomY - ImgTopY) / (Top - Bottom));
            float tipX = Mathf.Lerp(RestTipX, DrawnTipX, draw);
            float depthScale = (gripX - tipX) / (gripX - RestTipX);
            float squeeze = Mathf.Lerp(1f, 0.95f, draw);
            float midY = (Top + Bottom) * 0.5f;
            _limbs.localPosition = new Vector3(gripX, midY, 0f);
            _limbs.localScale = new Vector3(depthScale, squeeze, 1f);
            Vector2 topTip = new Vector2(tipX, midY + (Top - midY) * squeeze);
            Vector2 bottomTip = new Vector2(tipX, midY + (Bottom - midY) * squeeze);

            NockX = Mathf.Lerp(RestNockX, RestNockX - MaxPull, draw) + vibration;
            _string.SetPosition(0, topTip);
            _string.SetPosition(1, new Vector3(NockX, 0f, 0f));
            _string.SetPosition(2, bottomTip);
        }
    }
}
