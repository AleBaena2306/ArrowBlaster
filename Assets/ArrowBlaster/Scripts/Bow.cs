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

        private LineRenderer _limbs, _string;
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
            _limbs.widthMultiplier = scale;
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
            // Una sola pincelada gruesa y seca, que se afila en las puntas curvadas
            _limbs = MakeLine("Limbs", Segments, cfg.ink, 12, dry: 1f, wobble: 0.03f);
            _limbs.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.07f, 0.09f), new Keyframe(0.25f, 0.16f), new Keyframe(0.75f, 0.16f),
                new Keyframe(0.93f, 0.08f), new Keyframe(1f, 0f));

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
            float tipX = Mathf.Lerp(RestTipX, DrawnTipX, draw);
            float squeeze = Mathf.Lerp(1f, 0.93f, draw);
            Vector2 topTip = Vector2.zero, bottomTip = Vector2.zero;
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1);
                float y = Mathf.Lerp(Top, Bottom, t) * squeeze;
                // s: -1 en la punta superior, 0 en la empuñadura, 1 en la inferior (asimétrico)
                float s = y >= 0f ? -y / (Top * squeeze) : y / (Bottom * squeeze);
                float x = Mathf.Lerp(Bulge, tipX, s * s);
                // Puntas que se curvan hacia delante (recurvado), como en la referencia
                x += Mathf.SmoothStep(0.75f, 1f, Mathf.Abs(s)) * 0.16f;
                _limbs.SetPosition(i, new Vector3(x, y, 0f));
                if (i == 0) topTip = new Vector2(x, y);
                if (i == Segments - 1) bottomTip = new Vector2(x, y);
            }

            NockX = Mathf.Lerp(RestNockX, RestNockX - MaxPull, draw) + vibration;
            _string.SetPosition(0, topTip);
            _string.SetPosition(1, new Vector3(NockX, 0f, 0f));
            _string.SetPosition(2, bottomTip);
        }
    }
}
