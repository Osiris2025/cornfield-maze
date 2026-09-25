using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M32 (§17): the ground catches the moonlight — measured on the built Mac app.
///
///     "&lt;app&gt;" -refltest
///
/// Todd: "can you use the PBR files for reflections off the moonlight?" Until this milestone the ground
/// carried a constant `_Smoothness` per material and the sets' roughness never reached the shader, so the
/// floor was uniformly matte under the moon — nothing on the loose stones, nothing on the damp patches. The
/// sets now drive URP's metallic/smoothness map (RGB metallic 0, A = smoothness).
///
/// "It looks shinier" is not evidence, so this harness offers none. It solves for the point on the floor where
/// the moon's reflection actually appears, aims the player at it so the moon sits behind the shoulder and the
/// reflection lands dead centre, then measures the mean AND peak luminance of a window around it with the maps
/// BOUND and UNBOUND — same view, same scene, one variable. Frames are saved from the very texture that was
/// measured, so the number and the picture cannot disagree.
///
/// Two lessons this harness paid for, both kept in the report:
///   * The camera rig follows the player and overwrites any transform written into it: the first version set
///     the camera directly and drifted 76 m, measuring the wrong ground. It now moves the PLAYER and aims
///     through `AimAtForTest`, the game's own look state (M25b hit this same wall).
///   * The wait for a high moon is tens of seconds of standing still, which the Husk treats as an invitation.
///     One run ended with "Caught by the Husk" on screen and the band sampled corn leaves. The Husk is
///     disabled for the measurement and the report says so.
/// </summary>
public class M32ReflectionSelfTest : MonoBehaviour
{
    const string Flag = "-refltest";
    const float NightWaitCap = 80f;
    const float MoonAngleTarget = 20f;   // §25.5 takes the moon to 28 deg; 20 puts the highlight in front
    const int BandPixels = 130;          // the window measured around the mirror point, in pixels
    const float EyeHeight = 1.655f;      // M27: the first-person eye height

    readonly List<string> _lines = new List<string>();

    static bool Wanted()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == Flag) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        if (!Wanted()) return;
        var go = new GameObject("M32ReflectionSelfTest");
        go.AddComponent<M32ReflectionSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m32-reflection-report.txt");

    void Emit(string s)
    {
        _lines.Add(s);
        Debug.Log("[M32] " + s);
    }

    void Finish(int code)
    {
        File.WriteAllText(ReportPath, string.Join("\n", _lines) + "\n");
        Application.Quit(code);
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null;
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        var maze = player != null ? player.Maze : null;
        if (player == null || maze == null)
        {
            Emit("FAIL: no player or maze");
            Finish(1);
            yield break;
        }

        Emit("M32 the ground catches the moonlight — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));

        // ---- the two ground materials, found by what they actually wear ---------------------------------
        Material laneMat = null, fieldMat = null;
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var m = mr.sharedMaterial;
            if (m == null || !m.HasProperty("_BaseMap")) continue;
            var t = m.GetTexture("_BaseMap");
            if (t == null) continue;
            if (t.name == "T_Ground_LaneA" && laneMat == null) laneMat = m;
            if (t.name == "T_Ground_Field" && fieldMat == null) fieldMat = m;
        }
        if (laneMat == null || fieldMat == null)
        {
            Emit("FAIL: ground materials not found (lane=" + (laneMat != null) + " field=" + (fieldMat != null) + ")");
            Finish(1);
            yield break;
        }

        Emit("materials found: lane wears " + laneMat.GetTexture("_BaseMap").name +
             ", field wears " + fieldMat.GetTexture("_BaseMap").name);
        Describe("lane", laneMat);
        Describe("field", fieldMat);

        // ---- the Husk comes off ---------------------------------------------------------------------------
        int husksOff = 0;
        foreach (var h in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
        {
            h.enabled = false;
            husksOff++;
        }
        Emit("husk: disabled " + husksOff + " component(s) so the run cannot end mid-measurement — an earlier " +
             "attempt died here and measured corn leaves");

        // ---- first person, said out loud ------------------------------------------------------------------
        player.SetFirstPerson(true);
        yield return null;
        int modelRenderers = 0;
        foreach (var r in player.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                modelRenderers++;
        Emit("view: firstPerson=" + player.FirstPerson + ", model renderers still drawn=" + modelRenderers +
             " — in third person the player's own body sits on the mirror point");

        // ---- wait for a moon that is actually up ----------------------------------------------------------
        float waited = 0f;
        while (!DuskSky.IsNight && waited < NightWaitCap) { waited += Time.unscaledDeltaTime; yield return null; }
        float dark = waited;
        while (MoonElevation() < MoonAngleTarget && waited < 200f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Emit("moon: night after " + dark.ToString("0.0") + "s, elevation " + MoonElevation().ToString("0.0") +
             " deg after " + waited.ToString("0.0") + "s of play — at 2.6 deg, just after dusk, the reflection " +
             "falls 36 m down the lane and is too glancing to read");

        // ---- put the PLAYER where the reflection lands, and aim the game's own look state at it ----------
        var (centre, halfLane) = FindLane(maze);
        player.transform.position = centre + Vector3.up * 0.03f;
        for (int i = 0; i < 4; i++) yield return null;

        Vector3 toMoon = DuskSky.MoonDirection;
        Vector3 down = Vector3.Reflect(toMoon, Vector3.up).normalized;   // camera -> mirror point, flat floor
        Vector3 eye = player.transform.position + Vector3.up * EyeHeight;
        float s = -eye.y / down.y;
        Vector3 mirror = eye + down * s;

        // Aim, then SEARCH the elevation until the mirror point actually projects to the centre of the frame.
        // Two earlier versions of this got it wrong in ways worth recording: writing the camera's transform
        // directly drifts 76 m because the rig owns it, and correcting the look state by the measured residual
        // drove the pitch between both clamps (-87 to +48) instead of converging — the game's look accessor
        // does not map to camera pitch the way its own sign suggests. So this does not assume a sign: it probes,
        // keeps whichever direction reduces the residual, reverses and halves when it overshoots, and logs every
        // step so a failure is visible as a residual rather than as a quiet wrong number.
        Vector3 flat = new Vector3(mirror.x - eye.x, 0f, mirror.z - eye.z);
        float horiz = flat.magnitude;
        float wantElev = Mathf.Atan2(mirror.y - eye.y, horiz) * Mathf.Rad2Deg;
        float elev = wantElev;
        float step = 8f;
        float prevAbs = float.MaxValue;
        for (int k = 0; k < 14; k++)
        {
            Vector3 aim = eye + flat.normalized * horiz + Vector3.up * (Mathf.Tan(elev * Mathf.Deg2Rad) * horiz);
            player.AimAtForTest(aim);
            for (int i = 0; i < 2; i++) yield return null;
            var pc = player.Camera;
            Vector3 p = pc.WorldToScreenPoint(mirror);
            float errPx = p.y - pc.pixelHeight * 0.5f;
            float abs = Mathf.Abs(errPx);
            Emit("aim probe " + k + ": aim elevation " + elev.ToString("0.0") + " deg (look state reports " +
                 player.PitchForTest.ToString("0.0") + "), mirror point projects to y=" + p.y.ToString("0") +
                 " of " + pc.pixelHeight + " — residual " + errPx.ToString("0") + " px");
            if (abs < 12f) break;
            if (abs > prevAbs) step = -step * 0.5f;   // wrong way or overshot: reverse and halve
            prevAbs = abs;
            elev += step;
        }

        for (int i = 0; i < 3; i++) yield return null;
        Vector3 aimPoint = eye + flat.normalized * horiz + Vector3.up * (Mathf.Tan(elev * Mathf.Deg2Rad) * horiz);
        var cam = player.Camera;
        float drift = Vector3.Distance(cam.transform.position, eye);
        Emit("aim: player moved to the lane and aimed down the reflected ray; mirror point " + F(mirror) + " = " +
             s.ToString("0.00") + " m ahead of an eye at " + EyeHeight + " m. Camera sits " + drift.ToString("0.00") +
             " m from that eye — the rig owns the transform, so this is the check that the shot is where the " +
             "harness thinks it is");
        Vector3 sp = cam.WorldToScreenPoint(mirror);
        Emit("band: " + BandPixels + "x" + BandPixels + " px centred on the mirror point, which projects to " +
             sp.x.ToString("0") + "," + sp.y.ToString("0") + " of " + cam.pixelWidth + "x" + cam.pixelHeight +
             " (screen centre " + (cam.pixelWidth / 2) + "," + (cam.pixelHeight / 2) + ")");

        yield return Sample("m32-reflection-on.png", player, aimPoint, mirror, "BOUND (as shipped)");

        // ---- A/B #1: both maps unbound = the pre-M32 state ------------------------------------------------
        var laneGloss = laneMat.GetTexture("_MetallicGlossMap") as Texture2D;
        var fieldGloss = fieldMat.GetTexture("_MetallicGlossMap") as Texture2D;
        Materials.BindReflection(laneMat, null, Materials.PreM32LaneSmoothness);
        Materials.BindReflection(fieldMat, null, Materials.PreM32FieldSmoothness);
        Emit("A/B: both ground materials back to their pre-M32 state — lane _Smoothness=" +
             laneMat.GetFloat("_Smoothness").ToString("0.00") + ", field _Smoothness=" +
             fieldMat.GetFloat("_Smoothness").ToString("0.00") + ", metallic map unbound on both");
        yield return Sample("m32-reflection-off.png", player, aimPoint, mirror, "UNBOUND (pre-M32 constant)");

        // ---- A/B #2: the field rebound, the lane still bare — separates their contributions --------------
        Materials.BindReflection(fieldMat, fieldGloss, Materials.PreM32FieldSmoothness);
        Emit("A/B: field rebound, lane left unbound — separates the two surfaces in the band");
        yield return Sample("m32-reflection-fieldonly.png", player, aimPoint, mirror, "FIELD ONLY (lane unbound)");

        // ---- restore what ships ---------------------------------------------------------------------------
        Materials.BindReflection(laneMat, laneGloss, Materials.PreM32LaneSmoothness);
        Materials.BindReflection(fieldMat, fieldGloss, Materials.PreM32FieldSmoothness);
        Emit("restored to the shipping state: " + Bind(laneMat) + " / " + Bind(fieldMat));

        Emit("VERDICT: band mean luminance " + _unbound.ToString("0.00") + " -> " + _bound.ToString("0.00") +
             " of 255 with the maps bound (" + (_bound - _unbound >= 0 ? "+" : "") +
             (_bound - _unbound).ToString("0.00") + "), and the brightest pixel in the band " + _unboundPeak +
             " -> " + _boundPeak + ". The peak is the statistic that means something on a rough surface: a " +
             "smoothness of 0.28 puts the highlight in a tight lobe, so it raises the peak far more than the " +
             "mean of a 0.9 m window, and the constant 0.11 it replaces laid a weak sheen over the whole lane " +
             "equally — which is what made the floor read as flat. Field alone peaks at " + _fieldOnlyPeak +
             ": the lane crosses it at 87 % opacity (M31), mixing its body colour over the field's highlight.");

        Finish(0);
    }

    static float MoonElevation() =>
        Mathf.Asin(Mathf.Clamp(DuskSky.MoonDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

    // ---- measurement --------------------------------------------------------------------------------

    float _bound = -1f, _unbound = -1f, _fieldOnly = -1f;
    int _boundPeak, _unboundPeak, _fieldOnlyPeak;

    IEnumerator Sample(string file, FarmWalkerController player, Vector3 aimPoint, Vector3 mirror, string label)
    {
        // Re-aim through the game's own look state before every sample, then read the camera back: the rig
        // follows the player, so writing the camera's transform directly is what produced a 76 m drift.
        player.AimAtForTest(aimPoint);
        for (int i = 0; i < 4; i++) yield return null;
        var cam = player.Camera;
        Vector3 eye = player.transform.position + Vector3.up * EyeHeight;
        float drift = Vector3.Distance(cam.transform.position, eye);

        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height;

        Vector3 sp = cam.WorldToScreenPoint(mirror);
        int cx = Mathf.Clamp(Mathf.RoundToInt(sp.x), 0, w - 1);
        int cy = Mathf.Clamp(Mathf.RoundToInt(sp.y), 0, h - 1);
        int half = BandPixels / 2;
        double sum = 0;
        int n = 0, peak = -1;
        for (int y = Mathf.Max(0, cy - half); y <= Mathf.Min(h - 1, cy + half); y++)
        {
            for (int x = Mathf.Max(0, cx - half); x <= Mathf.Min(w - 1, cx + half); x++)
            {
                byte g = px[y * w + x].g;
                sum += g;
                n++;
                if (g > peak) peak = g;
            }
        }
        float mean = (float)(sum / Mathf.Max(1, n));

        var path = Path.Combine(Application.persistentDataPath, file);
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        Object.Destroy(tex);
        Emit("frame " + file + " -> " + new FileInfo(path).Length + " bytes; band mean G over " + n + " px = " +
             mean.ToString("0.00") + " of 255, peak " + peak + "   [" + label + "] (camera " + drift.ToString("0.00") +
             " m from the aimed eye)");

        if (label.StartsWith("BOUND")) { _bound = mean; _boundPeak = peak; }
        else if (label.StartsWith("UNBOUND")) { _unbound = mean; _unboundPeak = peak; }
        else { _fieldOnly = mean; _fieldOnlyPeak = peak; }
    }

    void Describe(string what, Material mat)
    {
        var bm = mat.GetTexture("_BaseMap");
        var nm = mat.GetTexture("_BumpMap");
        float ch = mat.HasProperty("_SmoothnessTextureChannel") ? mat.GetFloat("_SmoothnessTextureChannel") : -1f;
        float sm = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : -1f;
        Emit(what + ": baseMap=" + (bm ? bm.name : "none") +
             " normalMap=" + (nm ? nm.name : "NONE — a reflective floor without normals is a mirror blob") +
             " metallicGlossMap=" + Bind(mat) +
             " smoothnessChannel=" + ch +
             " _Smoothness=" + sm.ToString("0.00") +
             " keyword _METALLICSPECGLOSSMAP=" + (mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP") ? "on" : "OFF") +
             " keyword _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A=" +
             (mat.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") ? "ON (wrong!)" : "off"));
    }

    static string Bind(Material mat)
    {
        var g = mat.GetTexture("_MetallicGlossMap");
        return g == null ? "none" : g.name;
    }

    static string F(Vector3 v) =>
        "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";

    // ---- the straightest east-west run the maze has, where M29/M31 shot --------------------------------

    (Vector3, float) FindLane(MazeData maze)
    {
        int bestX = -1, bestY = -1, bestLen = 0;
        for (int y = 1; y < maze.Height - 1; y++)
        {
            int run = 0, start = -1;
            for (int x = 1; x < maze.Width - 1; x++)
            {
                bool straight = maze.IsPath(x, y) && maze.IsWall[x, y + 1] && maze.IsWall[x, y - 1];
                if (straight)
                {
                    if (run == 0) start = x;
                    run++;
                    if (run > bestLen) { bestLen = run; bestX = start; bestY = y; }
                }
                else run = 0;
            }
        }
        if (bestX < 0) return (maze.CellToWorld(2, 3), 1.04f);
        return (maze.CellToWorld(bestX + bestLen / 2, bestY), 1.04f);
    }
}
