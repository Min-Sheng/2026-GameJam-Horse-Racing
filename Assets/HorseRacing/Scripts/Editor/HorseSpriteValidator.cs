using UnityEngine;
using UnityEditor;
using System.Linq;

namespace HorseRacing.Editor
{
    /// <summary>
    /// Editor validation script that checks sprite sheet import settings
    /// for all horse character PNGs in Assets/HorseRacing/Art/Horses/.
    /// Validates sprite mode, alpha transparency, and minimum frame count.
    /// </summary>
    public static class HorseSpriteValidator
    {
        private const string SpriteSheetsFolder = "Assets/HorseRacing/Art/Horses";

        [MenuItem("HorseRacing/Validate Sprite Sheets")]
        public static void ValidateAllSpriteSheets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteSheetsFolder });

            if (guids.Length == 0)
            {
                Debug.LogWarning("[HorseSpriteValidator] No textures found in " + SpriteSheetsFolder);
                return;
            }

            int validCount = 0;
            int issueCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isValid = ValidateSpriteSheet(path);
                if (isValid)
                    validCount++;
                else
                    issueCount++;
            }

            Debug.Log($"[HorseSpriteValidator] Validation complete. " +
                $"{validCount} sheet(s) passed, {issueCount} sheet(s) have issues.");
        }

        /// <summary>
        /// Validates a single sprite sheet at the given asset path.
        /// Returns true if all checks pass.
        /// </summary>
        public static bool ValidateSpriteSheet(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[HorseSpriteValidator] Could not get TextureImporter for: {assetPath}");
                return false;
            }

            bool allValid = true;

            // Check sprite mode is Multiple
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Debug.LogWarning($"[HorseSpriteValidator] '{assetPath}' sprite mode is not 'Multiple'. " +
                    $"Current mode: {importer.spriteImportMode}. " +
                    "Set sprite mode to 'Multiple' for animation frame slicing.");
                allValid = false;
            }

            // Check alphaIsTransparency is enabled
            if (!importer.alphaIsTransparency)
            {
                Debug.LogWarning($"[HorseSpriteValidator] '{assetPath}' does not have 'Alpha Is Transparency' enabled. " +
                    "Enable this setting for correct transparency rendering.");
                allValid = false;
            }

            // Check sub-sprite count (at least 2 frames required for animation)
            int spriteCount = CountSubSprites(assetPath);
            if (spriteCount < 2)
            {
                Debug.LogWarning($"[HorseSpriteValidator] '{assetPath}' has fewer than 2 sub-sprites ({spriteCount} found). " +
                    "Sprite sheet may not be sliced correctly. At least 2 frames are required for animation.");
                allValid = false;
            }

            return allValid;
        }

        /// <summary>
        /// Counts the number of Sprite sub-assets at the given path.
        /// </summary>
        private static int CountSubSprites(string assetPath)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return allAssets.Count(a => a is Sprite);
        }
    }
}
