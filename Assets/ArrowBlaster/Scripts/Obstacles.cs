using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Obstáculos del vuelo. Sin físicas de Unity: cada uno comprueba a mano si la punta de la flecha lo toca.
    // Reglas:
    //  - Chocar sin atacar: quita una vida (pilares, enemigos, proyectiles).
    //  - Chocar atacando: rompe lo que toca y cuesta fuerza.
    //  - Las dianas pequeñas no hacen daño: solo se rompen atacando y dan monedas.
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

        // Mancha redonda de color (el shader de tinta le da el borde irregular)
        protected SpriteRenderer Dab(Transform parent, Vector2 pos, float size, Color color, int order)
        {
            var body = Gfx.Shape("Body", Gfx.Circle, parent, pos, Vector2.one * size, color, order);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            return body;
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

            // Trazo de color (vertical) y remate de tinta en el extremo que da al hueco
            Gfx.Shape("Stroke", Gfx.Square, root, Vector2.zero, r.size, Cfg.danger, 6);
            float capY = (capAtBottom ? -1f : 1f) * (r.height * 0.5f - 0.1f);
            Gfx.Shape("Cap", Gfx.Square, root, new Vector2(0f, capY), new Vector2(r.width + 0.7f, 0.26f), Cfg.ink, 8);
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
        private Transform _body, _pupilL, _pupilR;

        protected override void Build()
        {
            _baseY = transform.position.y;
            _phase = Random.value * 10f;
            _body = new GameObject("Oni").transform;
            _body.SetParent(transform, false);

            // Cuernos
            var hornL = Gfx.Shape("HornL", Gfx.Triangle, _body, new Vector2(-0.28f, 0.62f), new Vector2(0.34f, 0.2f), Cfg.ink, 4);
            hornL.transform.localRotation = Quaternion.Euler(0, 0, 110f);
            var hornR = Gfx.Shape("HornR", Gfx.Triangle, _body, new Vector2(0.28f, 0.62f), new Vector2(0.34f, 0.2f), Cfg.ink, 4);
            hornR.transform.localRotation = Quaternion.Euler(0, 0, 70f);

            Dab(_body, Vector2.zero, Radius * 2f, Cfg.enemy, 5);

            // Ojos que siguen a la flecha
            Gfx.Shape("EyeL", Gfx.Circle, _body, new Vector2(-0.2f, 0.12f), Vector2.one * 0.3f, Cfg.paper, 8);
            Gfx.Shape("EyeR", Gfx.Circle, _body, new Vector2(0.14f, 0.12f), Vector2.one * 0.26f, Cfg.paper, 8);
            _pupilL = Gfx.Shape("PupilL", Gfx.Circle, _body, new Vector2(-0.2f, 0.12f), Vector2.one * 0.13f, Cfg.ink, 9).transform;
            _pupilR = Gfx.Shape("PupilR", Gfx.Circle, _body, new Vector2(0.14f, 0.12f), Vector2.one * 0.12f, Cfg.ink, 9).transform;
            // Boca
            Gfx.Shape("Mouth", Gfx.Square, _body, new Vector2(-0.04f, -0.22f), new Vector2(0.4f, 0.08f), Cfg.ink, 8);

            _fireTimer = Cfg.enemyFireInterval * 0.5f;
        }

        public override void Tick(float dt)
        {
            _phase += dt;
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(_phase * 2.4f) * 0.25f;
            transform.position = p;
            _body.localScale = new Vector3(1f + Mathf.Sin(_phase * 4.8f) * 0.04f, 1f - Mathf.Sin(_phase * 4.8f) * 0.04f, 1f);

            Vector2 look = ((Vector2)Game.ArrowPosition - (Vector2)p).normalized * 0.06f;
            _pupilL.localPosition = new Vector2(-0.2f, 0.12f) + look;
            _pupilR.localPosition = new Vector2(0.14f, 0.12f) + look;

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
            Dab(transform, Vector2.zero, Radius * 2.2f, Cfg.enemy, 7);
        }

        public override void Tick(float dt)
        {
            transform.position += Vector3.left * (Cfg.projectileSpeed * dt);
            transform.Rotate(0f, 0f, 200f * dt);
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            if (!CircleCircle(tip, radius, transform.position, Radius)) return false;
            if (attacking) Game.Break(tip, Cfg.projectileForceCost, 0, Cfg.enemy);
            else Game.Damage(tip);
            return true;
        }
    }

    // Grupo de dianas pequeñas (mato): no hacen daño; romperlas atacando da monedas.
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
                Gfx.Shape("Ink", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 2f, Cfg.ink, 5);
                Gfx.Shape("Paper", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 1.5f, Cfg.paper, 6);
                Gfx.Shape("Ring", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 1.05f, Cfg.ink, 7);
                Gfx.Shape("Gold", Gfx.Circle, t, Vector2.zero, Vector2.one * Radius * 0.7f, Cfg.reward, 8);
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
