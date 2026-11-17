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
        private SpriteRenderer _arrowOutline, _arrowFill;
        private Renderer[] _arrowRenderers;
        private TrailRenderer _trail;
        private Bow _bow;
        private Lantern _lantern;
        private readonly List<SpriteRenderer> _aimDots = new List<SpriteRenderer>();
        private float _dropTimer;
        private Scenery _scenery;

        // Tutorial de la primera run: -1 = no hay; 0 apuntar; 1 impulso; 2 esperar obstáculo; 3 golpe
        private int _tutStep = -1;
        private bool _slowMo;
        private float BaseTimeScale => _slowMo ? 0.25f : 1f;

        // Zonas y jefes
        private int _zone;
        private Boss _boss;
        private bool _bossDone;
        private float _attackTimer, _attackCooldown;
        private float ZoneLength => Config.zoneLengths[Mathf.Clamp(_zone, 0, Config.zoneLengths.Length - 1)];
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
        public bool IsAttacking => _attacking && _state == State.Flight;

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
            Art.P = Config.Palette(Mathf.Clamp(Save.Zone, 0, Config.zoneLengths.Length - 1));
            _scenery = Scenery.Create(_cam, Config);
            _hud = new Hud(this, Config);
        }

        private void Start() => NewRun();

        // ───────────────────────── Run ─────────────────────────

        public void NewRun()
        {
            if (_world != null) Destroy(_world.gameObject);
            _obstacles.Clear();
            _world = new GameObject("World").transform;

            // Cada zona tiene su paleta: se aplica antes de construir nada
            Art.P = Config.Palette(Mathf.Clamp(Save.Zone, 0, Config.zoneLengths.Length - 1));
            _scenery.SetPalette(Art.P);

            BuildLaunchArea();
            BuildArrow();

            _pos = new Vector2(0f, 0f);
            _vel = Vector2.zero;
            _force = 0f;
            _lives = Config.lives + Upgrades.ExtraLives;
            _runCoins = 0;
            _dragging = false;
            _draw = _drawShown = 0f;
            _aimAngle = 15f;
            _arrowScale = 1.25f;
            _invulnerable = 0f;
            _attacking = false;
            _launchMultiplier = 1f;
            _nextSpawnX = Config.firstObstacleX;
            _zone = Mathf.Clamp(Save.Zone, 0, Config.zoneLengths.Length - 1);
            _boss = null;
            _bossDone = false;
            _attackTimer = _attackCooldown = 0f;
            _state = State.Aim;
            _tutStep = Save.TutorialDone ? -1 : 0;
            _slowMo = false;
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

            UpdateTutorial();
            UpdateArrowVisual();
            UpdateCamera(dt);
            if (_boss != null) _hud.SetProgress(_boss.Hp / _boss.MaxHp, true, "BOSS");
            else if (_bossDone) _hud.SetProgress(1f, false, "ZONE CLEARED");
            else _hud.SetProgress(Mathf.Clamp01(_pos.x / ZoneLength), false, Save.Loop > 0 ? $"ZONE {_zone + 1} · NG+{Save.Loop}" : $"ZONE {_zone + 1}");
            _hud.Tick(_force, _state == State.Aim ? 1f : _startForce, _pos.x, Save.Coins + _runCoins,
                draining: _attacking && _state == State.Flight, aiming: _state == State.Aim);
            _hud.SetAttackZone(_state == State.Flight && !_paused, Config.attackZone);
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

            // En vuelo: tocar la franja izquierda (attackZone) = golpe; el resto de la pantalla = impulso.
            // Teclado: Espacio = impulso; Shift, X o Z = golpe.
            _flapPressed = Input.GetKeyDown(KeyCode.Space);
            _attackPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
                             || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Z);
            float edge = Screen.width * Config.attackZone;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                    if (t.position.x < edge) _attackPressed = true; else _flapPressed = true;
                }
            }
            else if (Input.GetMouseButtonDown(0) && !PointerOverUi())
            {
                if (Input.mousePosition.x < edge) _attackPressed = true; else _flapPressed = true;
            }
        }

        private bool _flapPressed, _attackPressed;

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
        private const float ArrowTail = 0.3f;    // distancia del centro del puntero a su base (escala 1)

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
                d.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.25f, t));
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
                if (off <= Config.lanternCenterRadius) BeginFlight(Config.centerMultiplier + Upgrades.TargetCenterBonus, "PERFECT", true);
                else if (off <= Config.lanternRadius) BeginFlight(Config.ringMultiplier + Upgrades.TargetRingBonus, "GOOD", true);
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
            _force = Mathf.Min(Config.initialForce * Upgrades.PowerMultiplier * multiplier * Mathf.Lerp(0.7f, 1f, _launchDraw), Config.maxForce);
            _startForce = Mathf.Max(1f, _force);
            bool perfect = label == "PERFECT";
            _hud.Toast($"{label}  ×{multiplier:0.0}", perfect ? Config.reward : Config.uiInk);
            if (hitLantern)
            {
                _lantern.Hit(perfect);
                Splash(_pos, new Color32(0xFF, 0x4D, 0x5E, 0xFF), perfect ? 10 : 5, perfect ? 1.3f : 0.9f);
                Splash(_pos, Color.white, perfect ? 10 : 5, 1f);
                _shake = perfect ? 0.3f : 0.18f;
                StartCoroutine(HitStop(perfect ? 0.08f : 0.04f));
            }
            _attacking = false;
            _vel = new Vector2(Speed(), Mathf.Clamp(_vel.y, -3f, 5f));
            _state = State.Flight;
            _hud.SetHint("Tap right to fly · Tap the red edge to attack");
            StartCoroutine(HideHintLater(3f));
        }

        private IEnumerator HideHintLater(float t)
        {
            yield return new WaitForSeconds(t);
            if (_state == State.Flight && _tutStep < 0) _hud.SetHint("");
        }

        private float _startForce = 1f;

        // Atacar: la flecha va en línea recta y más rápida, pero gasta fuerza a cada segundo
        private float Speed() => (Config.minSpeed + _force * Config.speedPerForce) * Upgrades.SpeedMultiplier
                                 * (_attacking ? Config.attackSpeedMultiplier : 1f);

        private void UpdateFlight(float dt)
        {
            // Entrada: tocar = impulso; mantener = atacar (planeo)
            // Impulso al tocar la derecha; golpe de duración fija al tocar la franja roja de la izquierda
            if (_flapPressed) _vel.y = Config.flapVelocity;
            if (_attackCooldown > 0f) _attackCooldown -= dt;
            if (_attackTimer > 0f)
            {
                _attackTimer -= dt;
                if (_attackTimer <= 0f) _attackCooldown = Config.attackCooldown;
            }
            else if (_attackPressed && _attackCooldown <= 0f)
            {
                _attackTimer = Config.attackDuration;
                _force = Mathf.Max(0f, _force - Config.attackForceCost * Upgrades.AttackCostMultiplier);
                _shake = Mathf.Max(_shake, 0.08f);
            }
            _attacking = _attackTimer > 0f;

            if (_attacking) _vel.y = Mathf.MoveTowards(_vel.y, 0f, 60f * dt); // línea recta
            else _vel.y -= Config.gravity * dt;

            float drain = Config.forceDecayPerSecond * Upgrades.DecayMultiplier;
            _force = Mathf.Max(0f, _force - drain * dt);
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
            if (_dropTimer <= 0f && _attacking)
            {
                _dropTimer = 0.03f;
                Drop(_pos - _vel.normalized * ArrowTail * _arrowScale, Art.P.trail);
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

            if (_state == State.Flight && _force <= 0f) StartFalling();
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
            Splash(_pos, Color.white, 8, 1f);
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
            _hud.ShowOver(_pos.x, earned, Save.BestDistance, record, "Out of force");
        }

        // ───────────────────────── Golpes ─────────────────────────

        public void Damage(Vector2 at)
        {
            if (_invulnerable > 0f || _state != State.Flight) return;
            // Una sola barra: cada golpe quita fuerza (velocidad y vida a la vez)
            _force = Mathf.Max(0f, _force - Config.hitForceCost * Upgrades.HitCostMultiplier);
            _invulnerable = Config.invulnerableTime;
            _shake = 0.35f;
            Splash(at, Config.danger, 8, 1f);
            _hud.SetLives(_lives);
            _hud.Flash(Config.danger);
        }

        public void Break(Vector2 at, float forceCost, int coins, Color color)
        {
            _force = Mathf.Max(0f, _force - forceCost * Upgrades.AttackCostMultiplier);
            _runCoins += coins;
            _shake = Mathf.Max(_shake, 0.15f);
            Splash(at, color, 10, 1.1f);
            Splash(at, Color.white, 6, 0.8f);
            if (coins > 0) _hud.Toast($"+{coins}", Config.reward, small: true);
            StartCoroutine(HitStop(0.035f));
        }

        // ───────────────────────── Jefes y partículas ─────────────────────────

        private void SpawnBoss()
        {
            var go = new GameObject("Boss");
            go.transform.SetParent(_world, false);
            go.transform.position = new Vector2(_pos.x + 20f, 0f);
            var b = go.AddComponent<Boss>();
            b.Zone = _zone;
            b.MaxHp = Config.bossHp[Mathf.Clamp(_zone, 0, Config.bossHp.Length - 1)] * (1 + Save.Loop);
            float saved = Save.BossHp(_zone);
            b.Hp = saved > 0f ? Mathf.Min(saved, b.MaxHp) : b.MaxHp;
            b.Init(this);
            _obstacles.Add(b);
            _boss = b;
            _hud.Toast("BOSS!", Config.attack);
        }

        // Golpe atacando al jefe. Devuelve true si cae.
        public bool HitBoss(Boss boss, Vector2 at)
        {
            if (_invulnerable > 0f) return false;
            float dmg = Config.bossDamage * Upgrades.BossDamageMultiplier;
            boss.Hp = Mathf.Max(0f, boss.Hp - dmg);
            boss.Knockback(3f);
            Break(at, Config.breakForceCost, 0, Config.attack);
            _invulnerable = 0.15f;
            if (boss.Hp > 0f) { Save.SetBossHp(_zone, boss.Hp); return false; }

            // Jefe derrotado: recompensa, siguiente zona (o vuelta de New Game+ con jefes más duros)
            int reward = Config.bossReward[Mathf.Clamp(_zone, 0, Config.bossReward.Length - 1)] * (1 + Save.Loop);
            _runCoins += reward;
            Save.SetBossHp(_zone, -1f);
            int next = _zone + 1;
            if (next >= Config.zoneLengths.Length) { next = 0; Save.Loop = Save.Loop + 1; }
            Save.Zone = next;
            _bossDone = true;
            _boss = null;
            _nextSpawnX = _pos.x + 25f;
            Splash(boss.transform.position, Config.attack, 18, 2f);
            Splash(boss.transform.position, Color.white, 14, 1.6f);
            _shake = 0.6f;
            StartCoroutine(HitStop(0.15f));
            _hud.Toast($"BOSS DEFEATED!  +{reward}", Config.attack);
            return true;
        }

        public void Pickup(Vector2 at, bool good)
        {
            if (good)
            {
                _force = Mathf.Min(Config.maxForce, _force + Config.pickupForce);
                Splash(at, Config.reward, 4, 0.5f);
            }
            else
            {
                _force = Mathf.Max(0f, _force - Config.pickupForce);
                Splash(at, Color.white, 5, 0.6f);
                _shake = Mathf.Max(_shake, 0.12f);
            }
        }

        // Toque dorado recogido: monedas sin coste de fuerza ni parón
        public void Collect(Vector2 at, int coins)
        {
            _runCoins += coins;
            Splash(at, Config.reward, 5, 0.6f);
            _hud.Toast($"+{coins}", Config.reward, small: true);
        }

        private IEnumerator HitStop(float seconds)
        {
            if (_paused) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!_paused) Time.timeScale = BaseTimeScale;
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
            // Jefe al final de la zona; antes de él no se ponen más obstáculos
            if (!_bossDone && _boss == null && _pos.x >= ZoneLength - 14f) SpawnBoss();
            while (_nextSpawnX < viewRight + 15f)
            {
                if (!_bossDone && _nextSpawnX > ZoneLength - 22f) break;
                SpawnOne(_nextSpawnX);
                _nextSpawnX += Random.Range(Config.minSpacing, Config.maxSpacing);
            }
        }

        private void SpawnOne(float x)
        {
            float total = Config.wallWeight + Config.enemyWeight + Config.targetsWeight + Config.pickupsWeight;
            float r = Random.value * total;
            if (_tutStep >= 0) r = 0f; // durante el tutorial, solo paredes (para enseñar el golpe)
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
            else if (r < Config.wallWeight + Config.enemyWeight + Config.targetsWeight)
                Spawn<TargetCluster>("Targets", new Vector2(x, Random.Range(Config.groundY + 2f, Config.ceilingY - 2f)));
            else
                Spawn<PickupCluster>("Pickups", new Vector2(x, Random.Range(Config.groundY + 2.2f, Config.ceilingY - 2.2f)));
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
            _cam.backgroundColor = Art.P.skyBottom;
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

            _lantern = Lantern.Create(_world, Config, Upgrades.TargetSpeedMultiplier);

            // Puntos de la trayectoria
            _aimDots.Clear();
            for (int i = 0; i < 16; i++)
            {
                var d = Art.Shape("circle", _world, Vector2.zero, 0.16f, Color.white, 15);
                d.enabled = false;
                _aimDots.Add(d);
            }
        }

        private void BuildArrow()
        {
            _arrow = new GameObject("Arrow").transform;
            _arrow.SetParent(_world, false);
            // Puntero hipercasual (ShapeForge): triángulo con contorno del color de la estela.
            // La punta queda a 0,55 del centro (radio del contorno = 116/256 del alto del sprite).
            const float size = 0.55f * 256f / 116f;
            _arrowOutline = Art.Shape("pointer_outline", _arrow, Vector2.zero, size, Art.P.trail, 11);
            _arrowFill = Art.Shape("pointer_fill", _arrow, Vector2.zero, size, Art.P.pointer, 12);
            _arrowRenderers = _arrow.GetComponentsInChildren<Renderer>();

            // Estela: cinta ancha que se afila y se desvanece
            _trail = _arrow.gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = Art.LineMaterial;
            _trail.time = 0.45f;
            _trail.minVertexDistance = 0.05f;
            _trail.numCapVertices = 6;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.62f), new Keyframe(0.3f, 0.45f), new Keyframe(1f, 0f));
            _trail.sortingOrder = 9;
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
            foreach (var part in _arrowRenderers)
                if (!(part is TrailRenderer)) part.enabled = !blink;
            // Al atacar, el relleno se pone rojo (solo la cabeza) y la estela se ensancha
            _arrowFill.color = _attacking ? Config.attack : Art.P.pointer;
            _arrowOutline.color = Art.P.trail;
            _trail.emitting = _state == State.Launch || _state == State.Flight || _state == State.Falling;
            _trail.widthMultiplier = (_attacking ? 1.35f : 1f) * _arrowScale;
            Color tc = Art.P.trail;
            _trail.startColor = new Color(tc.r, tc.g, tc.b, 0.95f);
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

        // Estallido hipercasual: un aro que se expande y bolitas que salen despedidas encogiéndose
        private void Splash(Vector2 at, Color color, int count, float power)
        {
            var ring = Art.Shape("ring", _world, at, 0.6f * power, color, 19);
            StartCoroutine(Pop(ring, 0.3f, 2.4f));
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var dot = Art.Shape(i % 3 == 0 ? "spark" : "circle", _world, at + dir * 0.2f, Random.Range(0.12f, 0.3f) * power, color, 20);
                StartCoroutine(Fly(dot, dir * Random.Range(3f, 7f) * power, Random.Range(0.3f, 0.55f)));
            }
        }

        // Bolitas de la estela al atacar
        private void Drop(Vector2 at, Color color)
        {
            var dot = Art.Shape("circle", _world, at + Random.insideUnitCircle * 0.15f, Random.Range(0.08f, 0.18f), color, 7);
            StartCoroutine(Fly(dot, Random.insideUnitCircle * 0.8f, Random.Range(0.3f, 0.5f)));
        }

        // Escala de 0,4 a «grow» y se desvanece
        private IEnumerator Pop(SpriteRenderer sr, float life, float grow)
        {
            float t = 0f;
            Vector3 s0 = sr.transform.localScale;
            Color c = sr.color;
            while (t < life && sr != null)
            {
                t += Time.deltaTime;
                float k = t / life;
                sr.transform.localScale = s0 * Mathf.Lerp(0.4f, grow, 1f - (1f - k) * (1f - k));
                sr.color = new Color(c.r, c.g, c.b, 1f - k);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // Sale despedida, frena y encoge hasta desaparecer
        private IEnumerator Fly(SpriteRenderer sr, Vector2 v, float life)
        {
            float t = 0f;
            Vector3 s0 = sr.transform.localScale;
            while (t < life && sr != null)
            {
                t += Time.deltaTime;
                v *= 1f - 5f * Time.deltaTime;
                sr.transform.position += (Vector3)(v * Time.deltaTime);
                sr.transform.Rotate(0f, 0f, 360f * Time.deltaTime);
                sr.transform.localScale = s0 * (1f - t / life);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        // ───────────────────────── Tutorial ─────────────────────────
        // Solo la primera vez: un dedo animado enseña a apuntar; después, a cámara lenta, el impulso
        // (tocar a la derecha) y el golpe (tocar la franja roja) justo antes de la primera pared.

        private void UpdateTutorial()
        {
            switch (_tutStep)
            {
                case 0:
                    if (_state == State.Aim)
                    {
                        Vector2 vp = _cam.WorldToViewportPoint(Config.bowPosition);
                        _hud.SetTouch(!_dragging, vp, new Vector2(-260f, -150f));
                    }
                    else if (_state == State.Flight) { _tutStep = 1; SetSlowMo(true); _hud.SetHint("TAP on the right to fly up"); }
                    else _hud.SetTouch(false, default, default);
                    break;
                case 1:
                    _hud.SetTouch(true, new Vector2(0.65f, 0.42f), Vector2.zero);
                    if (_flapPressed || _state != State.Flight) { _tutStep = 2; SetSlowMo(false); _hud.SetHint(""); }
                    break;
                case 2:
                    _hud.SetTouch(false, default, default);
                    if (_state == State.Flight && ObstacleAhead(6.5f)) { _tutStep = 3; SetSlowMo(true); _hud.SetHint("TAP the red edge to SMASH"); }
                    break;
                case 3:
                    _hud.SetTouch(true, new Vector2(Config.attackZone * 0.5f, 0.42f), Vector2.zero);
                    _hud.PulseAttackZone = true;
                    if (_attackPressed || _state != State.Flight)
                    {
                        if (_attackPressed) Save.TutorialDone = true;
                        _tutStep = -1;
                        SetSlowMo(false);
                        _hud.SetHint("");
                        _hud.PulseAttackZone = false;
                    }
                    break;
                default:
                    _hud.SetTouch(false, default, default);
                    break;
            }
        }

        private void SetSlowMo(bool on)
        {
            _slowMo = on;
            if (!_paused) Time.timeScale = BaseTimeScale;
        }

        private bool ObstacleAhead(float distance)
        {
            foreach (var o in _obstacles)
            {
                if (o == null || o is Projectile || o is Boss) continue;
                float dx = o.transform.position.x - _pos.x;
                if (dx > 0.5f && dx < distance) return true;
            }
            return false;
        }

        // ───────────────────────── Pausa ─────────────────────────

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : BaseTimeScale;
            _hud.SetPaused(paused);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && (_state == State.Flight || _state == State.Launch)) SetPaused(true);
        }
    }
}
