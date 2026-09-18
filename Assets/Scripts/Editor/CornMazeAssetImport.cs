#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CornMaze.EditorTools
{
    /// <summary>
    /// T4c: the phone import settings for the supplied gingerbread cookie, plus the probe that
    /// reads the importer's OWN state back. "It imported fine" is not verification; the values
    /// printed by ProbeGingerbread() are the artefact.
    ///
    /// Run (batchmode):
    ///   Unity -batchmode -quit -projectPath . -logFile Builds/asset-probe.log \
    ///     -executeMethod CornMaze.EditorTools.CornMazeAssetImport.ApplyImportSettingsAndProbe
    /// </summary>
    public static class CornMazeAssetImport
    {
        const string ColorTexPath = "Assets/GingerbreadMan/textures/gb_man_color_512.png";
        const string NormalTexPath = "Assets/GingerbreadMan/textures/gb_man_normals_512.png";
        const string ModelPath = "Assets/GingerbreadMan/gb_man.fbx";

        const int PhoneMaxSize = 512;

        [MenuItem("Corn Maze/T4c: Apply cookie phone import settings")]
        public static void ApplyImportSettings()
        {
            ConfigureTexture(ColorTexPath, isNormalMap: false);
            ConfigureTexture(NormalTexPath, isNormalMap: true);
            ConfigureModel(ModelPath);
            AssetDatabase.SaveAssets();
            Debug.Log("GINGERBREAD-T4C: import settings applied (maxTextureSize " + PhoneMaxSize +
                      ", normals flagged NormalMap, model globalScale 1 / useFileScale 1 / Generic).");
        }

        public static void ApplyImportSettingsAndProbe()
        {
            ApplyImportSettings();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ProbeGingerbread();
        }

        static void ConfigureTexture(string path, bool isNormalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("GINGERBREAD-T4C: no TextureImporter at " + path);
                return;
            }

            // A phone target: 512 is plenty for a low-poly character seen at third-person distance.
            importer.maxTextureSize = PhoneMaxSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;

            if (isNormalMap)
            {
                // Not sRGB, flagged as a normal map, or the shading is wrong (and it renders as a
                // flat/washed surface rather than baked relief).
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.convertToNormalmap = false;
                importer.normalmapFilter = TextureImporterNormalFilter.Standard;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
            }

            importer.SaveAndReimport();
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("GINGERBREAD-T4C: no ModelImporter at " + path);
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            // Motion is authored in code (the file carries no clips), so Generic is right and the
            // Humanoid auto-mapping is deliberately not chased.
            importer.animationType = ModelImporterAnimationType.Generic;
            // None: do NOT let Unity embed/extract a Standard-shader material — that is the magenta
            // landmine in a URP project. The cookie's material is an explicit URP/Lit asset we own.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        [MenuItem("Corn Maze/T4c: Probe cookie import state")]
        public static void ProbeGingerbread()
        {
            var sb = new StringBuilder();
            sb.AppendLine("GINGERBREAD-PROBE: ===== BEGIN =====");

            AppendTextureReport(sb, ColorTexPath);
            AppendTextureReport(sb, NormalTexPath);
            AppendModelImporterReport(sb, ModelPath);
            AppendModelContentReport(sb, ModelPath);

            sb.AppendLine("GINGERBREAD-PROBE: ===== END =====");
            Debug.Log(sb.ToString());
        }

        static void AppendTextureReport(StringBuilder sb, string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            sb.AppendLine("GINGERBREAD-PROBE: texture " + path);
            if (importer == null)
            {
                sb.AppendLine("GINGERBREAD-PROBE:   NO TextureImporter (missing?)");
                return;
            }
            sb.AppendLine("GINGERBREAD-PROBE:   maxTextureSize=" + importer.maxTextureSize);
            sb.AppendLine("GINGERBREAD-PROBE:   textureType=" + (int)importer.textureType + " (" + importer.textureType + ")");
            sb.AppendLine("GINGERBREAD-PROBE:   sRGBTexture=" + importer.sRGBTexture);
            sb.AppendLine("GINGERBREAD-PROBE:   mipmapEnabled=" + importer.mipmapEnabled);
            sb.AppendLine("GINGERBREAD-PROBE:   isReadable=" + importer.isReadable);
            sb.AppendLine("GINGERBREAD-PROBE:   textureCompression=" + importer.textureCompression);
            sb.AppendLine("GINGERBREAD-PROBE:   npotScale=" + importer.npotScale);
            sb.AppendLine("GINGERBREAD-PROBE:   wrapMode=" + importer.wrapMode);
            sb.AppendLine("GINGERBREAD-PROBE:   normalmapFilter=" + (int)importer.normalmapFilter);
            var def = importer.GetPlatformTextureSettings("DefaultTexturePlatform");
            if (def != null)
                sb.AppendLine("GINGERBREAD-PROBE:   platform[Default] overridden=" + def.overridden +
                              " maxTextureSize=" + def.maxTextureSize + " format=" + def.format +
                              " compression=" + def.textureCompression);
        }

        static void AppendModelImporterReport(StringBuilder sb, string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            sb.AppendLine("GINGERBREAD-PROBE: model " + path);
            if (importer == null)
            {
                sb.AppendLine("GINGERBREAD-PROBE:   NO ModelImporter (missing?)");
                return;
            }
            sb.AppendLine("GINGERBREAD-PROBE:   animationType=" + (int)importer.animationType + " (" + importer.animationType + ")");
            sb.AppendLine("GINGERBREAD-PROBE:   globalScale=" + importer.globalScale);
            sb.AppendLine("GINGERBREAD-PROBE:   useFileScale=" + importer.useFileScale);
            sb.AppendLine("GINGERBREAD-PROBE:   materialImportMode=" + (int)importer.materialImportMode + " (" + importer.materialImportMode + ")");
            sb.AppendLine("GINGERBREAD-PROBE:   importAnimation=" + importer.importAnimation);
            sb.AppendLine("GINGERBREAD-PROBE:   isReadable=" + importer.isReadable);
        }

        static void AppendModelContentReport(StringBuilder sb, string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
            {
                sb.AppendLine("GINGERBREAD-PROBE:   could not load model GameObject at " + path);
                return;
            }

            sb.AppendLine("GINGERBREAD-PROBE:   root='" + root.name + "'");
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine("GINGERBREAD-PROBE:   skinnedMeshRenderers=" + renderers.Length);

            int totalBones = 0;
            foreach (var smr in renderers)
            {
                var mesh = smr.sharedMesh;
                int subs = mesh != null ? mesh.subMeshCount : -1;
                int verts = mesh != null ? mesh.vertexCount : -1;
                int tris = mesh != null ? mesh.triangles.Length / 3 : -1;
                totalBones += smr.bones != null ? smr.bones.Length : 0;
                sb.AppendLine("GINGERBREAD-PROBE:   renderer '" + smr.name + "' mesh='" + (mesh != null ? mesh.name : "<null>") +
                              "' subMeshes=" + subs + " verts=" + verts + " tris=" + tris +
                              " bones=" + (smr.bones != null ? smr.bones.Length : 0));
                var mat = smr.sharedMaterial;
                sb.AppendLine("GINGERBREAD-PROBE:     material=" + (mat != null ? ("'" + mat.name + "' shader='" + mat.shader.name + "'") : "<null>"));
                var b = smr.localBounds;
                sb.AppendLine("GINGERBREAD-PROBE:     localBoundsSize=" + b.size + " center=" + b.center);
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            sb.AppendLine("GINGERBREAD-PROBE:   transforms=" + transforms.Length + " (bone slots total=" + totalBones + ")");
            sb.AppendLine("GINGERBREAD-PROBE:   hierarchy:");
            foreach (var t in transforms)
                sb.AppendLine("GINGERBREAD-PROBE:     " + PathOf(t, root.transform));

            // Bounds at scale 1, which is what the 5.376/0.335 scale decision rests on.
            var renderersAll = root.GetComponentsInChildren<Renderer>(true);
            if (renderersAll.Length > 0)
            {
                var bounds = renderersAll[0].bounds;
                for (int i = 1; i < renderersAll.Length; i++)
                    bounds.Encapsulate(renderersAll[i].bounds);
                sb.AppendLine("GINGERBREAD-PROBE:   worldBoundsAtScale1 size=" + bounds.size + " (height=" + bounds.size.y + ")");
            }
        }

        static string PathOf(Transform t, Transform root)
        {
            var names = new List<string>();
            var cur = t;
            while (cur != null)
            {
                names.Insert(0, cur.name);
                if (cur == root) break;
                cur = cur.parent;
            }
            return string.Join("/", names) + (t.childCount == 0 ? "" : "  (" + t.childCount + " children)");
        }
    }
}
#endif
