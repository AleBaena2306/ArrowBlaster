using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ArrowBlaster.Editor
{
    // ARROW BLASTER > Setup: crea (o rehace) la escena del juego, el asset de configuración
    // y los ajustes del proyecto. Se puede pulsar las veces que haga falta: no borra la configuración.
    public static class ArrowBlasterSetup
    {
        private const string Root = "Assets/ArrowBlaster";
        private const string ScenePath = Root + "/Scenes/Game.unity";
        private const string ConfigPath = Root + "/Settings/ArrowConfig.asset";

        [MenuItem("POINTY DASH/Setup (escena y ajustes)", priority = 0)]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Settings");
            AssetDatabase.Refresh();

            var config = AssetDatabase.LoadAssetAtPath<ArrowConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ArrowConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = config.cameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.Palette(0).skyBottom;
            camGo.transform.position = new Vector3(config.aimCameraX, 0f, -10f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var game = new GameObject("Game").AddComponent<ArrowGame>();
            game.Config = config;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Kovu's Pride";
            PlayerSettings.productName = "Pointy Dash";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.KovusPride.ArrowBlaster");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.WebGL, "com.KovusPride.ArrowBlaster");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.KovusPride.ArrowBlaster");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.01";

            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"[ArrowBlaster] Escena lista en {ScenePath}. Pulse Play para jugar. Los números del juego están en {ConfigPath}.");
        }

        [MenuItem("POINTY DASH/Abrir configuración", priority = 1)]
        private static void OpenConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArrowConfig>(ConfigPath);
            if (config == null) { Setup(); return; }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("POINTY DASH/Borrar partida guardada", priority = 2)]
        private static void ClearSave()
        {
            if (!EditorUtility.DisplayDialog("Pointy Dash", "¿Borrar las monedas, el récord y las mejoras guardados en este ordenador?", "Borrar", "Cancelar")) return;
            PlayerPrefs.DeleteKey("ab_coins");
            PlayerPrefs.DeleteKey("ab_best");
            Upgrades.ResetAll();
            Save.ResetProgress();
            PlayerPrefs.Save();
        }

        // Bots programados juegan el juego entero varias veces desde cero, a toda velocidad,
        // y apuntan cuánto tardan en cada jefe (BotRuns/resumen.txt y BotRuns/runs.csv).
        [MenuItem("POINTY DASH/Bots/Simular progresión (6 bots)", priority = 40)]
        private static void SimulateProgression()
        {
            if (!EditorUtility.DisplayDialog("Pointy Dash · Bots",
                "Los bots empiezan desde cero y borran la partida guardada de este editor (monedas, mejoras y jefes).\n\n" +
                "Se abre el modo Play y el juego va tan rápido como pueda el PC. Al terminar se para solo y deja el resultado en la carpeta BotRuns del proyecto.",
                "Empezar", "Cancelar")) return;
            SessionState.SetBool("pd_bots", true);
            EditorApplication.isPlaying = true;
        }

        // Con Python escuchando (mlagents-learn MLAgents/pointy.yaml --run-id=pointy1) entrena el agente;
        // sin Python, el agente juega con el bot programado.
        [MenuItem("POINTY DASH/Bots/Entrenar con ML-Agents", priority = 42)]
        private static void TrainMlAgents()
        {
            SessionState.SetBool("pd_mlagents", true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("POINTY DASH/Bots/Abrir resultados", priority = 41)]
        private static void OpenResults()
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "BotRuns"));
            System.IO.Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(System.IO.Path.Combine(dir, "resumen.txt"));
        }
    }
}
