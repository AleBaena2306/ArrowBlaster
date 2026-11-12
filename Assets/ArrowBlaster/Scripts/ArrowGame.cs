using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArrowBlaster
{
    // Controlador de la run (versión en bloques grises del día 1):
    //   Apuntar → disparar contra la diana (multiplica la fuerza) → volar (tocar = impulso, mantener = atacar)
    //   → la fuerza o la vida llegan a 0 → la flecha cae → resumen y otra run.
    // Toda la escena se construye por código: basta con un GameObject con este componente
    // (lo crea el menú ARROW BLASTER > Setup).
    public sealed class ArrowGame : MonoBehaviour
    {
        public ArrowConfig Config;

        private enum State { Aim, Launch, Flight, Falling, Over }
        private State _state;
        private bool _paused;

        // Mundo
        private Camera _cam;
        private Transform _world;
        private Transform _arrow;
        private SpriteRenderer _arrowHead;
        private SpriteRenderer[] _arrowParts;
        private TrailRenderer _trail;
        private Bow _bow;
        private Lantern _lantern;
        private readonly List<SpriteRenderer> _aimDots = new List<SpriteRenderer>();
        private float _dropTimer;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private float _nextSpawnX;

        // Estado de la run
        private Vector2 _pos, _vel;
        private float _force, _holdTime, _invulnerable, _shake;
        // Puntería con el arco gigante
        private bool _dragging;
        private Vector2 _dragStart;
        private float _aimAngle = 15f, _draw, _drawShown, _launchDraw, _arrowScale = 1f;
        private bool _pressed, _attacking;
        private int _lives, _runCoins;
        private float _launchMultiplier = 1f;

        private Hud _hud;

        public Vector2 ArrowPosition => _pos;

        private void Awake()
        {
            if (Config == null)
            {
                Debug.LogWarning("[ArrowBlaster] Falta ArrowConfig en el componente ArrowGame: se usan los valores por defecto.");
                Config = ScriptableObject.CreateInstance<ArrowConfig>();
            }
            Application.targetFrameRate = 60;
            SetupCamera();
            EnsureEventSystem();
            Scenery.Create(_cam, Config);
            _hud = new Hud(this, Config);
        }

        private void Start() => NewRun();

        // ───────────────────────── Run ─────────────────────────

        public void NewRun()
        {
            if (_world != null) Destroy(_world.gameObject);
            _obstacles.Clear();
            _world = new GameObject("World").transform;

            BuildLaunchArea();
            BuildArrow();

            _pos = new Vector2(0f, 0f);
            _vel = Vector2.zero;
            _force = 0f;
            _lives = Config.lives;
            _runCoins = 0;
            _dragging = false;
            _draw = _drawShown = 0f;
            _aimAngle = 15f;
            _arrowScale = Config.bowScale * 1.1f;
            _invulnerable = 0f;
            _attacking = false;
            _launchMultiplier = 1f;
            _nextSpawnX = Config.firstObstacleX;
            _state = State.Aim;
            _hud.ShowRun(_lives);
            _hud.SetHint("Drag back to aim · Release to shoot");
            SetPaused(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) && _state != State.Over) SetPaused(!_paused);
            if (_paused) return;

            float dt = Time.deltaTime;
            ReadInput();

            switch (_state)
            {
                case State.Aim: UpdateAim(dt); break;
                case State.Launch: UpdateLaunch(dt); break;
                case State.Flight: UpdateFlight(dt); break;
                case State.Falling: UpdateFalling(dt); break;
                case State.Over: break;
            }

            UpdateArrowVisual();
            UpdateCamera(dt);
            _hud.Tick(_force, Config.maxForce * 0.5f, _pos.x, Save.Coins + _runCoins);
        }

        private bool _pressedThisFrame, _releasedThisFrame;

        private void ReadInput()
        {
            bool down = (Input.GetMouseButtonDown(0) && !PointerOverUi()) || Input.GetKeyDown(KeyCode.Space);
            bool held = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
            _pressedThisFrame = down;
            if (down) _pressed = true;
            _releasedThisFrame = _pressed && !held;
            if (!held) _pressed = false;
        }

        private static bool PointerOverUi()
        {
            if (EventSystem.current == null) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return false;
        }

        // ───────────────────────── Lanzamiento ─────────────────────────
        // Arco gigante fijo: se arrastra hacia atrás (como un tirachinas) para apuntar y tensar; al soltar, dispara.
        // La línea de puntos muestra la trayectoria. Atravesar el farolillo multiplica la fuerza de la run.
        // En ordenador también: mantener Espacio tensa y las flechas arriba/abajo cambian el ángulo.
        private const float ArrowTail = 1.12f;   // distancia del centro de la flecha a su culatín (escala 1)

        private Vector2 PointerWorld() => _cam.ScreenToWorldPoint(Input.mousePosition);

        private void UpdateAim(float dt)
        {
            bool mouseHeld = Input.GetMouseButton(0);
            bool keyHeld = Input.GetKey(KeyCode.Space);

            if (_pressedThisFrame && !keyHeld) { _dragging = true; _dragStart = PointerWorld(); }

            if (_dragging && mouseHeld)
            {
                Vector2 drag = _dragStart - PointerWorld();
                _draw = Mathf.Clamp01(drag.magnitude / Config.maxDrag);
                if (drag.sqrMagnitude > 0.04f)
                    _aimAngle = Mathf.Clamp(Mathf.Atan2(drag.y, drag.x) * Mathf.Rad2Deg, Config.minAimAngle, Config.maxAimAngle);
            }
            else if (keyHeld)
            {
                _draw = Mathf.MoveTowards(_draw, 1f, dt * 1.6f);
                float turn = (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1f : 0f);
                _aimAngle = Mathf.Clamp(_aimAngle + turn * 50f * dt, Config.minAimAngle, Config.maxAimAngle);
            }

            bool released = (_dragging && !mouseHeld) || Input.GetKeyUp(KeyCode.Space);
            if (released)
            {
                _dragging = false;
                if (_draw >= Config.minDrawToShoot) { Shoot(); return; }
                _draw = 0f;
            }
            if (!_dragging && !keyHeld) _draw = Mathf.MoveTowards(_draw, 0f, dt * 4f);

            _drawShown = Mathf.Lerp(_drawShown, _draw, 1f - Mathf.Exp(-18f * dt));
            _bow.Aim = _aimAngle;
            _bow.SetDraw(_drawShown);
            PlaceArrowOnBow();
            UpdateTrajectory(_draw >= Config.minDrawToShoot);
        }

        private Vector2 AimDirection => new Vector2(Mathf.Cos(_aimAngle * Mathf.Deg2Rad), Mathf.Sin(_aimAngle * Mathf.Deg2Rad));
        private float LaunchSpeed(float draw) => Mathf.Lerp(Config.launchSpeedMin, Config.launchSpeedMax, draw);

        // La flecha (más grande mientras está en el arco) apoyada en la cuerda, apuntando con el arco
        private void PlaceArrowOnBow()
        {
            Vector2 dir = AimDirection;
            float nock = _bow.NockX * Config.bowScale;
            _pos = Config.bowPosition + dir * (nock + ArrowTail * _arrowScale);
            _vel = dir;
        }

        // Puntos de tinta con la trayectoria prevista hasta pasar el farolillo
        private void UpdateTrajectory(bool visible)
        {
            Vector2 p = _pos + AimDirection * 0.6f * _arrowScale;
            Vector2 v = AimDirection * LaunchSpeed(_draw);
            const float step = 0.045f;
            int shown = 0;
            for (int i = 0; i < _aimDots.Count; i++)
            {
                for (int k = 0; k < 3; k++) { v.y -= Config.launchGravity * step; p += v * step; }
                var d = _aimDots[i];
                bool on = visible && p.x < Config.lanternX + 1.5f && p.y > Config.groundY;
                d.enabled = on;
                if (!on) continue;
                shown++;
                d.transform.position = p;
                float t = i / (float)_aimDots.Count;
                d.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.1f, t);
                d.color = new Color(Config.ink.r, Config.ink.g, Config.ink.b, Mathf.Lerp(0.55f, 0.15f, t));
            }
        }

        private void Shoot()
        {
            _launchDraw = _draw;
            _vel = AimDirection * LaunchSpeed(_draw);
            _bow.Release();
            _drawShown = 0f;
            _state = State.Launch;
            UpdateTrajectory(false);
            _hud.SetHint("");
            _shake = 0.12f + 0.15f * _draw;
        }

        // Vuelo del disparo con gravedad suave hasta cruzar el farolillo (o tocar el suelo)
        private void UpdateLaunch(float dt)
        {
            float prevX = _pos.x, prevY = _pos.y;
            _vel.y -= Config.launchGravity * dt;
            _pos += _vel * dt;
            if (_pos.y > Config.ceilingY) { _pos.y = Config.ceilingY; _vel.y = Mathf.Min(_vel.y, 0f); }

            float lx = _lantern.X;
            if (prevX < lx && _pos.x >= lx)
            {
                float t = (lx - prevX) / Mathf.Max(0.0001f, _pos.x - prevX);
                float yAtLantern = Mathf.Lerp(prevY, _pos.y, t);
                float off = Mathf.Abs(yAtLantern - _lantern.Y);
                if (off <= Config.lanternCenterRadius) BeginFlight(Config.centerMultiplier, "PERFECT", true);
                else if (off <= Config.lanternRadius) BeginFlight(Config.ringMultiplier, "GOOD", true);
                else BeginFlight(Config.missMultiplier, "MISS", false);
                return;
            }
            if (_pos.y <= Config.groundY + 0.2f)
            {
                _pos.y = Config.groundY + 0.25f;
                _vel.y = Config.flapVelocity;
                BeginFlight(Config.missMultiplier, "MISS", false);
            }
        }

        private void BeginFlight(float multiplier, string label, bool hitLantern)
        {
            _launchMultiplier = multiplier;
            _force = Mathf.Min(Config.initialForce * multiplier * Mathf.Lerp(0.7f, 1f, _launchDraw), Config.maxForce);
            bool perfect = multiplier >= Config.centerMultiplier;
            _hud.Toast($"{label}  ×{multiplier:0.0}", perfect ? Config.attack : Config.ink);
            if (hitLantern)
            {
                _lantern.Hit(perfect);
                Splash(_pos, new Color32(0xC8, 0x10, 0x2E, 0xFF), perfect ? 10 : 5, perfect ? 1.3f : 0.9f);
                Splash(_pos, Config.ink, perfect ? 10 : 5, 1f);
                _shake = perfect ? 0.3f : 0.18f;
                StartCoroutine(HitStop(perfect ? 0.08f : 0.04f));
            }
            _vel = new Vector2(Speed(), Mathf.Clamp(_vel.y, -3f, 5f));
            _state = State.Flight;
            _hud.SetHint("Tap to fly · Hold to attack");
            StartCoroutine(HideHintLater(3f));
        }

        private IEnumerator HideHintLater(float t)
        {
            yield return new WaitForSeconds(t);
            if (_state == State.Flight) _hud.SetHint("");
        }

        private float Speed() => Config.minSpeed + _force * Config.speedPerForce;

        private void UpdateFlight(float dt)
        {
            // Entrada: tocar = impulso; mantener = atacar (planeo)
            if (_pressedThisFrame) { _vel.y = Config.flapVelocity; _holdTime = 0f; }
            if (_pressed) _holdTime += dt; else _holdTime = 0f;
            _attacking = _pressed && _holdTime >= Config.holdThreshold;

            if (_attacking)
            {
                _vel.y -= Config.gravity * Config.attackGravityScale * dt;
                _vel.y = Mathf.Lerp(_vel.y, 0f, 6f * dt);
            }
            else _vel.y -= Config.gravity * dt;

            _force = Mathf.Max(0f, _force - Config.forceDecayPerSecond * dt);
            _vel.x = Speed();
            _pos += _vel * dt;

            if (_pos.y > Config.ceilingY) { _pos.y = Config.ceilingY; _vel.y = Mathf.Min(_vel.y, 0f); }
            if (_pos.y < Config.groundY)
            {
                _pos.y = Config.groundY + 0.05f;
                _vel.y = Config.flapVelocity * 0.8f;
                Damage(_pos);
            }

            if (_invulnerable > 0f) _invulnerable -= dt;

            _dropTimer -= dt;
            if (_dropTimer <= 0f)
            {
                _dropTimer = _attacking ? 0.035f : 0.1f;
                Drop(_pos - _vel.normalized * ArrowTail, _attacking ? Config.attack : Config.ink);
            }

            SpawnAhead();
            Vector2 tip = TipPosition();
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var o = _obstacles[i];
                if (o == null) { _obstacles.RemoveAt(i); continue; }
                o.Tick(dt);
                bool gone = _state == State.Flight && o.CheckHit(tip, Config.arrowRadius, _attacking);
                bool behind = o.RightEdge < _pos.x - 12f;
                if (gone || behind)
                {
                    Destroy(o.gameObject);
                    _obstacles.RemoveAt(i);
                }
            }

            if (_state == State.Flight && (_force <= 0f || _lives <= 0)) StartFalling();
        }

        private void StartFalling()
        {
            _state = State.Falling;
            _attacking = false;
            _hud.SetHint("");
        }

        private void UpdateFalling(float dt)
        {
            _vel.x = Mathf.MoveTowards(_vel.x, 0f, 6f * dt);
            _vel.y -= Config.gravity * dt;
            _pos += _vel * dt;
            // Bucle por índice: un enemigo puede disparar (y añadir un proyectil a la lista) mientras se recorre
            for (int i = _obstacles.Count - 1; i >= 0; i--)
                if (_obstacles[i] != null) _obstacles[i].Tick(dt);
            if (_pos.y > Config.groundY) return;
            _pos.y = Config.groundY;
            Splash(_pos, Config.ink, 8, 1f);
            _shake = 0.2f;
            EndRun();
        }

        private void EndRun()
        {
            _state = State.Over;
            int distanceCoins = Mathf.FloorToInt(_pos.x * Config.coinsPerMeter);
            int earned = _runCoins + distanceCoins;
            bool record = _pos.x > Save.BestDistance;
            Save.AddRun(earned, _pos.x);
            _runCoins = 0;
            _hud.ShowOver(_pos.x, earned, Save.BestDistance, record, _force <= 0f ? "Out of force" : "Out of life");
        }

        // ───────────────────────── Golpes ─────────────────────────

        public void Damage(Vector2 at)
        {
            if (_invulnerable > 0f || _state != State.Flight) return;
            _lives--;
            _invulnerable = Config.invulnerableTime;
            _shake = 0.35f;
            Splash(at, Config.danger, 8, 1f);
            _hud.SetLives(_lives);
            _hud.Flash(Config.danger);
        }

        public void Break(Vector2 at, float forceCost, int coins, Color color)
        {
            _force = Mathf.Max(0f, _force - forceCost);
            _runCoins += coins;
            _shake = Mathf.Max(_shake, 0.15f);
            Splash(at, color, 10, 1.1f);
            Splash(at, Config.ink, 6, 0.8f);
            if (coins > 0) _hud.Toast($"+{coins}", Config.reward, small: true);
            StartCoroutine(HitStop(0.035f));
        }

        private IEnumerator HitStop(float seconds)
        {
            if (_paused) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!_paused) Time.timeScale = 1f;
        }

        // ───────────────────────── Obstáculos ─────────────────────────

        public T Spawn<T>(string name, Vector2 pos) where T : Obstacle
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            go.transform.position = pos;
            var o = go.AddComponent<T>();
            o.Init(this);
            _obstacles.Add(o);
            return o;
        }

        private void SpawnAhead()
        {
            float viewRight = _cam.transform.position.x + _cam.orthographicSize * _cam.aspect;
            while (_nextSpawnX < viewRight + 15f)
            {
                SpawnOne(_nextSpawnX);
                _nextSpawnX += Random.Range(Config.minSpacing, Config.maxSpacing);
            }
        }

        private void SpawnOne(float x)
        {
            float total = Config.wallWeight + Config.enemyWeight + Config.targetsWeight;
            float r = Random.value * total;
            float margin = Config.gapHeight * 0.5f + 0.4f;
            if (r < Config.wallWeight)
            {
                var go = new GameObject("WallGap");
                go.transform.SetParent(_world, false);
                go.transform.position = new Vector2(x, 0f);
                var w = go.AddComponent<WallGap>();
                w.GapCenter = Random.Range(Config.groundY + margin, Config.ceilingY - margin);
                w.Init(this);
                _obstacles.Add(w);
            }
            else if (r < Config.wallWeight + Config.enemyWeight)
                Spawn<Enemy>("Enemy", new Vector2(x, Random.Range(Config.groundY + 1.5f, Config.ceilingY - 1.5f)));
            else
                Spawn<TargetCluster>("Targets", new Vector2(x, Random.Range(Config.groundY + 2f, Config.ceilingY - 2f)));
        }

        // ───────────────────────── Construcción ─────────────────────────

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = Config.cameraSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Config.paper;
            _cam.transform.position = new Vector3(Config.aimCameraX, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void BuildLaunchArea()
        {
            _bow = Bow.Create(_world, Config);
            _bow.transform.position = Config.bowPosition;
            _bow.SetScale(Config.bowScale);

            _lantern = Lantern.Create(_world, Config, 1f);

            // Puntos de la trayectoria
            _aimDots.Clear();
            for (int i = 0; i < 16; i++)
            {
                var d = Gfx.Shape("AimDot", InkArt.Blob, _world, Vector2.zero, Vector2.one * 0.16f, Config.inkSoft, 15);
                d.enabled = false;
                _aimDots.Add(d);
            }
        }

        private void BuildArrow()
        {
            _arrow = new GameObject("Arrow").transform;
            _arrow.SetParent(_world, false);
            Color shaft = new Color32(0x2B, 0x24, 0x1F, 0xFF);
            Color steel = new Color32(0x30, 0x34, 0x3A, 0xFF);

            // Plumas rojas y blancas (yabane), astil de bambú oscuro, punta de acero y culatín
            var featherTop = Gfx.Shape("FeatherTop", Gfx.Square, _arrow, new Vector2(-0.88f, 0.075f), new Vector2(0.44f, 0.1f), Config.attack, 9);
            featherTop.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            var featherBottom = Gfx.Shape("FeatherBottom", Gfx.Square, _arrow, new Vector2(-0.88f, -0.075f), new Vector2(0.44f, 0.1f), Config.attack, 9);
            featherBottom.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
            Gfx.Shape("FeatherBand", Gfx.Square, _arrow, new Vector2(-0.86f, 0f), new Vector2(0.09f, 0.28f), Config.paper, 10);
            Gfx.Shape("Shaft", Gfx.Square, _arrow, new Vector2(-0.36f, 0f), new Vector2(1.48f, 0.075f), shaft, 11);
            Gfx.Shape("Nock", Gfx.Square, _arrow, new Vector2(-1.1f, 0f), new Vector2(0.07f, 0.11f), Config.ink, 12);
            _arrowHead = Gfx.Shape("Head", Gfx.Triangle, _arrow, new Vector2(0.49f, 0f), new Vector2(0.26f, 0.2f), steel, 12);

            _arrowParts = _arrow.GetComponentsInChildren<SpriteRenderer>();
            _trail = _arrow.gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = InkArt.SpriteMaterial;
            _trail.time = 0.4f;
            _trail.minVertexDistance = 0.05f;
            _trail.numCapVertices = 3;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.13f), new Keyframe(0.3f, 0.09f), new Keyframe(1f, 0f));
            _trail.sortingOrder = 8;
            _trail.emitting = false;
        }

        private Vector2 TipPosition()
        {
            float a = Mathf.Atan2(_vel.y, Mathf.Max(0.01f, _vel.x));
            return _pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.55f * _arrowScale;
        }

        private void UpdateArrowVisual()
        {
            if (_arrow == null) return;
            _arrow.position = _pos;
            // La flecha es grande en el arco gigante y recupera su tamaño al empezar el vuelo
            if (_state != State.Aim && _state != State.Launch) _arrowScale = Mathf.MoveTowards(_arrowScale, 1f, Time.deltaTime * 4f);
            _arrow.localScale = new Vector3(_arrowScale, _arrowScale, 1f);
            float angle = Mathf.Atan2(_vel.y, Mathf.Max(0.01f, _vel.x)) * Mathf.Rad2Deg;
            _arrow.rotation = Quaternion.Euler(0f, 0f, angle);

            bool blink = _invulnerable > 0f && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
            _arrowHead.color = _attacking ? Config.attack : new Color32(0x30, 0x34, 0x3A, 0xFF);
            _arrowHead.transform.localScale = _attacking ? new Vector3(0.42f, 0.32f, 1f) : new Vector3(0.26f, 0.2f, 1f);
            foreach (var part in _arrowParts) part.enabled = !blink;
            _trail.emitting = _state == State.Launch || _state == State.Flight || _state == State.Falling;
            _trail.widthMultiplier = (_attacking ? 2.4f : 1f) * _arrowScale;
            Color tc = _attacking ? Config.attack : Config.ink;
            _trail.startColor = new Color(tc.r, tc.g, tc.b, 0.9f);
            _trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);

        }

        private void UpdateCamera(float dt)
        {
            float targetX = _state == State.Aim ? Config.aimCameraX : Mathf.Max(Config.aimCameraX, _pos.x + Config.cameraLead);
            var p = _cam.transform.position;
            p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-8f * dt));
            p.y = 0f;
            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - dt);
                Vector2 s = Random.insideUnitCircle * _shake * 0.6f;
                p.x += s.x; p.y += s.y;
            }
            _cam.transform.position = p;
        }

        // Salpicadura de tinta: manchas que salen despedidas y se desvanecen, y una mancha que queda un momento
        private void Splash(Vector2 at, Color color, int count, float power)
        {
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.12f, 0.42f) * power;
                var sr = Gfx.Shape("Ink", InkArt.Blob, _world, at, Vector2.one * size, color, 20);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                StartCoroutine(Fade(sr, Random.insideUnitCircle.normalized * Random.Range(2f, 7f) * power, Random.Range(0.35f, 0.65f)));
            }
            var stain = Gfx.Shape("Stain", InkArt.Blob, _world, at, Vector2.one * 0.9f * power, new Color(color.r, color.g, color.b, 0.55f), 3);
            stain.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            StartCoroutine(Fade(stain, Vector2.zero, 1.4f));
        }

        // Gota de tinta que deja la flecha al volar
        private void Drop(Vector2 at, Color color)
        {
            var sr = Gfx.Shape("Drop", InkArt.Blob, _world, at + Random.insideUnitCircle * 0.06f, Vector2.one * Random.Range(0.07f, 0.16f),
                new Color(color.r, color.g, color.b, 0.7f), 7);
            StartCoroutine(Fade(sr, Vector2.down * Random.Range(0.1f, 0.5f), Random.Range(0.5f, 0.9f)));
        }

        private IEnumerator Fade(SpriteRenderer sr, Vector2 v, float life)
        {
            float t = 0f;
            Color c = sr.color;
            Vector3 scale = sr.transform.localScale;
            while (t < life && sr != null)
            {
                t += Time.deltaTime;
                v *= 1f - 4f * Time.deltaTime;
                sr.transform.position += (Vector3)(v * Time.deltaTime);
                float k = t / life;
                sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k * k));
                sr.transform.localScale = scale * (1f + k * 0.25f);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ───────────────────────── Pausa ─────────────────────────

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _hud.SetPaused(paused);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && (_state == State.Flight || _state == State.Launch)) SetPaused(true);
        }
    }
}
