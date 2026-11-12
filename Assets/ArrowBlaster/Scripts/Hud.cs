using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowBlaster
{
    // UI de prueba en blanco y negro, construida por código. Todos los textos van centrados en su caja
    // y con auto-size con límites (Best Fit), para que no se salgan de su sitio.
    // El diseño definitivo se presenta para su visto bueno el día 4.
    public sealed class Hud
    {
        private readonly ArrowGame _game;
        private readonly ArrowConfig _cfg;
        private readonly Font _font;
        private readonly RectTransform _root;

        private Image _forceFill;
        private Text _distance, _coins, _hint, _toast, _small;
        private readonly List<Image> _lives = new List<Image>();
        private RectTransform _livesRow;
        private Image _flash;
        private GameObject _overPanel, _pausePanel, _pauseButton;
        private Text _overTitle, _overBody;
        private Coroutine _toastRoutine, _smallRoutine, _flashRoutine;

        public Hud(ArrowGame game, ArrowConfig cfg)
        {
            _game = game;
            _cfg = cfg;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo.GetComponent<RectTransform>();

            Build();
        }

        private void Build()
        {
            // Barra de fuerza (arriba a la izquierda)
            var bar = Box("ForceBar", _root, new Vector2(0, 1), new Vector2(60, -60), new Vector2(520, 44), _cfg.ink);
            var inner = Box("Inner", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(508, 32), _cfg.paper);
            _forceFill = Box("Fill", inner, new Vector2(0, 0.5f), Vector2.zero, new Vector2(508, 32), _cfg.ink).GetComponent<Image>();
            _forceFill.rectTransform.pivot = new Vector2(0, 0.5f);
            Label("ForceLabel", bar, new Vector2(0, 0), new Vector2(0, -34), new Vector2(200, 30), "FORCE", 18, 24, TextAnchor.MiddleLeft, _cfg.ink)
                .rectTransform.pivot = new Vector2(0, 1);

            // Vidas
            _livesRow = new GameObject("Lives", typeof(RectTransform)).GetComponent<RectTransform>();
            _livesRow.SetParent(_root, false);
            Anchor(_livesRow, new Vector2(0, 1), new Vector2(60, -150), new Vector2(300, 40));

            // Distancia y monedas
            _distance = Label("Distance", _root, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(500, 90), "0 m", 40, 72, TextAnchor.MiddleCenter, _cfg.ink);
            _coins = Label("Coins", _root, new Vector2(1, 1), new Vector2(-200, -70), new Vector2(320, 70), "0", 30, 52, TextAnchor.MiddleCenter, _cfg.ink);

            // Pausa
            _pauseButton = MakeButton("Pause", _root, new Vector2(1, 1), new Vector2(-60, -60), new Vector2(84, 84), "II", () => _game.SetPaused(true)).gameObject;

            // Indicaciones y avisos
            _hint = Label("Hint", _root, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1200, 90), "", 32, 56, TextAnchor.MiddleCenter, _cfg.ink);
            _toast = Label("Toast", _root, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1000, 150), "", 50, 110, TextAnchor.MiddleCenter, _cfg.ink);
            _small = Label("SmallToast", _root, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(400, 70), "", 30, 52, TextAnchor.MiddleCenter, _cfg.reward);

            // Destello al recibir daño
            _flash = Box("Flash", _root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            Stretch(_flash.rectTransform);
            _flash.raycastTarget = false;

            // Fin de run
            _overPanel = Panel("Over");
            _overTitle = Label("Title", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1000, 140), "", 50, 110, TextAnchor.MiddleCenter, _cfg.ink);
            _overBody = Label("Body", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 220), "", 28, 48, TextAnchor.MiddleCenter, _cfg.ink);
            MakeButton("Again", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(420, 120), "AGAIN", _game.NewRun);
            _overPanel.SetActive(false);

            // Pausa
            _pausePanel = Panel("PausePanel");
            Label("Title", _pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 140), "PAUSED", 50, 110, TextAnchor.MiddleCenter, _cfg.ink);
            MakeButton("Resume", _pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(420, 120), "RESUME", () => _game.SetPaused(false));
            MakeButton("Restart", _pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(420, 110), "RESTART", _game.NewRun);
            _pausePanel.SetActive(false);
        }

        // ───────────── API para el juego ─────────────

        public void ShowRun(int lives)
        {
            _overPanel.SetActive(false);
            _toast.text = "";
            _small.text = "";
            SetLives(lives);
        }

        public void SetHint(string text) => _hint.text = text;

        public void Tick(float force, float fullForce, float distance, int coins)
        {
            float t = Mathf.Clamp01(force / Mathf.Max(1f, fullForce));
            _forceFill.rectTransform.sizeDelta = new Vector2(508f * t, 32f);
            _distance.text = $"{Mathf.Max(0f, distance):0} m";
            _coins.text = coins.ToString();
        }

        public void SetLives(int lives)
        {
            while (_lives.Count < _cfg.lives)
            {
                int i = _lives.Count;
                var img = Box("Life", _livesRow, new Vector2(0, 0.5f), new Vector2(20 + i * 52, 0), new Vector2(38, 38), _cfg.ink).GetComponent<Image>();
                _lives.Add(img);
            }
            for (int i = 0; i < _lives.Count; i++)
                _lives[i].color = i < lives ? _cfg.ink : _cfg.inkSoft;
        }

        public void Toast(string text, Color color, bool small = false)
        {
            var label = small ? _small : _toast;
            label.text = text;
            label.color = color;
            if (small) { if (_smallRoutine != null) _game.StopCoroutine(_smallRoutine); _smallRoutine = _game.StartCoroutine(Fade(label, 0.7f)); }
            else { if (_toastRoutine != null) _game.StopCoroutine(_toastRoutine); _toastRoutine = _game.StartCoroutine(Fade(label, 1.3f)); }
        }

        public void Flash(Color color)
        {
            if (_flashRoutine != null) _game.StopCoroutine(_flashRoutine);
            _flashRoutine = _game.StartCoroutine(FlashRoutine(color));
        }

        public void ShowOver(float distance, int earned, float best, bool record, string reason)
        {
            _overTitle.text = record ? "NEW RECORD!" : reason.ToUpperInvariant();
            _overBody.text = $"{distance:0} m\n+{earned} coins\nBest {best:0} m";
            _overPanel.SetActive(true);
        }

        public void SetPaused(bool paused)
        {
            _pausePanel.SetActive(paused);
            _pauseButton.SetActive(!paused);
        }

        // ───────────── Animaciones ─────────────

        private static IEnumerator Fade(Text label, float duration)
        {
            Color c = label.color;
            var rt = label.rectTransform;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(1.3f, 1f, k / 0.15f) : 1f);
                label.color = new Color(c.r, c.g, c.b, k < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.7f) / 0.3f));
                yield return null;
            }
            label.text = "";
            label.color = c;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                _flash.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.35f, 0f, t / 0.3f));
                yield return null;
            }
            _flash.color = Color.clear;
        }

        // ───────────── Ayudas de construcción ─────────────

        private GameObject Panel(string name)
        {
            var bg = Box(name, _root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(_cfg.paper.r, _cfg.paper.g, _cfg.paper.b, 0.92f));
            Stretch(bg);
            return bg.gameObject;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, anchor, pos, size);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, int minSize, int maxSize, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Anchor(rt, anchor, pos, size);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.fontStyle = FontStyle.Bold;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = maxSize;
            t.fontSize = maxSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Box(name, parent, anchor, pos, size, _cfg.ink);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            var inner = Box("Inner", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12, 12), _cfg.paper);
            inner.GetComponent<Image>().raycastTarget = false;
            Label("Text", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30, 24), text, 20, 56, TextAnchor.MiddleCenter, _cfg.ink);
            return b;
        }
    }
}
