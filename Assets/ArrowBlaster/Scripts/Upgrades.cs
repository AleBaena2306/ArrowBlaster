using UnityEngine;

namespace ArrowBlaster
{
    // Tienda de mejoras (GDD): Fuerza inicial y Puntería (arco); Vida, Velocidad y Destrucción (flecha).
    // Cada mejora tiene su color de barra. Niveles y coste en PlayerPrefs (pasará a StorePlatform).
    public enum Stat { Power, Aim, Life, Speed, Destruction }

    public static class Upgrades
    {
        public sealed class Def
        {
            public Stat Stat; public string Name, Detail; public Color Color; public int MaxLevel, BaseCost; public float Growth;
        }

        public static readonly Def[] All =
        {
            new Def { Stat = Stat.Power, Name = "POWER", Detail = "Launch force", Color = new Color32(0xD7, 0x26, 0x3D, 0xFF), MaxLevel = 15, BaseCost = 25, Growth = 1.35f },
            new Def { Stat = Stat.Aim, Name = "AIM", Detail = "Slower target", Color = new Color32(0x2A, 0x7F, 0xD4, 0xFF), MaxLevel = 10, BaseCost = 30, Growth = 1.4f },
            new Def { Stat = Stat.Life, Name = "LIFE", Detail = "Extra hits", Color = new Color32(0x3B, 0xA5, 0x5C, 0xFF), MaxLevel = 6, BaseCost = 60, Growth = 1.8f },
            new Def { Stat = Stat.Speed, Name = "SPEED", Detail = "Faster, lasts longer", Color = new Color32(0xE8, 0xA3, 0x17, 0xFF), MaxLevel = 15, BaseCost = 30, Growth = 1.35f },
            new Def { Stat = Stat.Destruction, Name = "DESTRUCTION", Detail = "Cheaper attacks", Color = new Color32(0x8E, 0x44, 0xAD, 0xFF), MaxLevel = 15, BaseCost = 35, Growth = 1.38f },
        };

        private static string Key(Stat s) => "ab_up_" + s;
        public static int Level(Stat s) => PlayerPrefs.GetInt(Key(s), 0);
        public static Def Get(Stat s) => All[(int)s];
        public static bool IsMax(Stat s) => Level(s) >= Get(s).MaxLevel;
        public static int Cost(Stat s) { var d = Get(s); return Mathf.RoundToInt(d.BaseCost * Mathf.Pow(d.Growth, Level(s)) / 5f) * 5; }

        public static bool TryBuy(Stat s)
        {
            if (IsMax(s) || !Save.Spend(Cost(s))) return false;
            PlayerPrefs.SetInt(Key(s), Level(s) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetAll() { foreach (var d in All) PlayerPrefs.DeleteKey(Key(d.Stat)); }

        // Efectos
        public static float PowerMultiplier => 1f + 0.15f * Level(Stat.Power);
        public static float TargetSpeedMultiplier => Mathf.Max(0.35f, 1f - 0.065f * Level(Stat.Aim));
        public static int ExtraLives => Level(Stat.Life);
        public static float SpeedMultiplier => 1f + 0.04f * Level(Stat.Speed);
        public static float DecayMultiplier => Mathf.Max(0.35f, 1f - 0.045f * Level(Stat.Speed));
        public static float AttackCostMultiplier => Mathf.Max(0.3f, 1f - 0.045f * Level(Stat.Destruction));
    }
}
