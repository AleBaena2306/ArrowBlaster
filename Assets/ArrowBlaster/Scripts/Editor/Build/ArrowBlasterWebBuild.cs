using KovusPride.Web.Editor;
using UnityEditor;

namespace ArrowBlaster.Editor
{
    // Menús de build web para el portfolio (módulo Web del framework de Kovu's Pride).
    // Solo se compila si el paquete com.kovuspride.framework está instalado (define KP_FRAMEWORK).
    public static class ArrowBlasterWebBuild
    {
        private static readonly WebGame Game = new WebGame("arrow-blaster", "Arrow Blaster");

        [MenuItem("ARROW BLASTER/Build/Web (móvil + PC)", priority = 20)]
        private static void Web() => KovusPrideWebBuild.BuildAndPublish(Game);

        [MenuItem("ARROW BLASTER/Build/Web: solo compilar (sin publicar)", priority = 21)]
        private static void Only() => KovusPrideWebBuild.BuildOnly(Game);

        [MenuItem("ARROW BLASTER/Build/Web: prueba rápida (solo PC, sin publicar)", priority = 22)]
        private static void Quick() => KovusPrideWebBuild.QuickTest(Game);

        [MenuItem("ARROW BLASTER/Build/Web: publicar la última build", priority = 23)]
        private static void Publish() => KovusPrideWebBuild.PublishOnly(Game);
    }
}
