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
        private Text _overTitle, _overBody, _overButtonText, _shopCoins;
        private GameObject _shopButton;
        private RectTransform _forceBar;
        private Image _progressFill, _bossMark;
        private Text _progressLabel;
        private float _drainFx;
        private readonly List<ShopRow> _shopRows = new List<ShopRow>();
        private bool _shopFromAim;

        private sealed class ShopRow
        {
            public Stat Stat;
            public readonly List<Image> Pips = new List<Image>();
            public Button Buy;
            public Text BuyText;
            public Image BuyImage;
        }
        private Coroutine _toastRoutine, _smallRoutine, _flashRoutine;

        public Hud(ArrowGame game, ArrowConfig cfg)
        {
            _game = game;
            _cfg = cfg;
            _font = Art.Font;

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

        private Image _attackZone;

        // Dedo del tutorial: punto blanco con un aro que se expande al «tocar»
        private RectTransform _touch;
        private Image _touchDot, _touchRing;
        private bool _touchOn;
        private Vector2 _touchVp, _touchDrag;
        public bool PulseAttackZone;

        private void Build()
        {
            // Franja de ataque: rojo que se desvanece de izquierda (opaco) a derecha (transparente). Va por debajo del resto.
            var grad = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++) grad.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - x / 63f));
            grad.Apply();
            _attackZone = new GameObject("AttackZone", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _attackZone.rectTransform.SetParent(_root, false);
            _attackZone.sprite = Sprite.Create(grad, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
            _attackZone.color = new Color(_cfg.attack.r, _cfg.attack.g, _cfg.attack.b, 0.22f);
            _attackZone.raycastTarget = false;
            _attackZone.enabled = false;

            // Barra de fuerza (arriba a la izquierda): trazo de pincel que se vacía
            var bar = Box("ForceBar", _root, new Vector2(0, 1), new Vector2(60, -70), new Vector2(540, 64), new Color(_cfg.uiInk.r, _cfg.uiInk.g, _cfg.uiInk.b, 0.18f));
            _forceBar = bar;
            Art.UsePill(bar.GetComponent<Image>());
            _forceFill = Box("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(540, 64), _cfg.uiInk).GetComponent<Image>();
            Art.UsePill(_forceFill);
            Label("ForceLabel", bar, new Vector2(0, 0), new Vector2(6, -6), new Vector2(220, 36), "FORCE", 18, 30, TextAnchor.MiddleLeft, _cfg.uiInk)
                .rectTransform.pivot = new Vector2(0, 1);

            // Vidas
            _livesRow = new GameObject("Lives", typeof(RectTransform)).GetComponent<RectTransform>();
            _livesRow.SetParent(_root, false);
            Anchor(_livesRow, new Vector2(0, 1), new Vector2(60, -178), new Vector2(300, 48));

            // Distancia y monedas
            _distance = Label("Distance", _root, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(500, 90), "0 m", 40, 72, TextAnchor.MiddleCenter, _cfg.uiInk);
            // Barra de progreso hasta el jefe (o vida del jefe), justo debajo de la distancia
            var prog = Box("Progress", _root, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(620, 26), new Color(_cfg.uiInk.r, _cfg.uiInk.g, _cfg.uiInk.b, 0.15f));
            Art.UsePill(prog.GetComponent<Image>());
            _progressFill = Box("Fill", prog, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(620, 26), _cfg.uiInk).GetComponent<Image>();
            Art.UsePill(_progressFill);
            _bossMark = Box("BossMark", prog, new Vector2(1, 0.5f), new Vector2(26, 0), new Vector2(46, 46), _cfg.attack).GetComponent<Image>();
            _bossMark.sprite = Art.Circle;
            _progressLabel = Label("ProgressLabel", _root, new Vector2(0.5f, 1), new Vector2(0, -158), new Vector2(500, 36), "", 16, 26, TextAnchor.MiddleCenter, _cfg.uiInk);
            _coins = Label("Coins", _root, new Vector2(1, 1), new Vector2(-170, -70), new Vector2(240, 70), "0", 30, 52, TextAnchor.MiddleCenter, _cfg.uiInk);
            var coin = Box("CoinIcon", _root, new Vector2(1, 1), new Vector2(-410, -80), new Vector2(50, 50), _cfg.reward).GetComponent<Image>();
            coin.sprite = Art.Circle;

            // Pausa
            _shopButton = MakeButton("Shop", _root, new Vector2(1, 0), new Vector2(-60, 60), new Vector2(300, 100), "SHOP", OpenShopFromAim).gameObject;
            _pauseButton = MakeButton("Pause", _root, new Vector2(1, 1), new Vector2(-50, -50), new Vector2(100, 100), "II", () => _game.SetPaused(true), round: true).gameObject;

            // Indicaciones y avisos
            _hint = Label("Hint", _root, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1200, 90), "", 32, 56, TextAnchor.MiddleCenter, _cfg.uiInk);
            _toast = Label("Toast", _root, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1000, 150), "", 50, 110, TextAnchor.MiddleCenter, _cfg.uiInk);
            _small = Label("SmallToast", _root, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(400, 70), "", 30, 52, TextAnchor.MiddleCenter, _cfg.reward);

            // Dedo del tutorial
            _touch = new GameObject("Touch", typeof(RectTransform)).GetComponent<RectTransform>();
            _touch.SetParent(_root, false);
            Anchor(_touch, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            _touch.pivot = new Vector2(0.5f, 0.5f);
            _touchRing = Box("Ring", _touch, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90), Color.white).GetComponent<Image>();
            _touchRing.sprite = Art.Ring;
            _touchRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _touchDot = Box("Dot", _touch, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80), Color.white).GetComponent<Image>();
            _touchDot.sprite = Art.Circle;
            _touchDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var dotShadow = _touchDot.gameObject.AddComponent<Shadow>();
            dotShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            dotShadow.effectDistance = new Vector2(0f, -5f);
            _touch.gameObject.SetActive(false);

            // Destello al recibir daño
            _flash = Box("Flash", _root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            Stretch(_flash.rectTransform);
            _flash.raycastTarget = false;

            // Fin de run
            _overPanel = Panel("Over");
            _overTitle = Label("Title", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 410), new Vector2(1000, 110), "", 40, 90, TextAnchor.MiddleCenter, _cfg.uiInk);
            _overBody = Label("Body", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 320), new Vector2(1200, 60), "", 24, 40, TextAnchor.MiddleCenter, _cfg.uiInk);
            BuildShop(_overPanel.transform);
            // Monedas disponibles, arriba a la derecha del panel (mismo sitio que en el HUD)
            var coinIcon = Box("CoinIcon", _overPanel.transform, new Vector2(1, 1), new Vector2(-410, -80), new Vector2(50, 50), _cfg.reward).GetComponent<Image>();
            coinIcon.sprite = Art.Circle;
            _shopCoins = Label("Coins", _overPanel.transform, new Vector2(1, 1), new Vector2(-170, -70), new Vector2(240, 70), "0", 30, 52, TextAnchor.MiddleCenter, _cfg.uiInk);
            var again = MakeButton("Again", _overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(420, 110), "AGAIN", OnOverButton);
            _overButtonText = again.GetComponentInChildren<Text>();
            _overPanel.SetActive(false);

            // Pausa
            _pausePanel = Panel("PausePanel");
            Label("Title", _pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 140), "PAUSED", 50, 110, TextAnchor.MiddleCenter, _cfg.uiInk);
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

        // viewport: posición en pantalla (0–1). drag: recorrido del dedo en píxeles de referencia (cero = toque)
        public void SetTouch(bool on, Vector2 viewport, Vector2 drag)
        {
            _touchOn = on;
            _touchVp = viewport;
            _touchDrag = drag;
        }

        private void AnimateTouch()
        {
            if (_attackZone.enabled)
            {
                float a = PulseAttackZone ? 0.3f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : 0.22f;
                _attackZone.color = new Color(_cfg.attack.r, _cfg.attack.g, _cfg.attack.b, a);
            }
            if (_touch.gameObject.activeSelf != _touchOn) _touch.gameObject.SetActive(_touchOn);
            if (!_touchOn) return;
            _touch.anchorMin = _touch.anchorMax = _touchVp;

            if (_touchDrag != Vector2.zero)
            {
                // Arrastre: pulsa, tira hacia atrás, se queda un momento y suelta
                float t = Mathf.Repeat(Time.unscaledTime, 1.6f) / 1.6f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, t));
                _touch.anchoredPosition = _touchDrag * k;
                float alpha = t < 0.85f ? 1f : 1f - (t - 0.85f) / 0.15f;
                _touchDot.color = new Color(1f, 1f, 1f, 0.95f * alpha);
                float press = Mathf.Clamp01(t / 0.15f);
                _touchRing.color = new Color(1f, 1f, 1f, (1f - press) * alpha);
                _touchRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.8f, press);
                _touchDot.rectTransform.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(1.2f, 0.9f, press) : 0.9f);
            }
            else
            {
                // Toque: el punto se hunde y el aro se expande
                float t = Mathf.Repeat(Time.unscaledTime, 0.9f) / 0.9f;
                _touch.anchoredPosition = Vector2.zero;
                _touchDot.color = new Color(1f, 1f, 1f, 0.95f);
                _touchDot.rectTransform.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(1.15f, 0.85f, t / 0.15f) : Mathf.Lerp(0.85f, 1.15f, (t - 0.15f) / 0.85f));
                _touchRing.color = new Color(1f, 1f, 1f, 1f - t);
                _touchRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 2.2f, t);
            }
        }

        public void SetAttackZone(bool visible, float width)
        {
            _attackZone.enabled = visible;
            if (!visible) return;
            var rt = _attackZone.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(width, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // t = avance hacia el jefe (0-1) o, en combate, vida que le queda
        public void SetProgress(float t, bool bossFight, string label)
        {
            SetFill(_progressFill, t, 620f);
            _progressFill.color = bossFight ? _cfg.attack : _cfg.uiInk;
            _bossMark.enabled = !bossFight;
            _progressLabel.text = label;
        }

        public void Tick(float force, float fullForce, float distance, int coins, bool draining = false, bool aiming = false)
        {
            float t = aiming ? 1f : Mathf.Clamp01(force / Mathf.Max(1f, fullForce));
            SetFill(_forceFill, t, 540f);
            _shopButton.SetActive(aiming && !_overPanel.activeSelf);

            // Efecto en la barra mientras se ataca: late en rojo, tiembla y suelta gotas de tinta
            _drainFx = Mathf.MoveTowards(_drainFx, draining ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 24f);
            _forceFill.color = Color.Lerp(_cfg.uiInk, _cfg.attack, _drainFx * (0.6f + 0.4f * pulse));
            _forceBar.anchoredPosition = new Vector2(60, -70) + Random.insideUnitCircle * 5f * _drainFx;
            if (draining && Random.value < 0.5f) _game.StartCoroutine(Drip(t));
            _distance.text = $"{Mathf.Max(0f, distance):0} m";
            AnimateTouch();
            _coins.text = coins.ToString();
        }

        public void SetLives(int lives)
        {
            while (_lives.Count < _cfg.lives)
            {
                int i = _lives.Count;
                var img = Box("Life", _livesRow, new Vector2(0, 0.5f), new Vector2(10 + i * 56, 0), new Vector2(46, 46), _cfg.uiInk).GetComponent<Image>();
                img.sprite = Art.Heart;
                _lives.Add(img);
            }
            for (int i = 0; i < _lives.Count; i++)
                _lives[i].color = i < lives ? _cfg.attack : new Color(_cfg.uiInk.r, _cfg.uiInk.g, _cfg.uiInk.b, 0.15f);
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
            _shopFromAim = false;
            _overTitle.text = record ? "NEW RECORD!" : reason.ToUpperInvariant();
            _overBody.text = $"{distance:0} m   ·   +{earned} coins   ·   Best {best:0} m";
            _overButtonText.text = "AGAIN";
            RefreshShop();
            _overPanel.SetActive(true);
        }

        private void OpenShopFromAim()
        {
            _shopFromAim = true;
            _overTitle.text = "UPGRADES";
            _overBody.text = "";
            _overButtonText.text = "CLOSE";
            RefreshShop();
            _overPanel.SetActive(true);
        }

        private void OnOverButton()
        {
            if (_shopFromAim) { _overPanel.SetActive(false); _game.NewRun(); }
            else _game.NewRun();
        }

        // ───────────── Tienda ─────────────

        private void BuildShop(Transform parent)
        {
            for (int i = 0; i < Upgrades.All.Length; i++)
            {
                var def = Upgrades.All[i];
                float y = 225 - i * 96;
                var row = new ShopRow { Stat = def.Stat };
                Label("Name", parent, new Vector2(0.5f, 0.5f), new Vector2(-560, y + 14), new Vector2(380, 56), def.Name, 24, 44, TextAnchor.MiddleLeft, _cfg.uiInk);
                Label("Detail", parent, new Vector2(0.5f, 0.5f), new Vector2(-560, y - 30), new Vector2(380, 34), def.Detail, 16, 26, TextAnchor.MiddleLeft, new Color(_cfg.uiInk.r, _cfg.uiInk.g, _cfg.uiInk.b, 0.6f));
                float pipW = Mathf.Min(44f, 560f / def.MaxLevel);
                for (int p = 0; p < def.MaxLevel; p++)
                {
                    var pip = Box("Pip", parent, new Vector2(0.5f, 0.5f), new Vector2(-340 + p * pipW + pipW * 0.5f, y), new Vector2(pipW - 6, 40), def.Color).GetComponent<Image>();
                    Art.UsePill(pip);
                    row.Pips.Add(pip);
                }
                var stat = def.Stat;
                row.Buy = MakeButton("Buy", parent, new Vector2(0.5f, 0.5f), new Vector2(470, y), new Vector2(280, 90), "", () => Buy(stat));
                row.BuyText = row.Buy.GetComponentInChildren<Text>();
                row.BuyImage = row.Buy.GetComponent<Image>();
                _shopRows.Add(row);
            }
        }

        private void Buy(Stat stat)
        {
            if (Upgrades.TryBuy(stat)) Toast(Upgrades.Get(stat).Name + " +1", Upgrades.Get(stat).Color, small: true);
            RefreshShop();
        }

        private void RefreshShop()
        {
            int coins = Save.Coins;
            _shopCoins.text = coins.ToString();
            foreach (var row in _shopRows)
            {
                var def = Upgrades.Get(row.Stat);
                int level = Upgrades.Level(row.Stat);
                for (int p = 0; p < row.Pips.Count; p++)
                    row.Pips[p].color = p < level ? def.Color : new Color(def.Color.r, def.Color.g, def.Color.b, 0.15f);
                bool max = Upgrades.IsMax(row.Stat);
                int cost = Upgrades.Cost(row.Stat);
                row.BuyText.text = max ? "MAX" : cost.ToString();
                bool can = !max && coins >= cost;
                row.Buy.interactable = can;
                row.BuyImage.color = can ? _cfg.uiInk : new Color(_cfg.uiInk.r, _cfg.uiInk.g, _cfg.uiInk.b, 0.3f);
            }
        }

        private IEnumerator Drip(float fill)
        {
            var img = Box("Drip", _forceBar, new Vector2(0, 0.5f), new Vector2(540f * fill - 10f, Random.Range(-14f, 14f)), Vector2.one * Random.Range(10f, 20f), _cfg.attack).GetComponent<Image>();
            img.sprite = Art.Circle;
            Vector2 v = new Vector2(Random.Range(-60f, 60f), Random.Range(-260f, -120f));
            float t = 0f;
            while (t < 0.5f && img != null)
            {
                t += Time.unscaledDeltaTime;
                img.rectTransform.anchoredPosition += v * Time.unscaledDeltaTime;
                img.color = new Color(_cfg.attack.r, _cfg.attack.g, _cfg.attack.b, 1f - t / 0.5f);
                yield return null;
            }
            if (img != null) Object.Destroy(img.gameObject);
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
            var bg = Box(name, _root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(_cfg.uiPanel.r, _cfg.uiPanel.g, _cfg.uiPanel.b, 0.94f));
            var img = bg.GetComponent<Image>();
            img.sprite = null;
            img.raycastTarget = true; // bloquea los toques al juego mientras el panel está abierto
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

        // Barra 9-slice: cambia de ancho en vez de recortarse para conservar los extremos redondos
        private static void SetFill(Image img, float t, float width)
        {
            t = Mathf.Clamp01(t);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.y, width * t), rt.sizeDelta.y);
            img.enabled = t > 0.001f;
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
            t.fontStyle = FontStyle.Normal;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = maxSize;
            t.fontSize = maxSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.25f);
            sh.effectDistance = new Vector2(0f, -4f);
            return t;
        }

        // Botón hipercasual: píldora blanca con el texto en el color del panel; los redondos son un aro
        private Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction onClick, bool round = false)
        {
            var rt = Box(name, parent, anchor, pos, size, _cfg.uiInk);
            var img = rt.GetComponent<Image>();
            if (round) img.sprite = Art.Ring; else Art.UsePill(img);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            Label("Text", rt, new Vector2(0.5f, 0.5f), round ? Vector2.zero : new Vector2(-size.x * 0.04f, 0f), size * (round ? 0.5f : 0.72f), text, 20, 60,
                TextAnchor.MiddleCenter, round ? _cfg.uiInk : _cfg.uiPanel);
            return b;
        }
    }
}
