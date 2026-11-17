using UnityEngine;

namespace ArrowBlaster
{
    // Lanzador mínimo: dos puntos y una goma. La goma se estira al apuntar y rebota al soltar.
    // Mantiene la API del antiguo arco (Aim, SetDraw, Release, NockX) para no tocar la lógica del juego.
    public sealed class Bow : MonoBehaviour
    {
        private const float Top = 1.3f, Bottom = -1.3f;     // puntos de anclaje respecto al centro
        private const float TipX = -0.2f;
        private const float RestNockX = -0.2f;
        private const float MaxPull = 1.4f;                  // cuánto retrocede el puntero con la goma tensa

        private LineRenderer _band;
        private SpriteRenderer _topPeg, _bottomPeg;
        private float _draw, _releaseTime = -1f, _releaseFrom, _wobble, _scale = 1f;
        public float Aim;

        public float Draw => _draw;
        public float NockX { get; private set; } = RestNockX;

        public void SetScale(float scale)
        {
            _scale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
            if (_band != null) _band.widthMultiplier = 0.12f * scale;
        }

        public static Bow Create(Transform parent, ArrowConfig cfg)
        {
            var go = new GameObject("Bow");
            go.transform.SetParent(parent, false);
            var bow = go.AddComponent<Bow>();
            bow.Build();
            return bow;
        }

        private void Build()
        {
            Color c = Art.P.trail;
            _band = Art.Line("Band", transform, 3, 0.12f, new Color(c.r, c.g, c.b, 0.9f), 10);
            _topPeg = Art.Shape("circle", transform, new Vector2(TipX, Top), 0.34f, Color.white, 13);
            _bottomPeg = Art.Shape("circle", transform, new Vector2(TipX, Bottom), 0.34f, Color.white, 13);
            Refresh();
        }

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
            if (_band == null) return;
            float vibration = 0f;
            if (_releaseTime >= 0f)
            {
                // La goma pasa de largo y rebota amortiguada
                float t = _releaseTime;
                vibration = _releaseFrom * 0.6f * Mathf.Exp(-7f * t) * Mathf.Cos(t * 45f);
                _wobble = -4f * _releaseFrom * Mathf.Exp(-6f * t) * Mathf.Sin(t * 30f);
            }
            else _wobble = 0f;

            transform.localRotation = Quaternion.Euler(0f, 0f, Aim + _wobble);
            NockX = Mathf.Lerp(RestNockX, RestNockX - MaxPull, _draw) + vibration;
            _band.SetPosition(0, new Vector3(TipX, Top, 0f));
            _band.SetPosition(1, new Vector3(NockX, 0f, 0f));
            _band.SetPosition(2, new Vector3(TipX, Bottom, 0f));
            // Los puntos se aprietan un poco al tensar
            float k = 1f + _draw * 0.25f;
            _topPeg.transform.localScale = _bottomPeg.transform.localScale = Vector3.one * (0.34f / _topPeg.sprite.bounds.size.y) * k;
        }
    }
}
