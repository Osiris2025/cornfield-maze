#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CornMaze.EditorTools
{
    /// <summary>
    /// M29 (§17): the import settings for the derived ground maps, plus the probe that reads the importers'
    /// OWN state back.
    ///
    /// The order for this milestone is exact about the settings — albedo sRGB + Repeat; normal map Non-Color +
    /// Repeat with the green channel NOT flipped (the Poly Haven sets are `nor_gl`, OpenGL convention, which is
    /// Unity's — flipping would invert the lighting on every ridge of gravel); roughness and the raggedness
    /// mask Non-Color with sRGB off. `ProbeGround()` prints what the importers actually hold, because "it
    /// imported fine" is not evidence.
    ///
    /// One deliberate extra: `T_Ground_LaneEdge` gets Read/Write enabled. `GroundLaneMesh` samples it on the
    /// CPU at world-build time to pull the lane margins in, so the mask has to be readable in the player.
    /// That costs a CPU-side copy of a 1K map; the M29 report carries the number and the phone follow-up.
    ///
    /// Run (batchmode):
    ///   Unity -batchmode -quit -projectPath . -logFile Builds/ground-import.log \
    ///     -executeMethod CornMaze.EditorTools.CornMazeGroundImport.ApplyAll
    /// </summary>
    public static class CornMazeGroundImport
    {
        const string Dir = "Assets/Resources/Ground/";

        // Ground maps are seen from a standing player's eye height in long lanes, so 1K is the floor of what
        // is useful; the derived art is authored at 1024 and downsizing here would throw away the grader's work.
        const int MaxSize = 1024;

        [MenuItem("Corn Maze/M29: Apply ground import settings and probe")]
        public static void ApplyAll()
        {
            Configure(Dir + "T_Ground_Field.png", GroundMap.Albedo);
            Configure(Dir + "T_Ground_Field_N.png", GroundMap.Normal);
            Configure(Dir + "T_Ground_Field_R.png", GroundMap.Data);
            Configure(Dir + "T_Ground_Lane.png", GroundMap.Albedo);
            Configure(Dir + "T_Ground_Lane_N.png", GroundMap.Normal);
            Configure(Dir + "T_Ground_Lane_R.png", GroundMap.Data);
            Configure(Dir + "T_Ground_LaneEdge.png", GroundMap.Mask);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("M29-GROUND: import settings applied.\n" + ProbeGround());
        }

        enum GroundMap { Albedo, Normal, Data, Mask }

        static void Configure(string path, GroundMap kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("M29-GROUND: no TextureImporter at " + path);
                return;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.wrapMode = TextureWrapMode.Repeat;      // every map tiles; the maze floor is a repeat
            settings.mipmapEnabled = kind != GroundMap.Mask; // the mask is sampled on the CPU, never drawn
            settings.sRGBTexture = kind == GroundMap.Albedo;

            switch (kind)
            {
                case GroundMap.Albedo:
                    settings.textureType = TextureImporterType.Default;
                    break;
                case GroundMap.Normal:
                    settings.textureType = TextureImporterType.NormalMap;
                    // The sets are nor_gl (OpenGL convention) which is Unity's own: flipping green here would
                    // light every gravel ridge from the wrong side.
                    settings.flipGreenChannel = false;
                    break;
                case GroundMap.Data:
                case GroundMap.Mask:
                    settings.textureType = TextureImporterType.Default;
                    break;
            }

            importer.SetTextureSettings(settings);
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.None;   // none of the seven carries alpha
            importer.isReadable = kind == GroundMap.Mask;             // GroundLaneMesh reads the mask
            importer.SaveAndReimport();
        }

        /// <summary>The artefact: what the importers hold now, read back rather than remembered.</summary>
        public static string ProbeGround()
        {
            var sb = new StringBuilder();
            sb.AppendLine("M29 ground import — read back from the importers:");
            string[] files =
            {
                "T_Ground_Field.png", "T_Ground_Field_N.png", "T_Ground_Field_R.png",
                "T_Ground_Lane.png", "T_Ground_Lane_N.png", "T_Ground_Lane_R.png",
                "T_Ground_LaneEdge.png"
            };
            foreach (var f in files)
            {
                var importer = AssetImporter.GetAtPath(Dir + f) as TextureImporter;
                if (importer == null) { sb.AppendLine("  " + f + " MISSING"); continue; }
                var s = new TextureImporterSettings();
                importer.ReadTextureSettings(s);
                sb.AppendLine("  " + f +
                              " type=" + s.textureType +
                              " sRGB=" + s.sRGBTexture +
                              " wrap=" + s.wrapMode +
                              " mips=" + s.mipmapEnabled +
                              " readable=" + importer.isReadable +
                              " flipGreen=" + s.flipGreenChannel +
                              " max=" + importer.maxTextureSize +
                              " compression=" + importer.textureCompression);
            }
            return sb.ToString();
        }
    }
}
#endif
