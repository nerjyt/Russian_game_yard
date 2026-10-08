#if UNITY_EDITOR
using UnityEditor;

namespace Dvor.EditorTools
{
    /// <summary>
    /// Настраивает импорт текстур автоматически по имени файла.
    /// Карта нормалей, импортированная как обычная картинка, светит неправильно —
    /// это самая частая причина «почему у меня стены выглядят странно».
    /// </summary>
    public class TextureImportRules : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("_Dvor/Resources/Textures")) return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;

            // Основы фасада читаются кодом: по ним дорисовываются окна.
            // Сжатые в DXT текстуры читаются ненадёжно, поэтому без сжатия;
            // 512 хватает с запасом — они идут в композит 512x512, не на экран.
            if (file.StartsWith("panel_a") || file.StartsWith("panel_b"))
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
            }

            if (file.EndsWith("_normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (file.EndsWith("_rough") || file.EndsWith("_ao") || file.EndsWith("_disp"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;   // служебные карты — линейные
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }
        }
    }
}
#endif
