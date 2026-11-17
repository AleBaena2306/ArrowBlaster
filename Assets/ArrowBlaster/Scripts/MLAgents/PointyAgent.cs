using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ArrowBlaster
{
    // Agente de ML-Agents que juega Pointy Dash. Ve lo mismo que una persona (altura, velocidad, fuerza
    // y los 4 obstáculos siguientes) y en cada decisión elige: nada, impulso o golpe.
    // Sin Python conectado juega con el bot programado (Heuristic), así que también sirve para simular.
    public sealed class PointyAgent : Agent, IBotInput
    {
        private const int Ahead = 4;
        public const int ObservationSize = 4 + Ahead * 7;

        private ArrowGame _game;
        private readonly BotBrain _teacher = new BotBrain(0.8f);
        private bool _flap, _attack;
        private float _lastX;

        // Menú POINTY DASH > Bots > Entrenar con ML-Agents: crea el agente al entrar en Play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool("pd_mlagents", false)) return;
            UnityEditor.SessionState.SetBool("pd_mlagents", false);
            var game = Object.FindFirstObjectByType<ArrowGame>();
            if (game == null) return;
            var go = new GameObject("PointyAgent");
            var bp = go.AddComponent<BehaviorParameters>();
            bp.BehaviorName = "PointyDash";
            bp.BrainParameters.VectorObservationSize = ObservationSize;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3);
            go.AddComponent<PointyAgent>();
            var dr = go.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 2;
            dr.TakeActionsBetweenDecisions = false;   // un toque es un toque, no se repite
#endif
        }

        public override void Initialize()
        {
            _game = FindFirstObjectByType<ArrowGame>();
            _game.Bot = this;
            _game.OnEvent += OnGameEvent;
            Save.TutorialDone = true;
            Time.captureDeltaTime = Time.fixedDeltaTime;   // pasos pequeños y fijos: sin atravesar paredes
            AudioListener.volume = 0f;
        }

        private void OnGameEvent(string id)
        {
            if (id == "hurt") AddReward(-0.3f);
            else if (id == "boss_defeated") AddReward(3f);
        }

        public override void OnEpisodeBegin()
        {
            if (_game.IsOver) _game.NewRun();
            _lastX = 0f;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var cfg = _game.Config;
            Vector2 p = _game.Position, v = _game.Velocity;
            sensor.AddObservation(Mathf.InverseLerp(cfg.groundY, cfg.ceilingY, p.y));
            sensor.AddObservation(Mathf.Clamp(v.y / 12f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp01(_game.Force / cfg.maxForce));
            sensor.AddObservation(_game.Zone / 3f);

            // Los 4 obstáculos más cercanos por delante
            var near = new System.Collections.Generic.List<Obstacle>();
            foreach (var o in _game.Obstacles)
                if (o != null && o.BotKind != Obstacle.Kind.None && o.transform.position.x > p.x - 0.6f) near.Add(o);
            near.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            for (int i = 0; i < Ahead; i++)
            {
                if (i < near.Count)
                {
                    var o = near[i];
                    sensor.AddObservation(Mathf.Clamp01((o.transform.position.x - p.x) / 25f));
                    sensor.AddObservation(Mathf.Clamp((o.BotY - p.y) / 6f, -1f, 1f));
                    for (int k = 1; k <= 5; k++) sensor.AddObservation((int)o.BotKind == k ? 1f : 0f);
                }
                else for (int k = 0; k < 7; k++) sensor.AddObservation(0f);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            int a = actions.DiscreteActions[0];
            _flap = a == 1;
            _attack = a == 2;
            // Recompensa por avanzar; castigo al acabar la run
            AddReward((_game.Distance - _lastX) * 0.01f);
            _lastX = _game.Distance;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            _teacher.Decide(_game, Time.fixedDeltaTime * 2f, out bool flap, out bool attack);
            var d = actionsOut.DiscreteActions;
            d[0] = attack ? 2 : flap ? 1 : 0;
        }

        private void FixedUpdate()
        {
            if (_game != null && _game.IsOver)
            {
                AddReward(-1f);
                EndEpisode();
            }
        }

        // IBotInput: el juego pregunta cada fotograma; la acción decidida se usa una vez
        public void Decide(ArrowGame game, float dt, out bool flap, out bool attack)
        {
            flap = _flap;
            attack = _attack;
            _flap = _attack = false;
        }

        public float AimAngle(ArrowGame game) => _teacher.AimAngle(game);
    }
}
