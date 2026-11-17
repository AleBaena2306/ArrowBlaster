using UnityEngine;

namespace ArrowBlaster
{
    // Todos los números del juego en un solo asset (Assets/ArrowBlaster/Settings/ArrowConfig.asset).
    // Se pueden tocar con el juego en marcha: los cambios se notan en la siguiente run.
    [CreateAssetMenu(fileName = "ArrowConfig", menuName = "Pointy Dash/Config")]
    public sealed class ArrowConfig : ScriptableObject
    {
        [Header("Mundo")]
        [Tooltip("Altura del suelo (unidades). Tocarlo quita una vida.")]
        public float groundY = -4.3f;
        [Tooltip("Techo: la flecha no puede subir más.")]
        public float ceilingY = 4.6f;
        [Tooltip("Tamaño ortográfico de la cámara (medio alto visible).")]
        public float cameraSize = 5.4f;
        [Tooltip("Posición X de la cámara mientras se apunta.")]
        public float aimCameraX = 6f;
        [Tooltip("Cuánto se adelanta la cámara a la flecha (la flecha queda a la izquierda).")]
        public float cameraLead = 5.5f;

        [Header("Lanzamiento: arco gigante (arrastrar hacia atrás y soltar)")]
        [Tooltip("Posición de la empuñadura del arco.")]
        public Vector2 bowPosition = new Vector2(0f, -1.3f);
        [Tooltip("Tamaño del arco respecto al normal.")]
        public float bowScale = 1.8f;
        [Tooltip("Arrastre (unidades de mundo) para tensar el arco al máximo.")]
        public float maxDrag = 3f;
        [Tooltip("Tensión mínima para disparar (si es menor, se cancela).")]
        public float minDrawToShoot = 0.2f;
        public float minAimAngle = -25f;
        public float maxAimAngle = 60f;
        [Tooltip("Velocidad de salida con el arco poco y muy tenso.")]
        public float launchSpeedMin = 12f;
        public float launchSpeedMax = 28f;
        [Tooltip("Gravedad durante el disparo hasta pasar el farolillo.")]
        public float launchGravity = 9f;

        [Header("Objetivo del lanzamiento: farolillo que sube y baja")]
        public float lanternX = 12f;
        public float lanternMidY = 0.6f;
        [Tooltip("Recorrido vertical desde el punto medio.")]
        public float lanternTravel = 2.8f;
        [Tooltip("Ciclos por segundo con la mejora de puntería a nivel 0 (la mejora lo frena).")]
        public float lanternSpeed = 0.4f;
        [Tooltip("Radio del farolillo (acierto normal).")]
        public float lanternRadius = 1.05f;
        [Tooltip("Radio del emblema central (acierto perfecto).")]
        public float lanternCenterRadius = 0.36f;
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
        [Tooltip("Ancho de la franja izquierda de la pantalla que ataca (0,2 = 20 %); el resto da impulso.")]
        [Range(0.05f, 0.5f)] public float attackZone = 0.2f;
        [Tooltip("Fuerza que cuesta cada golpe (embestida).")]
        public float attackForceCost = 6f;
        [Tooltip("Duración de cada golpe en segundos; mantener pulsado más tiempo no lo alarga.")]
        public float attackDuration = 0.35f;
        [Tooltip("Espera tras un golpe antes de poder dar otro (además de soltar).")]
        public float attackCooldown = 0.2f;
        [Tooltip("Velocidad horizontal mientras se ataca (multiplicador); la flecha va en línea recta.")]
        public float attackSpeedMultiplier = 1.7f;
        [Tooltip("Fuerza que cuesta romper un obstáculo atacando.")]
        public float breakForceCost = 10f;
        [Tooltip("Fuerza que cuesta romper un proyectil atacando.")]
        public float projectileForceCost = 3f;

        [Header("Vuelo")]
        public float gravity = 22f;
        [Tooltip("Velocidad vertical que da cada toque.")]
        public float flapVelocity = 8.5f;
        [Tooltip("Segundos pulsando para entrar en modo ataque.")]
        public float holdThreshold = 0.15f;
        [Tooltip("Gravedad mientras se ataca (planeo).")]
        public float attackGravityScale = 0.15f;
        [Tooltip("Radio de choque de la punta de la flecha.")]
        public float arrowRadius = 0.18f;

        [Header("Vida")]
        [Tooltip("Ya no se usa: la barra de fuerza es también la vida.")]
        public int lives = 3;
        [Tooltip("Fuerza que quita cada golpe recibido (la barra es velocidad y vida a la vez).")]
        public float hitForceCost = 18f;

        [Header("Dificultad por zona (todas las zonas van en la misma run)")]
        [Tooltip("Multiplica todo lo que quita fuerza de golpe en cada zona: choques, pinchos, ataques y roturas. En la zona 2 un choque debe dejarte casi sin fuerza.")]
        public float[] zoneLossMultiplier = { 1f, 2.5f, 4f, 6f };
        [Tooltip("Multiplica el desgaste continuo de fuerza en cada zona.")]
        public float[] zoneDecayMultiplier = { 1f, 1.3f, 1.6f, 1.9f };
        [Tooltip("Ritmo de subida que se le exige al jugador entre paredes (unidades por segundo). Más bajo = huecos más fáciles de alcanzar.")]
        public float gapClimbRate = 3.2f;
        [Tooltip("Ritmo de bajada exigible entre paredes (unidades por segundo).")]
        public float gapDropRate = 6f;
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
        [Tooltip("Probabilidades relativas: pared, enemigo, monedas (las partículas tienen su propio peso).")]
        public float wallWeight = 4f;
        public float enemyWeight = 2f;
        public float targetsWeight = 2f;
        public float projectileSpeed = 5f;
        public float enemyFireInterval = 1.6f;
        [Tooltip("Distancia a la que el enemigo empieza a disparar.")]
        public float enemyRange = 18f;

        [Header("Partículas de impulso (como en Burrito Bison)")]
        [Tooltip("Probabilidad relativa de un grupo de partículas frente a pared, enemigo y monedas.")]
        public float pickupsWeight = 3f;
        [Tooltip("Proporción de grupos de partículas malos (frenan); cada grupo es todo bueno o todo malo.")]
        [Range(0f, 1f)] public float badPickupChance = 0.3f;
        [Tooltip("Fuerza que da una partícula buena y quita una mala.")]
        public float pickupForce = 9f;
        [Tooltip("Velocidad a la que las partículas avanzan hacia la izquierda, contra la flecha.")]
        public float pickupDriftSpeed = 3f;

        [Header("Zonas y jefes")]
        [Tooltip("Distancia hasta el jefe de cada zona (metros).")]
        public float[] zoneLengths = { 300f, 400f, 500f, 600f };
        [Tooltip("Vida de cada jefe (se conserva entre runs).")]
        public float[] bossHp = { 400f, 900f, 1600f, 2500f };
        [Tooltip("Monedas por derrotar a cada jefe.")]
        public int[] bossReward = { 150, 300, 500, 800 };
        [Tooltip("Daño por golpe atacando (lo aumenta la mejora Destrucción).")]
        public float bossDamage = 30f;
        public float bossRadius = 1.5f;
        public float bossFireInterval = 1.3f;
        [Tooltip("Distancia a la que el jefe espera por delante de la flecha.")]
        public float bossDistance = 8f;
        [Tooltip("Velocidad a la que la flecha se acerca al jefe durante un golpe.")]
        public float bossChargeSpeed = 22f;

        [Header("Monedas")]
        public float coinsPerMeter = 0.4f;
        public int coinsPerTarget = 12;
        public int coinsPerEnemy = 10;
        public int coinsPerWall = 3;

        [Header("Colores de juego")]
        public Color danger = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
        public Color enemy = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
        public Color reward = new Color32(0xFF, 0xD2, 0x1F, 0xFF);
        public Color attack = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
        [Tooltip("Texto e iconos de la interfaz.")]
        public Color uiInk = Color.white;
        [Tooltip("Fondo de los paneles (fin de run, tienda, pausa).")]
        public Color uiPanel = new Color32(0x1D, 0x1A, 0x3A, 0xFF);

        [Header("Paletas por zona (estilo hipercasual: cada zona cambia de colores)")]
        public ZonePalette[] palettes =
        {
            new ZonePalette   // Violeta y lima
            {
                skyTop = new Color32(0x3B, 0x2B, 0xFF, 0xFF), skyBottom = new Color32(0xA4, 0x3D, 0xF2, 0xFF), hills = new Color(1f, 1f, 1f, 0.12f),
                ground = new Color32(0x2B, 0x1A, 0x9E, 0xFF), groundLip = new Color32(0xC6, 0xEF, 0x00, 0xFF),
                wall = new Color32(0xC6, 0xEF, 0x00, 0xFF), wallSide = new Color32(0x5F, 0xB8, 0x00, 0xFF),
                pointer = new Color32(0x2E, 0x2A, 0x8F, 0xFF), trail = new Color32(0xFF, 0xE1, 0x1C, 0xFF),
                coin = new Color32(0xFF, 0x2E, 0x7E, 0xFF), coinRing = new Color32(0xFF, 0xE1, 0x1C, 0xFF),
                enemy = new Color32(0xFF, 0x7A, 0x00, 0xFF),
            },
            new ZonePalette   // Azul y naranja
            {
                skyTop = new Color32(0x0B, 0x3D, 0xFF, 0xFF), skyBottom = new Color32(0x2F, 0x8B, 0xFF, 0xFF), hills = new Color(1f, 1f, 1f, 0.12f),
                ground = new Color32(0x0A, 0x2A, 0x9C, 0xFF), groundLip = new Color32(0xFF, 0xB0, 0x00, 0xFF),
                wall = new Color32(0xFF, 0xA8, 0x00, 0xFF), wallSide = new Color32(0xE8, 0x6A, 0x00, 0xFF),
                pointer = new Color32(0x2B, 0x2F, 0xB8, 0xFF), trail = new Color32(0xFF, 0xD2, 0x1F, 0xFF),
                coin = new Color32(0xFF, 0x2E, 0x7E, 0xFF), coinRing = new Color32(0xFF, 0xD2, 0x1F, 0xFF),
                enemy = new Color32(0x18, 0xC9, 0x64, 0xFF),
            },
            new ZonePalette   // Menta y ciruela
            {
                skyTop = new Color32(0x16, 0xD4, 0x8A, 0xFF), skyBottom = new Color32(0x7E, 0xD0, 0x7A, 0xFF), hills = new Color(1f, 1f, 1f, 0.14f),
                ground = new Color32(0x3A, 0x1E, 0x5A, 0xFF), groundLip = new Color32(0xB0, 0x2C, 0x6E, 0xFF),
                wall = new Color32(0xB0, 0x2C, 0x6E, 0xFF), wallSide = new Color32(0x5A, 0x1E, 0x5A, 0xFF),
                pointer = new Color32(0x1F, 0xB8, 0x9A, 0xFF), trail = new Color32(0xFF, 0x54, 0x70, 0xFF),
                coin = new Color32(0xF7, 0xB2, 0x4A, 0xFF), coinRing = new Color32(0xFF, 0x54, 0x70, 0xFF),
                enemy = new Color32(0x6A, 0x2F, 0xD8, 0xFF),
            },
            new ZonePalette   // Atardecer y turquesa
            {
                skyTop = new Color32(0xFF, 0x5E, 0x62, 0xFF), skyBottom = new Color32(0xFF, 0xB3, 0x6B, 0xFF), hills = new Color(1f, 1f, 1f, 0.16f),
                ground = new Color32(0x0D, 0x6E, 0x7A, 0xFF), groundLip = new Color32(0x1F, 0xD5, 0xC8, 0xFF),
                wall = new Color32(0x1F, 0xB5, 0xB0, 0xFF), wallSide = new Color32(0x0D, 0x6E, 0x7A, 0xFF),
                pointer = new Color32(0x2B, 0x2D, 0x42, 0xFF), trail = new Color32(0xFF, 0xFF, 0xFF, 0xFF),
                coin = new Color32(0xFF, 0xE1, 0x1C, 0xFF), coinRing = new Color32(0xFF, 0xFF, 0xFF, 0xFF),
                enemy = new Color32(0x6A, 0x2F, 0xD8, 0xFF),
            },
        };

        public ZonePalette Palette(int zone) => palettes == null || palettes.Length == 0 ? default : palettes[Mathf.Abs(zone) % palettes.Length];
    }
}
