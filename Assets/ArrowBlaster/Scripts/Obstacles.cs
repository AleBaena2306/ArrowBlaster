using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Obstáculos del vuelo. Sin físicas de Unity: cada uno comprueba a mano si la punta de la flecha lo toca.
    // Reglas:
    //  - Chocar sin atacar: quita una vida (paredes, enemigos, proyectiles).
    //  - Chocar atacando: rompe lo que toca y cuesta fuerza.
    //  - Las dianas pequeñas no hacen daño: solo se rompen atacando y dan monedas.
    public abstract class Obstacle : MonoBehaviour
    {
        protected ArrowGame Game;
        protected ArrowConfig Cfg => Game.Config;

        public virtual float RightEdge => transform.position.x + 2f;

        public void Init(ArrowGame game) { Game = game; Build(); }
        protected abstract void Build();
        public virtual void Tick(float dt) { }
        // Devuelve true si el obstáculo ya no existe (se ha roto entero o ha salido de la pantalla)
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

    // Pared con un hueco: hay que pasar por el hueco o romperla atacando.
    public sealed class WallGap : Obstacle
    {
        public float GapCenter;
        private readonly List<(SpriteRenderer sr, Rect rect)> _parts = new List<(SpriteRenderer, Rect)>();

        public override float RightEdge => transform.position.x + Cfg.wallWidth;

        protected override void Build()
        {
            float x = transform.position.x, w = Cfg.wallWidth;
            float gapTop = GapCenter + Cfg.gapHeight * 0.5f, gapBottom = GapCenter - Cfg.gapHeight * 0.5f;
            AddPart(new Rect(x - w * 0.5f, gapTop, w, Cfg.ceilingY + 2f - gapTop));
            AddPart(new Rect(x - w * 0.5f, Cfg.groundY - 2f, w, gapBottom - (Cfg.groundY - 2f)));
        }

        private void AddPart(Rect r)
        {
            if (r.height <= 0.05f) return;
            var sr = Gfx.Shape("Wall", Gfx.Square, transform, r.center - (Vector2)transform.position, r.size, Cfg.danger, 5);
            _parts.Add((sr, r));
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                if (!CircleRect(tip, radius, _parts[i].rect)) continue;
                if (attacking) Game.Break(tip, Cfg.breakForceCost, Cfg.coinsPerWall, Cfg.danger);
                else Game.Damage(tip);
                Destroy(_parts[i].sr.gameObject);
                _parts.RemoveAt(i);
            }
            return _parts.Count == 0;
        }
    }

    // Enemigo de color que dispara proyectiles hacia la flecha.
    public sealed class Enemy : Obstacle
    {
        private const float Radius = 0.6f;
        private float _fireTimer;

        protected override void Build()
        {
            Gfx.Shape("Body", Gfx.Circle, transform, Vector2.zero, Vector2.one * Radius * 2f, Cfg.enemy, 5);
            Gfx.Shape("Eye", Gfx.Circle, transform, new Vector2(-0.2f, 0.15f), Vector2.one * 0.28f, Cfg.paper, 6);
            _fireTimer = Cfg.enemyFireInterval * 0.5f;
        }

        public override void Tick(float dt)
        {
            float ahead = transform.position.x - Game.ArrowPosition.x;
            if (ahead < 1f || ahead > Cfg.enemyRange) return;
            _fireTimer -= dt;
            if (_fireTimer > 0f) return;
            _fireTimer = Cfg.enemyFireInterval;
            Game.Spawn<Projectile>("Projectile", (Vector2)transform.position + Vector2.left * Radius);
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
            Gfx.Shape("Ball", Gfx.Circle, transform, Vector2.zero, Vector2.one * Radius * 2f, Cfg.enemy, 7);
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

    // Grupo de dianas pequeñas: no hacen daño; romperlas atacando da monedas.
    public sealed class TargetCluster : Obstacle
    {
        private const float Radius = 0.38f;
        private readonly List<Transform> _targets = new List<Transform>();

        public override float RightEdge => transform.position.x + 4f;

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
                Gfx.Shape("Ring", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 2f, Cfg.reward, 5);
                Gfx.Shape("Inner", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 1.2f, Cfg.paper, 6);
                Gfx.Shape("Dot", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 0.5f, Cfg.reward, 7);
                _targets.Add(t);
            }
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            if (!attacking) return false;
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (!CircleCircle(tip, radius, _targets[i].position, Radius)) continue;
                Game.Break(_targets[i].position, 0f, Cfg.coinsPerTarget, Cfg.reward);
                Destroy(_targets[i].gameObject);
                _targets.RemoveAt(i);
            }
            return _targets.Count == 0;
        }
    }
}
