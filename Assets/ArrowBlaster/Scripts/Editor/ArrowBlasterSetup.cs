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

        [MenuItem("ARROW BLASTER/Setup (escena y ajustes)", priority = 0)]
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
            cam.backgroundColor = config.paper;
            camGo.transform.position = new Vector3(config.aimCameraX, 0f, -10f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var game = new GameObject("Game").AddComponent<ArrowGame>();
            game.Config = config;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Kovu's Pride";
            PlayerSettings.productName = "Arrow Blaster";
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

        [MenuItem("ARROW BLASTER/Abrir configuración", priority = 1)]
        private static void OpenConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArrowConfig>(ConfigPath);
            if (config == null) { Setup(); return; }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("ARROW BLASTER/Borrar partida guardada", priority = 2)]
        private static void ClearSave()
        {
            if (!EditorUtility.DisplayDialog("Arrow Blaster", "¿Borrar las monedas y el récord guardados en este ordenador?", "Borrar", "Cancelar")) return;
            PlayerPrefs.DeleteKey("ab_coins");
            PlayerPrefs.DeleteKey("ab_best");
            PlayerPrefs.Save();
        }
    }
}
