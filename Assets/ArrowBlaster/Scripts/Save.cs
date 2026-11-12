using UnityEngine;

namespace ArrowBlaster
{
    // Guardado provisional (PlayerPrefs). El día 3 pasa a StorePlatform del framework,
    // que en Playgama guarda con su SDK y en el resto con PlayerPrefs.
    public static class Save
    {
        private const string CoinsKey = "ab_coins";
        private const string BestKey = "ab_best";

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
        public static float BestDistance => PlayerPrefs.GetFloat(BestKey, 0f);

        // Zona actual (0-3) y vuelta de New Game+; vida restante de cada jefe (negativa = entera)
        public static int Zone { get => PlayerPrefs.GetInt("ab_zone", 0); set { PlayerPrefs.SetInt("ab_zone", value); PlayerPrefs.Save(); } }
        public static int Loop { get => PlayerPrefs.GetInt("ab_loop", 0); set { PlayerPrefs.SetInt("ab_loop", value); PlayerPrefs.Save(); } }
        public static float BossHp(int zone) => PlayerPrefs.GetFloat("ab_boss" + zone, -1f);
        public static void SetBossHp(int zone, float hp) { PlayerPrefs.SetFloat("ab_boss" + zone, hp); PlayerPrefs.Save(); }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("ab_zone");
            PlayerPrefs.DeleteKey("ab_loop");
            for (int i = 0; i < 8; i++) PlayerPrefs.DeleteKey("ab_boss" + i);
        }

        public static bool Spend(int coins)
        {
            if (coins > Coins) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - coins);
            PlayerPrefs.Save();
            return true;
        }

        public static void AddRun(int coins, float distance)
        {
            PlayerPrefs.SetInt(CoinsKey, Coins + coins);
            if (distance > BestDistance) PlayerPrefs.SetFloat(BestKey, distance);
            PlayerPrefs.Save();
        }
    }
}
