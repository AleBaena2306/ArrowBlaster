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

        public static void AddRun(int coins, float distance)
        {
            PlayerPrefs.SetInt(CoinsKey, Coins + coins);
            if (distance > BestDistance) PlayerPrefs.SetFloat(BestKey, distance);
            PlayerPrefs.Save();
        }
    }
}
