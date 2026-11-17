using UnityEditor;

namespace ArrowBlaster.EditorTools
{
    // Importación de los PNG de ShapeForge (Resources/Shapes): sin compresión (los colores planos se ensucian),
    // transparencia limpia en los bordes y mipmaps para que las figuras pequeñas no se vean dentadas.
    public sealed class ShapesImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Shapes/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.isReadable = false;
        }
    }
}
