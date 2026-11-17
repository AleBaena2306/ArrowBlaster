using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArrowBlaster
{
    // Quien controla el juego cuando no es una persona (bot programado o agente de ML-Agents)
    public interface IBotInput
    {
        void Decide(ArrowGame game, float dt, out bool flap, out bool attack);
        float AimAngle(ArrowGame game);
    }

    // Bot programado que juega «como una persona»: solo mira lo que hay en pantalla, reacciona con retraso,
    // no toca más rápido de lo que toca un dedo y a veces se equivoca. skill 0 = torpe, 1 = muy bueno.
    public sealed class BotBrain : IBotInput
    {
        public float Skill;
        private readonly float _reaction, _tapInterval, _miss;
        private float _sinceDecision, _sinceFlap = 1f;
        private float _targetY;
        private bool _wantAttack;

        public BotBrain(float skill)
        {
            Skill = Mathf.Clamp01(skill);
            _reaction = Mathf.Lerp(0.32f, 0.14f, Skill);     // tiempo de reacción humano
            _tapInterval = Mathf.Lerp(0.24f, 0.13f, Skill);  // lo más rápido que toca seguido
            _miss = Mathf.Lerp(0.22f, 0.03f, Skill);         // probabilidad de fallar una decisión
        }

        public float AimAngle(ArrowGame g)
        {
            // Apunta hacia arriba con más o menos tino
            float spread = Mathf.Lerp(18f, 4f, Skill);
            return 18f + Random.Range(-spread, spread);
        }

        public void Decide(ArrowGame g, float dt, out bool flap, out bool attack)
        {
            flap = attack = false;
            if (!g.InFlight) return;
            var cfg = g.Config;
            _sinceFlap += dt;
            _sinceDecision += dt;
            Vector2 p = g.Position, v = g.Velocity;

            // Solo replantea lo que quiere hacer cada «tiempo de reacción»
            if (_sinceDecision >= _reaction)
            {
                _sinceDecision = 0f;
                Plan(g, p, v);
                if (Random.value < _miss) { _targetY += Random.Range(-1.2f, 1.2f); _wantAttack = !_wantAttack && Random.value < 0.3f; }
            }

            // Ataque: un golpe cuando lo ha decidido
            if (_wantAttack) { attack = true; _wantAttack = false; }

            // Altura: predice dónde estará dentro de un instante y toca si se va a quedar corto
            float t = 0.28f;
            float predicted = p.y + v.y * t - 0.5f * cfg.gravity * t * t;
            bool low = predicted < _targetY - 0.25f;
            bool nearGround = p.y < cfg.groundY + 0.9f && v.y < 0f;
            bool nearCeiling = p.y > cfg.ceilingY - 0.6f;
            if ((low || nearGround) && !nearCeiling && _sinceFlap >= _tapInterval)
            {
                flap = true;
                _sinceFlap = 0f;
            }
        }

        private void Plan(ArrowGame g, Vector2 p, Vector2 v)
        {
            var cfg = g.Config;
            float speed = Mathf.Max(4f, v.x);
            float look = speed * Mathf.Lerp(0.9f, 1.6f, Skill);   // cuánto mira hacia delante
            Obstacle next = null;
            float best = float.MaxValue;
            float target = Mathf.Lerp(cfg.groundY, cfg.ceilingY, 0.5f);
            bool attack = false;

            foreach (var o in g.Obstacles)
            {
                if (o == null || o.BotKind == Obstacle.Kind.None) continue;
                float dx = o.transform.position.x - p.x;
                if (o.BotKind == Obstacle.Kind.Boss)
                {
                    target = o.BotY;
                    if (Mathf.Abs(dx) < 7f && Random.value < Mathf.Lerp(0.5f, 0.95f, Skill)) attack = true;
                    next = o;
                    best = -1f;
                    continue;
                }
                if (dx < -0.6f || dx > look) continue;
                // Prioridad: paredes (obligatorio) > evitar > recoger
                float score = dx + (o.BotKind == Obstacle.Kind.Gap ? 0f : o.BotKind == Obstacle.Kind.Avoid ? 2f : 5f);
                if (score < best) { best = score; next = o; }
            }

            if (next != null && next.BotKind != Obstacle.Kind.Boss)
            {
                float dx = next.transform.position.x - p.x;
                switch (next.BotKind)
                {
                    case Obstacle.Kind.Gap:
                        target = next.BotY;
                        // Si no le da tiempo a cuadrarse con el hueco, rompe la pared
                        float timeLeft = dx / Mathf.Max(4f, v.x);
                        if (Mathf.Abs(p.y - next.BotY) > cfg.gapHeight * 0.45f && timeLeft < 0.35f && Random.value < Skill) attack = true;
                        break;
                    case Obstacle.Kind.Avoid:
                        float side = p.y > next.BotY ? 1f : -1f;
                        if (next.BotY + 2.2f > cfg.ceilingY - 0.5f) side = -1f;
                        if (next.BotY - 2.2f < cfg.groundY + 0.5f) side = 1f;
                        target = next.BotY + side * 2.2f;
                        if (dx < 2.5f && Mathf.Abs(p.y - next.BotY) < 0.9f && Random.value < Skill * 0.8f) attack = true;
                        break;
                    case Obstacle.Kind.Collect:
                        target = next.BotY;
                        break;
                }
            }
            _targetY = Mathf.Clamp(target, cfg.groundY + 0.8f, cfg.ceilingY - 0.8f);
            if (attack) _wantAttack = true;
        }
    }

    // Simulador de progresión: varios bots juegan el juego entero desde cero (compran mejoras entre runs)
    // y se mide cuánto tardan en derrotar cada jefe. Resultado en la carpeta BotRuns del proyecto.
    public sealed class BotPlayer : MonoBehaviour
    {
        public static bool Requested;           // lo activa el menú POINTY DASH > Bots
        public int players = 6;
        public float minSkill = 0.35f, maxSkill = 0.8f;
        public float maxHoursPerPlayer = 3f;    // tope por si un bot se atasca
        public float overheadPerRun = 10f;      // segundos reales por run que no son vuelo (apuntar, tienda)

        private ArrowGame _game;
        private int _player = -1, _runs;
        private float _restartIn;
        private readonly StringBuilder _csv = new StringBuilder();
        private readonly StringBuilder _summary = new StringBuilder();
        private readonly List<float> _completions = new List<float>();
        private string _dir;

        public static void Attach(ArrowGame game)
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("pd_bots", false)) { Requested = true; UnityEditor.SessionState.SetBool("pd_bots", false); }
#endif
            if (!Requested) return;
            var bp = game.gameObject.AddComponent<BotPlayer>();
            bp._game = game;
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = 1f / 50f;   // cada fotograma avanza 0,02 s de juego, tan rápido como pueda el PC
            AudioListener.volume = 0f;
            _dir = Path.Combine(Application.dataPath, "..", "BotRuns");
            Directory.CreateDirectory(_dir);
            _csv.AppendLine("player;skill;run;play_minutes;zone_reached;distance;coins;bosses_dead;upgrades");
            _game.OnEvent += OnGameEvent;
            NextPlayer();
        }

        private void OnDestroy()
        {
            Time.captureDeltaTime = 0f;
            if (_game != null) _game.OnEvent -= OnGameEvent;
        }

        private float SkillOf(int i) => players <= 1 ? minSkill : Mathf.Lerp(minSkill, maxSkill, i / (float)(players - 1));
        private float Minutes => (Save.PlayTime + _runs * overheadPerRun) / 60f;

        private void NextPlayer()
        {
            _player++;
            if (_player >= players) { Finish(); return; }
            // Partida nueva desde cero
            PlayerPrefs.DeleteAll();
            Save.TutorialDone = true;
            _runs = 0;
            float skill = SkillOf(_player);
            _game.Bot = new BotBrain(skill);
            _summary.AppendLine($"Bot {_player + 1} (habilidad {skill:0.00}):");
            _game.NewRun();
        }

        private void OnGameEvent(string id)
        {
            if (id == "boss_defeated")
                _summary.AppendLine($"  jefe zona {_game.Zone + 1} derrotado a los {Minutes:0.0} min (run {_runs + 1})");
        }

        private void Update()
        {
            if (_game == null || _player >= players) return;
            if (!_game.IsOver) return;
            if (_restartIn <= 0f) { OnRunEnded(); _restartIn = 0.05f; return; }
            _restartIn -= Time.unscaledDeltaTime;
            if (_restartIn <= 0f) _game.NewRun();
        }

        private void OnRunEnded()
        {
            _runs++;
            int dead = 0;
            for (int z = 0; z < _game.Config.zoneLengths.Length; z++) if (Save.BossDead(z)) dead++;
            // Compra como una persona: lo más barato primero, hasta quedarse sin monedas
            for (int guard = 0; guard < 50; guard++)
            {
                Stat? cheapest = null;
                foreach (Stat s in System.Enum.GetValues(typeof(Stat)))
                    if (!Upgrades.IsMax(s) && (cheapest == null || Upgrades.Cost(s) < Upgrades.Cost(cheapest.Value))) cheapest = s;
                if (cheapest == null || !Upgrades.TryBuy(cheapest.Value)) break;
            }
            var ups = new StringBuilder();
            foreach (Stat s in System.Enum.GetValues(typeof(Stat))) ups.Append(s).Append('=').Append(Upgrades.Level(s)).Append(' ');
            // Punto y coma: Excel en español usa la coma para los decimales
            _csv.AppendLine($"{_player + 1};{SkillOf(_player):0.00};{_runs};{Minutes:0.0};{_game.Zone + 1};{_game.Distance:0};{_game.LastRunCoins};{dead};{ups.ToString().Trim()}");

            bool beaten = Save.Loop > 0;
            bool timeout = Minutes > maxHoursPerPlayer * 60f;
            if (beaten || timeout)
            {
                _summary.AppendLine(beaten ? $"  JUEGO COMPLETADO en {Minutes:0.0} min ({_runs} runs)" : $"  NO lo completa en {maxHoursPerPlayer:0} h (llega a la zona {Save.Zone + 1})");
                if (beaten) _completions.Add(Minutes);
                File.WriteAllText(Path.Combine(_dir, "runs.csv"), _csv.ToString());
                NextPlayer();
                _restartIn = 0f;
            }
        }

        private void Finish()
        {
            _completions.Sort();
            _summary.AppendLine();
            if (_completions.Count > 0)
            {
                float avg = 0f; foreach (var c in _completions) avg += c; avg /= _completions.Count;
                _summary.AppendLine($"Completan {_completions.Count}/{players}. Media {avg:0} min · mínimo {_completions[0]:0} · máximo {_completions[_completions.Count - 1]:0}. Objetivo: 60–120 min.");
            }
            else _summary.AppendLine($"Ningún bot completa el juego en {maxHoursPerPlayer:0} h.");
            File.WriteAllText(Path.Combine(_dir, "runs.csv"), _csv.ToString());
            File.WriteAllText(Path.Combine(_dir, "resumen.txt"), _summary.ToString());
            Debug.Log("[Pointy Dash · Bots]\n" + _summary);
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
