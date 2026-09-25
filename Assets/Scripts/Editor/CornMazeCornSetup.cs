#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CornMaze.EditorTools
{
    /// <summary>
    /// M20 (FSD §24): the corn field. This is the editor half — the import settings the FBX cannot
    /// carry (Cutout, normal maps as normal maps), the two material assets, and the six runtime
    /// prefabs with their LOD chains wired. The runtime half is MazeWorldBuilder.PlantCornBlocks().
    ///
    /// Run (batchmode):
    ///   Unity -batchmode -quit -projectPath . -logFile Builds/corn-setup.log \
    ///     -executeMethod CornMaze.EditorTools.CornMazeCornSetup.ProbeCorn
    /// </summary>
    public static class CornMazeCornSetup
    {
        const string CornDir = "Assets/Corn";
        const string BlocksDir = CornDir + "/Blocks";
        const string TexDir = CornDir + "/Textures";
        const string MatDir = CornDir + "/Materials";
        const string ResDir = "Assets/Resources/Corn";

        const string AlbedoOld = TexDir + "/T_Corn_01_D.png";
        const string NormalOld = TexDir + "/T_Corn_01_NRM.png";
        const string AlbedoCcc = TexDir + "/corn_texture.png";

        const string MatOld = MatDir + "/corn_old.mat";
        const string MatCcc = MatDir + "/corn_corn_corn.mat";

        /// <summary>Alpha clip threshold §24.4 asks for: 0.5, on the leaf cards.</summary>
        public const float AlphaClip = 0.5f;

        static string BlockPath(int index, string lod = "")
            => BlocksDir + "/CornBlock_2m_" + index.ToString("00") + lod + ".fbx";

        // ----- probe: read the importers and the blocks back ---------------------------------

        /// <summary>One-shot for batchmode: settings → materials → prefabs → probe.</summary>
        public static void SetupAll()
        {
            ApplyTextureImportSettings();
            CreateCornMaterials();
            BuildCornPrefabs();
            ProbeCorn();
        }

        [MenuItem("Corn Maze/M20: Probe corn import + block slots")]
        public static void ProbeCorn()
        {
            var sb = new StringBuilder();
            sb.AppendLine("CORN-PROBE: ===== BEGIN =====");

            AppendTexture(sb, AlbedoOld);
            AppendTexture(sb, NormalOld);
            AppendTexture(sb, AlbedoCcc);

            for (int i = 1; i <= 6; i++)
                AppendBlock(sb, i, "");
            AppendBlock(sb, 1, "_LOD1");
            AppendBlock(sb, 1, "_LOD2");

            AppendMaterial(sb, MatOld);
            AppendMaterial(sb, MatCcc);

            for (int i = 1; i <= 6; i++)
                AppendCornPrefab(sb, ResDir + "/CornBlock_2m_" + i.ToString("00") + ".prefab", i == 1);

            sb.AppendLine("CORN-PROBE: ===== END =====");
            Debug.Log(sb.ToString());
        }

        static void AppendTexture(StringBuilder sb, string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            sb.AppendLine("CORN-PROBE: texture " + path);
            if (importer == null) { sb.AppendLine("CORN-PROBE:   NO TextureImporter"); return; }
            sb.AppendLine("CORN-PROBE:   maxTextureSize=" + importer.maxTextureSize +
                          " textureType=" + (int)importer.textureType + " (" + importer.textureType + ")" +
                          " sRGB=" + importer.sRGBTexture + " mipmaps=" + importer.mipmapEnabled +
                          " compression=" + importer.textureCompression +
                          " alphaSource=" + importer.alphaSource + " alphaIsTransparency=" + importer.alphaIsTransparency);
        }

        static void AppendMaterial(StringBuilder sb, string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            sb.AppendLine("CORN-PROBE: material " + path);
            if (mat == null) { sb.AppendLine("CORN-PROBE:   MISSING"); return; }
            sb.AppendLine("CORN-PROBE:   shader='" + mat.shader.name + "' instancing=" + mat.enableInstancing +
                          " surface=" + (mat.HasProperty("_Surface") ? mat.GetFloat("_Surface").ToString() : "n/a") +
                          " alphaClip=" + (mat.HasProperty("_AlphaClip") ? mat.GetFloat("_AlphaClip").ToString() : "n/a") +
                          " cutoff=" + (mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff").ToString() : "n/a") +
                          " keywordALPHATEST=" + mat.IsKeywordEnabled("_ALPHATEST_ON"));
            sb.AppendLine("CORN-PROBE:   baseMap=" + TexName(mat, "_BaseMap") + " bumpMap=" + TexName(mat, "_BumpMap"));
        }

        /// <summary>
        /// Reads the PREFAB back — the artefact that actually ships. The LOD chain, the two materials and
        /// the Cutout settings live there, not in the FBX.
        /// </summary>
        static void AppendCornPrefab(StringBuilder sb, string path, bool verbose)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("CORN-PROBE: prefab " + path);
            if (prefab == null) { sb.AppendLine("CORN-PROBE:   MISSING"); return; }

            var group = prefab.GetComponent<LODGroup>();
            if (group == null) { sb.AppendLine("CORN-PROBE:   NO LODGroup"); return; }

            var lods = group.GetLODs();
            sb.AppendLine("CORN-PROBE:   lodLevels=" + lods.Length);
            int total = 0;
            for (int i = 0; i < lods.Length; i++)
            {
                int tris = 0, renderers = lods[i].renderers.Length;
                foreach (var r in lods[i].renderers)
                {
                    var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
                    total += tris;
                }
                sb.AppendLine("CORN-PROBE:   LOD" + i + " height=" + lods[i].screenRelativeTransitionHeight.ToString("0.00") +
                              " renderers=" + renderers + " tris=" + tris);
            }
            sb.AppendLine("CORN-PROBE:   trisAllLods=" + total);

            if (!verbose) return;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int s = 0; s < mats.Length; s++)
                {
                    var m = mats[s];
                    sb.AppendLine("CORN-PROBE:   " + r.name + "[" + s + "] " +
                                  (m == null ? "<null>" : m.name + " surface=" + m.GetFloat("_Surface") +
                                   " alphaClip=" + m.GetFloat("_AlphaClip") +
                                   " cutoff=" + m.GetFloat("_Cutoff") +
                                   " ALPHATEST=" + m.IsKeywordEnabled("_ALPHATEST_ON") +
                                   " instancing=" + m.enableInstancing +
                                   " baseMap=" + TexName(m, "_BaseMap") +
                                   " bumpMap=" + TexName(m, "_BumpMap")));
                }
            }
        }

        static string TexName(Material mat, string prop)
        {
            if (!mat.HasProperty(prop)) return "n/a";
            var t = mat.GetTexture(prop);
            return t == null ? "<null>" : t.name;
        }

        static void AppendBlock(StringBuilder sb, int index, string lod)
        {
            var path = BlockPath(index, lod);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("CORN-PROBE: block " + path);
            if (root == null) { sb.AppendLine("CORN-PROBE:   could not load"); return; }

            int tris = 0, verts = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    verts += mesh.vertexCount;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
                }
            }
            sb.AppendLine("CORN-PROBE:   renderers=" + renderers.Length + " verts=" + verts + " tris=" + tris);

            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                var names = new List<string>();
                foreach (var m in mats)
                    names.Add(m == null ? "<null>" : (m.name + "[" + m.shader.name + "/" + TexName(m, "_BaseMap") + "]"));
                sb.AppendLine("CORN-PROBE:   renderer '" + r.name + "' slots=" + mats.Length + " -> " + string.Join(", ", names));
            }

            var bounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            if (!first)
                sb.AppendLine("CORN-PROBE:   bounds size=" + bounds.size.ToString("0.000") + " min=" + bounds.min.ToString("0.000"));
        }

        // ----- the import settings the FBX cannot carry --------------------------------------

        [MenuItem("Corn Maze/M20: Apply corn texture import settings")]
        public static void ApplyTextureImportSettings()
        {
            ConfigureTexture(AlbedoOld, normal: false, useAlpha: false);
            ConfigureTexture(NormalOld, normal: true, useAlpha: false);
            ConfigureTexture(AlbedoCcc, normal: false, useAlpha: true);
            AssetDatabase.SaveAssets();
            Debug.Log("CORN-M20: texture import settings applied (normal map flagged Normal + no sRGB; " +
                      "the corn-corn-corn albedo keeps its alpha for the Cutout cards).");
        }

        static void ConfigureTexture(string path, bool normal, bool useAlpha)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError("CORN-M20: no TextureImporter at " + path); return; }

            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            if (normal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.convertToNormalmap = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = useAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = useAlpha;
            }
            importer.SaveAndReimport();
        }

        // ----- the two material assets --------------------------------------------------------

        [MenuItem("Corn Maze/M20: Create corn materials")]
        public static void CreateCornMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                AssetDatabase.CreateFolder(CornDir, "Materials");
                AssetDatabase.Refresh();
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoOld);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalOld);
            var cccAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoCcc);

            CreateCornMaterial(MatOld, albedo, normal, new Color(1f, 1f, 1f), cutout: true);
            CreateCornMaterial(MatCcc, cccAlbedo, null, new Color(1f, 1f, 1f), cutout: true);

            AssetDatabase.SaveAssets();
            Debug.Log("CORN-M20: corn materials created at " + MatOld + " and " + MatCcc);
        }

        static void CreateCornMaterial(string path, Texture2D map, Texture2D bump, Color tint, bool cutout)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("CORN-M20: URP/Lit not found"); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            mat.shader = shader;

            mat.SetTexture("_BaseMap", map);
            mat.SetColor("_BaseColor", tint);
            if (bump != null)
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1f);
            }
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.10f);

            if (cutout)
            {
                // §24.4: the leaf cards MUST clip, or every leaf renders as a solid slab. The FBX
                // cannot carry this, which is exactly why it lives here.
                mat.SetFloat("_Surface", 0f);            // opaque + alpha clip (not blended)
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", AlphaClip);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = -1;
            }

            // 1 144 block instances per maze (§24.2) — instancing is not optional.
            mat.enableInstancing = true;

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
        }

        // ----- the six runtime prefabs, LOD chain wired ---------------------------------------

        /// <summary>
        /// The FBX slot order is NOT the same in every block (probed 2026-09-24: 01/04/05/06 come out
        /// [Maize, corn], 02/03 come out [corn, Maize]). So the material is chosen by the TEXTURE the
        /// embedded material references, never by slot index — the README warns the slot names are
        /// misleading, and the probe shows they are.
        /// </summary>
        [MenuItem("Corn Maze/M20: Build corn prefabs (LOD chain + materials)")]
        public static void BuildCornPrefabs()
        {
            Directory.CreateDirectory(ResDir);
            var old = AssetDatabase.LoadAssetAtPath<Material>(MatOld);
            var ccc = AssetDatabase.LoadAssetAtPath<Material>(MatCcc);
            if (old == null || ccc == null)
            {
                Debug.LogError("CORN-M20: materials missing — run CreateCornMaterials first");
                return;
            }

            // 1. Record which slot is which BEFORE the embedded materials are dropped. A previous run may
            //    already have dropped them, in which case put them back for the read: the FBX's own slot
            //    names are the only source of truth for slot order, and it is NOT the same in every block.
            var slotIsCcc = new Dictionary<string, bool[]>();
            for (int i = 1; i <= 6; i++)
            {
                var flags = ReadSlotIdentity(i);
                slotIsCcc[BlockPath(i)] = flags;
                Debug.Log("CORN-M20: block " + i.ToString("00") + " slot class -> [" +
                          string.Join(",", System.Array.ConvertAll(flags, f => f ? "ccc" : "old")) + "]");
            }

            // 2. Drop the embedded material descriptions: the prefabs will carry ours. The FBX in
            //    Editor stays exactly as the generator wrote it — the blocks themselves are untouched.
            for (int i = 1; i <= 6; i++)
            {
                SetMaterialImportMode(BlockPath(i));
                SetMaterialImportMode(BlockPath(i, "_LOD1"));
                SetMaterialImportMode(BlockPath(i, "_LOD2"));
            }
            AssetDatabase.Refresh();

            for (int i = 1; i <= 6; i++)
                BuildOneCornPrefab(i, old, ccc, slotIsCcc[BlockPath(i)]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CORN-M20: six corn prefabs built in " + ResDir + " with LOD0/1/2 wired");
        }

        static void SetMaterialImportMode(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        static void RestoreMaterialDescriptions(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Slot order differs per block (probed: 01/04/05/06 come out [Maize, corn], 02/03 [corn, Maize]).
        /// Classify by the TEXTURE the embedded material points at, falling back to its NAME, and restore
        /// the material descriptions first if an earlier run already dropped them.
        /// </summary>
        static bool[] ReadSlotIdentity(int index)
        {
            var flags = ReadSlotTextureIdentity(BlockPath(index));
            if (flags != null) return flags;

            for (int lod = 0; lod < 3; lod++)
            {
                string suffix = lod == 0 ? "" : (lod == 1 ? "_LOD1" : "_LOD2");
                RestoreMaterialDescriptions(BlockPath(index, suffix));
            }
            AssetDatabase.Refresh();

            var after = ReadSlotTextureIdentity(BlockPath(index));
            if (after == null)
                Debug.LogError("CORN-M20: block " + index + " carries no readable material description — " +
                               "slot class cannot be determined; materials will be wrong.");
            return after ?? new bool[] { false };
        }

        static bool[] ReadSlotTextureIdentity(string blockPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(blockPath);
            if (root == null) return null;
            var flags = new List<bool>();
            bool sawMaterial = false, sawTexture = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null) sawMaterial = true;
                    string tex = m == null ? "" : TexName(m, "_BaseMap").ToLowerInvariant();
                    string nm = m == null ? "" : m.name.ToLowerInvariant();
                    if (tex.Length > 0 && tex != "<null>") sawTexture = true;
                    // Texture first; if the FBX carried no texture reference, fall back to the measured
                    // slot naming (the slot literally named "corn" is the corn-corn-corn card set).
                    flags.Add(tex.Contains("corn_texture") ||
                              (tex.Length == 0 && nm == "corn"));
                }
            // No TEXTURE anywhere means the material descriptions are gone (Unity fell back to its default
            // Lit), and a read like that tells us NOTHING about the slot order.
            return sawTexture ? flags.ToArray() : null;
        }

        static void BuildOneCornPrefab(int index, Material old, Material ccc, bool[] cccSlots)
        {
            var full = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath(index));
            var lod1 = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath(index, "_LOD1"));
            var lod2 = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath(index, "_LOD2"));
            if (full == null || lod1 == null || lod2 == null)
            {
                Debug.LogError("CORN-M20: block " + index + " is missing an LOD");
                return;
            }

            var go = new GameObject("CornBlock_2m_" + index.ToString("00"));
            var l0 = Object.Instantiate(full, go.transform);
            var l1 = Object.Instantiate(lod1, go.transform);
            var l2 = Object.Instantiate(lod2, go.transform);
            l0.name = "LOD0"; l1.name = "LOD1"; l2.name = "LOD2";

            AssignMaterials(l0, old, ccc, cccSlots);
            AssignMaterials(l1, old, ccc, cccSlots);
            AssignMaterials(l2, old, ccc, cccSlots);

            var group = go.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.50f, RenderersOf(l0)),
                new LOD(0.16f, RenderersOf(l1)),
                new LOD(0.02f, RenderersOf(l2))
            });
            group.RecalculateBounds();
            group.fadeMode = LODFadeMode.None;

            int tris = TrisOf(l0) + TrisOf(l1) + TrisOf(l2);

            var path = ResDir + "/CornBlock_2m_" + index.ToString("00") + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log("CORN-M20: prefab " + path + " — tris " + tris + " across 3 LODs");
        }

        static void AssignMaterials(GameObject root, Material old, Material ccc, bool[] cccSlots)
        {
            int slot = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int s = 0; s < mats.Length; s++)
                {
                    bool isCcc = slot < cccSlots.Length && cccSlots[slot];
                    mats[s] = isCcc ? ccc : old;
                    slot++;
                }
                r.sharedMaterials = mats;
            }
        }

        static Renderer[] RenderersOf(GameObject go) => go.GetComponentsInChildren<Renderer>(true);

        static int TrisOf(GameObject go)
        {
            int tris = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
            }
            return tris;
        }
    }
}
#endif
