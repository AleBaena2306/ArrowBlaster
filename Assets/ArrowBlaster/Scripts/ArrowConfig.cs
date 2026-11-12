using UnityEngine;

namespace ArrowBlaster
{
    // Todos los números del juego en un solo asset (Assets/ArrowBlaster/Settings/ArrowConfig.asset).
    // Se pueden tocar con el juego en marcha: los cambios se notan en la siguiente run.
    [CreateAssetMenu(fileName = "ArrowConfig", menuName = "Arrow Blaster/Config")]
    public sealed class ArrowConfig : ScriptableObject
    {
        [Header("Mundo")]
        [Tooltip("Altura del suelo (unidades). Tocarlo quita una vida.")]
        public float groundY = -4.3f;
        [Tooltip("Techo: la flecha no puede subir más.")]
        public float ceilingY = 4.6f;
        [Tooltip("Tamaño ortográfico de la cámara (medio alto visible).")]
        public float cameraSize = 5.4f;
        [Tooltip("Cuánto se adelanta la cámara a la flecha (la flecha queda a la izquierda).")]
        public float cameraLead = 5.5f;

        [Header("Lanzamiento")]
        [Tooltip("Oscilaciones por segundo de la mira con la mejora a nivel 0.")]
        public float aimSpeed = 0.9f;
        [Tooltip("Recorrido vertical de la mira (desde el centro de la diana).")]
        public float aimRange = 3.2f;
        public float targetX = 9f;
        [Tooltip("Mitad del alto de la diana.")]
        public float targetHalfHeight = 1.8f;
        [Tooltip("Mitad del alto del centro de la diana.")]
        public float targetCenterHalf = 0.35f;
        public float centerMultiplier = 1.5f;
        public float ringMultiplier = 1.2f;
        public float missMultiplier = 1f;

        [Header("Fuerza y velocidad")]
        [Tooltip("Fuerza inicial con la mejora a nivel 0.")]
        public float initialForce = 60f;
        public float maxForce = 400f;
        [Tooltip("Velocidad horizontal mínima (con fuerza 0).")]
        public float minSpeed = 4f;
        [Tooltip("Velocidad que da cada punto de fuerza.")]
        public float speedPerForce = 0.08f;
        [Tooltip("Fuerza que se pierde por segundo de vuelo.")]
        public float forceDecayPerSecond = 3.5f;
        [Tooltip("Fuerza que cuesta romper un obstáculo atacando.")]
        public float breakForceCost = 10f;
        [Tooltip("Fuerza que cuesta romper un proyectil atacando.")]
        public float projectileForceCost = 3f;

        [Header("Vuelo")]
        public float gravity = 22f;
        [Tooltip("Velocidad vertical que da cada toque.")]
        public float flapVelocity = 8.5f;
        [Tooltip("Segundos pulsando para entrar en modo ataque.")]
        public float holdThreshold = 0.18f;
        [Tooltip("Gravedad mientras se ataca (planeo).")]
        public float attackGravityScale = 0.15f;
        [Tooltip("Radio de choque de la punta de la flecha.")]
        public float arrowRadius = 0.18f;

        [Header("Vida")]
        public int lives = 3;
        [Tooltip("Segundos de invulnerabilidad tras un golpe.")]
        public float invulnerableTime = 0.8f;

        [Header("Obstáculos")]
        [Tooltip("Distancia del primer obstáculo.")]
        public float firstObstacleX = 28f;
        public float minSpacing = 9f;
        public float maxSpacing = 14f;
        [Tooltip("Alto del hueco de las paredes.")]
        public float gapHeight = 3.4f;
        public float wallWidth = 0.9f;
        [Tooltip("Probabilidades relativas: pared, enemigo, dianas.")]
        public float wallWeight = 4f;
        public float enemyWeight = 2f;
        public float targetsWeight = 2f;
        public float projectileSpeed = 5f;
        public float enemyFireInterval = 1.6f;
        [Tooltip("Distancia a la que el enemigo empieza a disparar.")]
        public float enemyRange = 18f;

        [Header("Monedas")]
        public float coinsPerMeter = 0.1f;
        public int coinsPerTarget = 5;
        public int coinsPerEnemy = 10;
        public int coinsPerWall = 3;

        [Header("Colores (tinta en blanco y negro; lo peligroso en color)")]
        public Color paper = new Color32(0xEF, 0xE9, 0xDD, 0xFF);
        public Color ink = new Color32(0x16, 0x16, 0x16, 0xFF);
        public Color inkSoft = new Color32(0x16, 0x16, 0x16, 0x40);
        public Color danger = new Color32(0xD7, 0x26, 0x3D, 0xFF);
        public Color enemy = new Color32(0x2A, 0x7F, 0xD4, 0xFF);
        public Color reward = new Color32(0xE8, 0xA3, 0x17, 0xFF);
        public Color attack = new Color32(0xD7, 0x26, 0x3D, 0xFF);
    }
}
