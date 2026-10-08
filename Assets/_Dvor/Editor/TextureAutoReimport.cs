#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dvor.EditorTools
{
    /// <summary>
    /// Правила импорта применяются только к новым файлам. Если текстуры уже
    /// лежали в проекте, они остаются со старыми настройками — и фасад молча
    /// рисуется процедурно. Этот класс проверяет их при каждой перекомпиляции
    /// и переимпортирует те, у которых настройки не те. Руками ничего жать не нужно.
    /// </summary>
    public static class TextureAutoReimport
    {
        private const string Folder = "Assets/_Dvor/Resources/Textures";

        [InitializeOnLoadMethod]
        private static void Check()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;

            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;

                string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool isBase   = file.StartsWith("panel_a") || file.StartsWith("panel_b");
                bool isNormal = file.EndsWith("_normal");

                bool wrong =
                    (isBase && (!imp.isReadable ||
                                imp.textureCompression != TextureImporterCompression.Uncompressed)) ||
                    (isNormal && imp.textureType != TextureImporterType.NormalMap);

                if (!wrong) continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fixedCount++;
            }

            if (fixedCount > 0)
                Debug.Log($"[Двор] Переимпортировал текстур с неверными настройками: {fixedCount}");
        }

        [MenuItem("Двор/Переимпортировать текстуры", priority = 40)]
        private static void Force()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Debug.LogWarning("[Двор] Папки с текстурами нет — игра рисует процедурные");
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("[Двор] Текстуры переимпортированы");
        }
    }
}
#endif
