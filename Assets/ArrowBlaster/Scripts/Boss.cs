using UnityEngine;

namespace ArrowBlaster
{
    // Jefe de zona: espera por delante de la flecha, flota y dispara. Solo se le hace daño atacando;
    // chocar sin atacar quita una vida. Cada golpe lo empuja hacia atrás. Su vida se guarda entre runs.
    public sealed class Boss : Obstacle
    {
        public int Zone;
        public float MaxHp, Hp;
        private Transform _body;
        private float _offset, _phase, _fireTimer, _flash;
        private SpriteRenderer _sprite;

        public override float RightEdge => float.MaxValue; // no desaparece por detrás: sigue a la flecha

        protected override void Build()
        {
            _body = new GameObject("BossBody").transform;
            _body.SetParent(transform, false);
            string[] looks = { "character_roundRed", "character_roundGreen", "character_roundPurple", "character_roundYellow" };
            _sprite = KenneyArt.Shape(looks[Zone % looks.Length], _body, Vector2.zero, Cfg.bossRadius * 3.1f, Cfg.ink, 9, keepColor: true);
            _offset = Cfg.bossDistance + 10f; // entra desde fuera de la pantalla
            _fireTimer = Cfg.bossFireInterval;
        }

        public void Knockback(float amount) { _offset += amount; _flash = 1f; }

        public override void Tick(float dt)
        {
            _phase += dt;
            // El jefe va a la par de la flecha; al atacar, la flecha embiste y se le acerca.
            // Sin atacar, el jefe recupera su distancia.
            if (Game.IsAttacking) _offset = Mathf.Max(0f, _offset - Cfg.bossChargeSpeed * dt);
            else _offset = Mathf.MoveTowards(_offset, Cfg.bossDistance, dt * 4f);
            var p = transform.position;
            p.x = Game.ArrowPosition.x + _offset;
            p.y = Mathf.Sin(_phase * 1.3f) * 2.2f + 0.3f;
            transform.position = p;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);
            float squash = Mathf.Sin(_phase * 3f) * 0.04f;
            _body.localScale = new Vector3(1f + squash + _flash * 0.15f, 1f - squash - _flash * 0.1f, 1f);
            _sprite.color = Color.Lerp(Cfg.ink, Cfg.attack, _flash);

            _fireTimer -= dt;
            if (_fireTimer > 0f || _offset > Cfg.bossDistance + 2f || _offset < 3f) return;
            _fireTimer = Cfg.bossFireInterval * Random.Range(0.8f, 1.2f);
            Game.Spawn<Projectile>("BossShot", (Vector2)p + new Vector2(-Cfg.bossRadius, Random.Range(-0.6f, 0.6f)));
        }

        public override bool CheckHit(Vector2 tip, float radius, bool attacking)
        {
            if (!CircleCircle(tip, radius, transform.position, Cfg.bossRadius)) return false;
            if (attacking) return Game.HitBoss(this, tip);
            Game.Damage(tip);
            Knockback(2.5f);
            return false;
        }
    }
}
