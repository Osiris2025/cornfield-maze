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
    /// T5: creates the explicit URP/Lit cookie materials — the model must NOT carry an embedded
    /// Standard-shader material (that renders as flat magenta in a URP project).
    ///
    /// Run (batchmode):
    ///   Unity -batchmode -quit -projectPath . -logFile Builds/asset-probe.log \
    ///     -executeMethod CornMaze.EditorTools.CornMazeAssetImport.ApplySettingsMaterialsAndProbe
    /// </summary>
    public static class CornMazeAssetImport
    {
        // T5 moved the whole asset folder under Resources/ so the runtime builder can reach the
        // model and the materials through Resources.Load (this project builds its world in code and
        // owns no serialized scene references).
        const string ModelPath = "Assets/Resources/GingerbreadMan/gb_man.fbx";
        const string ColorTexPath = "Assets/Resources/GingerbreadMan/textures/gb_man_color_512.png";
        const string NormalTexPath = "Assets/Resources/GingerbreadMan/textures/gb_man_normals_512.png";
        const string DoughMatPath = "Assets/Resources/GingerbreadMan/mat_cookie_dough.mat";
        const string IcingMatPath = "Assets/Resources/GingerbreadMan/mat_cookie_icing.mat";

        const int PhoneMaxSize = 512;

        // ----- T4c -------------------------------------------------------------------------

        [MenuItem("Corn Maze/T4c: Apply cookie phone import settings")]
        public static void ApplyImportSettings()
        {
            ConfigureTexture(ColorTexPath, isNormalMap: false);
            ConfigureTexture(NormalTexPath, isNormalMap: true);
            ConfigureModel(ModelPath);
            AssetDatabase.SaveAssets();
            Debug.Log("GINGERBREAD-T4C: import settings applied (maxTextureSize " + PhoneMaxSize +
                      ", normals flagged NormalMap, model globalScale 1 / useFileScale 1 / Generic / materialImportMode None).");
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
                // Not sRGB, and flagged as a normal map, or the shading is wrong (flat/washed
                // surface instead of baked relief).
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

        // ----- T5: the cookie's own URP materials -------------------------------------------

        [MenuItem("Corn Maze/T5: Create cookie materials")]
        public static void CreateCookieMaterials()
        {
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(ColorTexPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexPath);
            if (color == null) Debug.LogError("GINGERBREAD-T5: colour map missing at " + ColorTexPath);
            if (normal == null) Debug.LogError("GINGERBREAD-T5: normal map missing at " + NormalTexPath);

            // Dough and icing are separate material assets on purpose: the rain dissolve must wash
            // the icing off FIRST, and the icing is its own mesh (gb_man_decoration), so it needs its
            // own material instance to tint and fade independently.
            CreateOrUpdateMaterial(DoughMatPath, color, normal, smoothness: 0.12f, name: "cookie_dough");
            CreateOrUpdateMaterial(IcingMatPath, color, normal, smoothness: 0.38f, name: "cookie_icing");
            AssetDatabase.SaveAssets();
            Debug.Log("GINGERBREAD-T5: created URP/Lit cookie materials " + DoughMatPath + " and " + IcingMatPath);
        }

        static Material CreateOrUpdateMaterial(string path, Texture2D map, Texture2D bump, float smoothness, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("GINGERBREAD-T5: URP/Lit shader not found — is the project on URP?");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            mat.shader = shader;

            mat.SetTexture("_BaseMap", map);            // the colour map
            mat.SetColor("_BaseColor", Color.white);    // the texture carries the dough colour
            if (bump != null)
            {
                mat.SetTexture("_BumpMap", bump);       // the normal map (flagged NormalMap in T4c)
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Surface", 0f);               // opaque; the dissolve flips this at runtime
            mat.renderQueue = -1;

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // ----- the probe: read the importer and the imported asset back ---------------------

        // ----- the iOS native plugin meta: rebuild it in Unity's own format ------------------
        // The pre-existing Assets/Plugins/iOS/IOSAudioSession.mm.meta is malformed YAML
        // ("Unexpected scalar when reading mapping"), which throws inside URP's
        // ShaderBuildPreprocessor and fails the WHOLE build ("Build Finished, Result: Failure.")
        // before any player is produced. Let the importer rewrite its own meta rather than
        // hand-editing YAML: Unity only ever emits a format it can read back.

        public static void FixIosPluginImporter()
        {
            const string pluginPath = "Assets/Plugins/iOS/IOSAudioSession.mm";

            var importer = AssetImporter.GetAtPath(pluginPath) as PluginImporter;
            if (importer == null)
            {
                Debug.LogError("CORN-PLUGIN: AssetImporter.GetAtPath('" + pluginPath +
                               "') did not give a PluginImporter — the malformed .meta cannot be repaired this way.");
                return;
            }

            var before = importer.GetCompatibleWithPlatform(BuildTarget.iOS);
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
            importer.SetPlatformData(BuildTarget.iOS, "AddToEmbeddedBinaries", "false");
            importer.SetPlatformData(BuildTarget.iOS, "CPU", "AnyCPU");
            importer.SetPlatformData(BuildTarget.iOS, "CompileFlags", "");
            importer.SetPlatformData(BuildTarget.iOS, "FrameworkDependencies", "AVFoundation");
            importer.SaveAndReimport();

            var check = AssetImporter.GetAtPath(pluginPath) as PluginImporter;
            Debug.Log("CORN-PLUGIN: " + pluginPath +
                      " | iOS before=" + before + " after=" + (check != null && check.GetCompatibleWithPlatform(BuildTarget.iOS)) +
                      " anyPlatform=" + (check != null && check.GetCompatibleWithAnyPlatform()) +
                      " editor=" + (check != null && check.GetCompatibleWithEditor()) +
                      " frameworks='" + (check != null ? check.GetPlatformData(BuildTarget.iOS, "FrameworkDependencies") : "?") + "'" +
                      " importerType=" + (check != null ? check.GetType().Name : "<null>"));
        }

        public static void ApplySettingsMaterialsAndProbe()
        {
            ApplyImportSettings();
            CreateCookieMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ProbeGingerbread();
        }

        [MenuItem("Corn Maze/T4c+T5: Probe cookie import state")]
        public static void ProbeGingerbread()
        {
            var sb = new StringBuilder();
            sb.AppendLine("GINGERBREAD-PROBE: ===== BEGIN =====");

            AppendTextureReport(sb, ColorTexPath);
            AppendTextureReport(sb, NormalTexPath);
            AppendModelImporterReport(sb, ModelPath);
            AppendMaterialReport(sb, DoughMatPath);
            AppendMaterialReport(sb, IcingMatPath);
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

        static void AppendMaterialReport(StringBuilder sb, string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            sb.AppendLine("GINGERBREAD-PROBE: material " + path);
            if (mat == null)
            {
                sb.AppendLine("GINGERBREAD-PROBE:   MISSING");
                return;
            }
            sb.AppendLine("GINGERBREAD-PROBE:   shader='" + mat.shader.name + "'");
            var baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            var bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            sb.AppendLine("GINGERBREAD-PROBE:   _BaseMap=" + (baseMap != null ? baseMap.name : "<null>") +
                          " _BumpMap=" + (bumpMap != null ? bumpMap.name : "<null>"));
            sb.AppendLine("GINGERBREAD-PROBE:   _Smoothness=" + (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness").ToString() : "n/a") +
                          " _Metallic=" + (mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic").ToString() : "n/a") +
                          " _NormalMapKeyword=" + mat.IsKeywordEnabled("_NORMALMAP"));
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
            var renderersAll = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderersAll.Length > 0 ? renderersAll[0].bounds : new Bounds();
            for (int i = 1; i < renderersAll.Length; i++) bounds.Encapsulate(renderersAll[i].bounds);

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
                var lb = smr.localBounds;
                sb.AppendLine("GINGERBREAD-PROBE:     localBoundsSize=" + lb.size + " center=" + lb.center);
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            sb.AppendLine("GINGERBREAD-PROBE:   transforms=" + transforms.Length + " (bone slots total=" + totalBones + ")");
            sb.AppendLine("GINGERBREAD-PROBE:   worldBoundsAtScale1 size=" + bounds.size +
                          " min=" + bounds.min + " max=" + bounds.max + " height=" + bounds.size.y);
            sb.AppendLine("GINGERBREAD-PROBE:   hierarchy:");
            foreach (var t in transforms)
                sb.AppendLine("GINGERBREAD-PROBE:     " + PathOf(t, root.transform));

            // Rest pose of the joints the animation drives: the animation must compose onto this,
            // not overwrite it (an FBX rig's rest rotations are not identity).
            sb.AppendLine("GINGERBREAD-PROBE:   joint rest pose (name | localPosition | localEuler):");
            string[] joints =
            {
                "spine01", "spine02", "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R",
                "hip.L", "hip.R", "chin.L", "chin.R", "foot.L", "foot.R", "neck", "head"
            };
            foreach (var jointName in joints)
            {
                var t = FindByName(root.transform, jointName);
                if (t == null)
                {
                    sb.AppendLine("GINGERBREAD-PROBE:     " + jointName + " | <not found>");
                    continue;
                }
                sb.AppendLine("GINGERBREAD-PROBE:     " + jointName + " | pos=" + t.localPosition +
                              " | euler=" + t.localRotation.eulerAngles + " | worldY=" + t.position.y.ToString("0.###"));
            }
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
