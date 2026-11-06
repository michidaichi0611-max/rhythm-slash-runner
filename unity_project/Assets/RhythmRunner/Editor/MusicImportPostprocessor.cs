using UnityEditor;
using UnityEngine;

namespace RhythmSlashRunner.EditorTools
{
    /// <summary>
    /// Round 34: recorded music loops (Resources/Audio/Music) are read sample by sample when the game builds a piece at a
    /// tempo, so they must be decompressed when loaded; Vorbis keeps them small in the app.
    /// </summary>
    public sealed class MusicImportPostprocessor : AssetPostprocessor
    {
        const string MusicFolder = "Assets/RhythmRunner/Resources/Audio/Music/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = .7f;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }
    }
}
