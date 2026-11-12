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
        private Transform _aimLine;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private float _nextSpawnX;

        // Estado de la run
        private Vector2 _pos, _vel;
        private float _force, _aimTime, _launchY, _holdTime, _invulnerable, _shake;
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
            _aimTime = 0f;
            _invulnerable = 0f;
            _attacking = false;
            _launchMultiplier = 1f;
            _nextSpawnX = Config.firstObstacleX;
            _state = State.Aim;
            _hud.ShowRun(_lives);
            _hud.SetHint("Tap to shoot");
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

        // Apuntar: la flecha sube y baja delante de la diana; un toque dispara.
        private void UpdateAim(float dt)
        {
            _aimTime += dt;
            _pos = new Vector2(0f, Mathf.Sin(_aimTime * Mathf.PI * 2f * Config.aimSpeed) * Config.aimRange);
            _vel = Vector2.right;
            if (_aimLine != null) _aimLine.position = new Vector3(Config.targetX * 0.5f, _pos.y, 0f);
            if (!_pressedThisFrame) return;
            _launchY = _pos.y;
            _state = State.Launch;
            _vel = new Vector2(28f, 0f);
            if (_aimLine != null) _aimLine.gameObject.SetActive(false);
            _hud.SetHint("");
        }

        // La flecha vuela recta hasta la diana; al cruzarla se decide el multiplicador de fuerza.
        private void UpdateLaunch(float dt)
        {
            _pos += _vel * dt;
            if (_pos.x < Config.targetX) return;

            float off = Mathf.Abs(_launchY);
            string label;
            Color color;
            if (off <= Config.targetCenterHalf) { _launchMultiplier = Config.centerMultiplier; label = "PERFECT"; color = Config.attack; }
            else if (off <= Config.targetHalfHeight) { _launchMultiplier = Config.ringMultiplier; label = "GOOD"; color = Config.ink; }
            else { _launchMultiplier = Config.missMultiplier; label = "MISS"; color = Config.ink; }

            _force = Mathf.Min(Config.initialForce * _launchMultiplier, Config.maxForce);
            _hud.Toast($"{label}  ×{_launchMultiplier:0.0}", color);
            if (_launchMultiplier > 1f) { Splash(_pos, Config.ink, 10, 1.3f); _shake = 0.25f; }
            _vel = new Vector2(Speed(), 2f);
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
            foreach (var o in _obstacles) if (o != null) o.Tick(dt);
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
            _cam.transform.position = new Vector3(Config.targetX * 0.5f, 0f, -10f);
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
            // Suelo de tinta (se repite siguiendo a la cámara)
            var ground = Gfx.Shape("Ground", Gfx.Square, _world, new Vector2(0f, Config.groundY - 1f), new Vector2(4000f, 2f), Config.ink, 1);
            ground.transform.localPosition = new Vector2(1900f, Config.groundY - 1f);

            // Arco
            Gfx.Shape("Bow", Gfx.Square, _world, new Vector2(-0.9f, 0f), new Vector2(0.18f, Config.aimRange * 2f + 1f), Config.ink, 4);

            // Diana vertical: anillo exterior, centro de color
            float h = Config.targetHalfHeight;
            var target = new GameObject("LaunchTarget").transform;
            target.SetParent(_world, false);
            target.localPosition = new Vector2(Config.targetX, 0f);
            Gfx.Shape("Outer", Gfx.Square, target, Vector2.zero, new Vector2(0.5f, h * 2f), Config.ink, 3);
            Gfx.Shape("Ring", Gfx.Square, target, Vector2.zero, new Vector2(0.36f, h * 1.4f), Config.paper, 4);
            Gfx.Shape("Center", Gfx.Square, target, Vector2.zero, new Vector2(0.5f, Config.targetCenterHalf * 2f), Config.attack, 5);

            // Guía de puntería
            _aimLine = Gfx.Shape("AimLine", Gfx.Square, _world, Vector2.zero, new Vector2(Config.targetX, 0.04f), Config.inkSoft, 2).transform;
        }

        private void BuildArrow()
        {
            _arrow = new GameObject("Arrow").transform;
            _arrow.SetParent(_world, false);
            var shaft = Gfx.Shape("Shaft", Gfx.Square, _arrow, new Vector2(-0.45f, 0f), new Vector2(1.1f, 0.09f), Config.ink, 10);
            _arrowHead = Gfx.Shape("Head", Gfx.Triangle, _arrow, new Vector2(0.22f, 0f), new Vector2(0.34f, 0.3f), Config.ink, 11);
            Gfx.Shape("Fletch", Gfx.Square, _arrow, new Vector2(-0.95f, 0f), new Vector2(0.22f, 0.24f), Config.ink, 10);

            _arrowParts = _arrow.GetComponentsInChildren<SpriteRenderer>();
            _trail = _arrow.gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = shaft.sharedMaterial;
            _trail.time = 0.35f;
            _trail.minVertexDistance = 0.05f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
            _trail.startColor = Config.ink;
            _trail.endColor = new Color(Config.ink.r, Config.ink.g, Config.ink.b, 0f);
            _trail.sortingOrder = 9;
        }

        private Vector2 TipPosition()
        {
            float a = Mathf.Atan2(_vel.y, Mathf.Max(0.01f, _vel.x));
            return _pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.35f;
        }

        private void UpdateArrowVisual()
        {
            if (_arrow == null) return;
            _arrow.position = _pos;
            float angle = Mathf.Atan2(_vel.y, Mathf.Max(0.01f, _vel.x)) * Mathf.Rad2Deg;
            _arrow.rotation = Quaternion.Euler(0f, 0f, angle);

            bool blink = _invulnerable > 0f && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
            _arrowHead.color = _attacking ? Config.attack : Config.ink;
            _arrowHead.transform.localScale = _attacking ? new Vector3(0.5f, 0.44f, 1f) : new Vector3(0.34f, 0.3f, 1f);
            foreach (var part in _arrowParts) part.enabled = !blink;
            _trail.widthMultiplier = _attacking ? 2.2f : 1f;
            _trail.startColor = _attacking ? Config.attack : Config.ink;
        }

        private void UpdateCamera(float dt)
        {
            float targetX = _state == State.Aim || _state == State.Launch && _pos.x < Config.targetX
                ? Config.targetX * 0.5f
                : _pos.x + Config.cameraLead;
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

        // Salpicadura de tinta de prueba: círculos que salen despedidos y se desvanecen
        private void Splash(Vector2 at, Color color, int count, float power)
        {
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.12f, 0.38f) * power;
                var sr = Gfx.Shape("Ink", Gfx.Circle, _world, at, Vector2.one * size, color, 20);
                StartCoroutine(SplashDrop(sr, Random.insideUnitCircle.normalized * Random.Range(2f, 6f) * power));
            }
        }

        private IEnumerator SplashDrop(SpriteRenderer sr, Vector2 v)
        {
            float t = 0f, life = Random.Range(0.35f, 0.6f);
            Color c = sr.color;
            while (t < life && sr != null)
            {
                t += Time.deltaTime;
                v *= 1f - 4f * Time.deltaTime;
                sr.transform.position += (Vector3)(v * Time.deltaTime);
                sr.color = new Color(c.r, c.g, c.b, c.a * (1f - t / life));
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
