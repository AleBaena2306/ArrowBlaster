using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Obstáculos del vuelo. Sin físicas de Unity: cada uno comprueba a mano si la punta de la flecha lo toca.
    // Reglas:
    //  - Chocar sin atacar: quita una vida (pilares, enemigos, proyectiles).
    //  - Chocar atacando: rompe lo que toca y cuesta fuerza.
    //  - Los toques dorados no hacen daño: dan monedas al tocarlos.
    // Estilo: tinta negra sobre papel; lo peligroso, en color.
    public abstract class Obstacle : MonoBehaviour
    {
        protected ArrowGame Game;
        protected ArrowConfig Cfg => Game.Config;

        public virtual float RightEdge => transform.position.x + 2f;

        public void Init(ArrowGame game) { Game = game; Build(); }
        protected abstract void Build();
        public virtual void Tick(float dt) { }
        // Devuelve true si el obstáculo ya no existe (se ha roto entero)
        public abstract bool CheckHit(Vector2 tip, float radius, bool attacking);

        protected static bool CircleRect(Vector2 c, float r, Rect rect)
        {
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        protected static bool CircleCircle(Vector2 a, float ra, Vector2 b, float rb)
        {
            float d = ra + rb;
            return (a - b).sqrMagnitude <= d * d;
        }


    }

    // Dos trazos verticales de color con un hueco: hay que pasar por el hueco o romperlos atacando.
    public sealed class WallGap : Obstacle
    {
        public float GapCenter;
        private readonly List<(Transform root, Rect rect)> _parts = new List<(Transform, Rect)>();

        public override float RightEdge => transform.position.x + Cfg.wallWidth;

        protected override void Build()
        {
            float x = transform.position.x, w = Cfg.wallWidth;
            float gapTop = GapCenter + Cfg.gapHeight * 0.5f, gapBottom = GapCenter - Cfg.gapHeight * 0.5f;
            AddPart(new Rect(x - w * 0.5f, gapTop, w, Cfg.ceilingY + 3f - gapTop), capAtBottom: true);
            AddPart(new Rect(x - w * 0.5f, Cfg.groundY - 1f, w, gapBottom - (Cfg.groundY - 1f)), capAtBottom: false);
        }

        private void AddPart(Rect r, bool capAtBottom)
        {
            if (r.height <= 0.05f) return;
            var root = new GameObject("Pillar").transform;
            root.SetParent(transform, false);
            root.position = r.center;

            // Columna de Kenney (Scribble Platformer Expansion) repetida en vertical, en tinta,
            // con una pincelada roja en el extremo que da al hueco (lo peligroso, en color)
            var col = KenneyArt.Shape("column_middle", root, Vector2.zero, 1f, Cfg.ink, 6);
            col.drawMode = SpriteDrawMode.Tiled;
            col.transform.localScale = Vector3.one;
            col.size = new Vector2(r.width * 128f / 88f, r.height);
            float h = r.height * 0.5f;
            float capY = capAtBottom ? -h + 0.12f : h - 0.12f;
            Brush.Line("Cap", root, new Vector2(-r.width * 0.75f, capY), new Vector2(r.width * 0.75f, capY), 0.3f, Cfg.danger, 8, bend: 0.08f, dry: 0.8f);
            _parts.Add((root, r));
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                if (!CircleRect(tip, radius, _parts[i].rect)) continue;
                if (attacking) Game.Break(tip, Cfg.breakForceCost, Cfg.coinsPerWall, Cfg.danger);
                else Game.Damage(tip);
                Destroy(_parts[i].root.gameObject);
                _parts.RemoveAt(i);
            }
            return _parts.Count == 0;
        }
    }

    // Mancha de color con ojos y cuernos de tinta (un pequeño oni) que dispara bolas hacia la flecha.
    public sealed class Enemy : Obstacle
    {
        private const float Radius = 0.6f;
        private float _fireTimer, _phase, _baseY;
        private Transform _body;

        protected override void Build()
        {
            _baseY = transform.position.y;
            _phase = Random.value * 10f;
            _body = new GameObject("Enemy").transform;
            _body.SetParent(transform, false);
            // Personaje redondo de Kenney (Scribble Platformer); conserva su color
            string[] looks = { "character_roundRed", "character_roundGreen", "character_roundPurple", "character_roundYellow" };
            KenneyArt.Shape(looks[Random.Range(0, looks.Length)], _body, Vector2.zero, Radius * 3.1f, Cfg.ink, 5, keepColor: true);

            _fireTimer = Cfg.enemyFireInterval * 0.5f;
        }

        public override void Tick(float dt)
        {
            _phase += dt;
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(_phase * 2.4f) * 0.25f;
            transform.position = p;
            _body.localScale = new Vector3(1f + Mathf.Sin(_phase * 4.8f) * 0.04f, 1f - Mathf.Sin(_phase * 4.8f) * 0.04f, 1f);


            float ahead = p.x - Game.ArrowPosition.x;
            if (ahead < 1f || ahead > Cfg.enemyRange) return;
            _fireTimer -= dt;
            if (_fireTimer > 0f) return;
            _fireTimer = Cfg.enemyFireInterval;
            _body.localScale = new Vector3(1.2f, 0.85f, 1f);
            Game.Spawn<Projectile>("Projectile", (Vector2)p + Vector2.left * Radius);
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            if (!CircleCircle(tip, radius, transform.position, Radius)) return false;
            if (attacking) Game.Break(tip, Cfg.breakForceCost, Cfg.coinsPerEnemy, Cfg.enemy);
            else Game.Damage(tip);
            return true;
        }
    }

    public sealed class Projectile : Obstacle
    {
        private const float Radius = 0.25f;
        public override float RightEdge => transform.position.x + Radius;

        protected override void Build()
        {
            var shot = KenneyArt.Shape("effect_shot", transform, Vector2.zero, Radius * 5f, Cfg.ink, 7, keepColor: true);
            shot.flipX = true; // la estela del disparo queda detrás (va hacia la izquierda)
        }

        public override void Tick(float dt)
        {
            transform.position += Vector3.left * (Cfg.projectileSpeed * dt);

        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            if (!CircleCircle(tip, radius, transform.position, Radius)) return false;
            if (attacking) Game.Break(tip, Cfg.projectileForceCost, 0, Cfg.enemy);
            else Game.Damage(tip);
            return true;
        }
    }

    // Grupo de toques dorados: dan monedas al tocarlos (sin necesidad de atacar) y no hacen daño.
    public sealed class TargetCluster : Obstacle
    {
        private const float Radius = 0.38f;
        private readonly List<Transform> _targets = new List<Transform>();
        private float _phase;

        public override float RightEdge => transform.position.x + 5f;

        protected override void Build()
        {
            int count = Random.Range(3, 6);
            int pattern = Random.Range(0, 3); // 0 línea, 1 diagonal subiendo, 2 diagonal bajando
            for (int i = 0; i < count; i++)
            {
                float dy = pattern == 0 ? 0f : (pattern == 1 ? 1f : -1f) * (i - (count - 1) * 0.5f) * 0.8f;
                var t = new GameObject("Target").transform;
                t.SetParent(transform, false);
                t.localPosition = new Vector2(i * 1.1f, dy);
                KenneyArt.Shape("tile_coin", t, Vector2.zero, Radius * 4f, Cfg.ink, 6, keepColor: true);
                _targets.Add(t);
            }
        }

        public override void Tick(float dt)
        {
            _phase += dt;
            for (int i = 0; i < _targets.Count; i++)
                _targets[i].localScale = Vector3.one * (1f + Mathf.Sin(_phase * 5f + i) * 0.06f);
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (!CircleCircle(tip, radius, _targets[i].position, Radius)) continue;
                Game.Collect(_targets[i].position, Cfg.coinsPerTarget);
                Destroy(_targets[i].gameObject);
                _targets.RemoveAt(i);
            }
            return _targets.Count == 0;
        }
    }
}

namespace ArrowBlaster
{
    // Partículas de impulso: las buenas (estrellas doradas) dan fuerza y un acelerón; las malas (humo de tinta) frenan.
    // Se recogen al tocarlas, ataques aparte. Van en grupos en línea o en arco para que merezca la pena ir a por ellas.
    public sealed class PickupCluster : Obstacle
    {
        private const float Radius = 0.42f;
        private readonly System.Collections.Generic.List<(Transform t, bool good)> _items = new System.Collections.Generic.List<(Transform, bool)>();
        private float _phase;

        public override float RightEdge => transform.position.x + 7f;

        protected override void Build()
        {
            int count = Random.Range(4, 8);
            bool arc = Random.value < 0.5f;
            bool good = Random.value >= Cfg.badPickupChance; // todo el grupo es bueno o todo malo
            for (int i = 0; i < count; i++)
            {
                float k = i / (float)(count - 1);
                float dy = arc ? Mathf.Sin(k * Mathf.PI) * 1.6f - 0.6f : (k - 0.5f) * Random.Range(-2f, 2f);
                var t = new GameObject(good ? "Good" : "Bad").transform;
                t.SetParent(transform, false);
                t.localPosition = new Vector2(i * 1.05f, dy);
                if (good) KenneyArt.Shape("star", t, Vector2.zero, Radius * 2.6f, Cfg.ink, 6, keepColor: true);
                else KenneyArt.Shape("smoke", t, Vector2.zero, Radius * 2.6f, Cfg.ink, 6);
                _items.Add((t, good));
            }
        }

        public override void Tick(float dt)
        {
            _phase += dt;
            transform.position += Vector3.left * (Cfg.pickupDriftSpeed * dt); // vienen hacia la flecha
            for (int i = 0; i < _items.Count; i++)
            {
                var t = _items[i].t;
                t.localScale = Vector3.one * (1f + Mathf.Sin(_phase * 6f + i) * 0.08f);
                t.localRotation = Quaternion.Euler(0f, 0f, (_items[i].good ? 40f : -25f) * _phase);
            }
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!CircleCircle(tip, radius, _items[i].t.position, Radius)) continue;
                Game.Pickup(_items[i].t.position, _items[i].good);
                Destroy(_items[i].t.gameObject);
                _items.RemoveAt(i);
            }
            return _items.Count == 0;
        }
    }
}
