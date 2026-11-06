using UnityEditor;

namespace RhythmSlashRunner.EditorTools
{
    /// <summary>Imports everything under Resources/Art as UI-ready sprites (no mipmaps, full-quality compression).</summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        // bumping the version re-imports every texture with the current rules
        public override uint GetVersion() { return 4; }

        const string ArtFolder = "Assets/RhythmRunner/Resources/Art/";

        static readonly string[] CharacterPrefixes = { "hamster_", "cat_", "robot_", "penguin_", "shiba_", "otottsan_", "toumei_", "tori_", "whamster_", "shark_" };

        /// <summary>A character pose (not the wide FEVER cut-in, which fills the screen width).</summary>
        static bool CharacterPose(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("cutin_fever")) return false;
            foreach (string p in CharacterPrefixes) if (name.StartsWith(p)) return true;
            return false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var importer = (TextureImporter)assetImporter;
            bool tile = assetPath.Contains("wood_road_tile") || assetPath.Contains("/wall_") || assetPath.Contains("/tile_"); // repeating textures, not sprites
            importer.textureType = tile ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = tile;
            importer.alphaIsTransparency = !tile;
            importer.wrapMode = tile ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // round 34 (app size): a repeating texture of odd size (896×683) fell back to uncompressed RGBA on Android (3.3 MB
            // each); scaled to the nearest power of two it compresses like the rest (sampled by UV, so it looks the same)
            importer.npotScale = tile ? TextureImporterNPOTScale.ToNearest : TextureImporterNPOTScale.None;
            // Android (release P2 / owner round 5: app size): ASTC 6x6 for sprites, 8x8 for full-screen opaque pictures
            bool opaque = assetPath.Contains("backdrop") || assetPath.Contains("stage_bg_") || assetPath.Contains("key_visual") || assetPath.Contains("launch_bg") || assetPath.Contains("/wall_");
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = CharacterPose(assetPath) ? 512 : 2048; // round 34: poses are drawn at most ~500 px tall on a phone
            android.format = opaque ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 50;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
