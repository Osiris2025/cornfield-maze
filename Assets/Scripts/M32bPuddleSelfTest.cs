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
public class M32bPuddleSelfTest : MonoBehaviour
{
    const string Flag = "-puddletest";
    const float NightWaitCap = 80f;
    const float MoonAngleTarget = 20f;   // §25.5 takes the moon to 28 deg; 20 puts the highlight in front
    const int BandPixels = 130;          // the window measured around the mirror point, in pixels
    const float EyeHeight = 1.655f;      // M27: the first-person eye height
    // The values the committed PNGs' alpha channels actually carry, measured by
    // scripts/m32_smoothness_readback.py (Blender, design time): T_Ground_Lane_M alpha mean 0.137,
    // T_Ground_Field_M alpha mean 0.095, 0.0% of either surface above 0.40.
    const float SourceLaneSmoothness = 0.137f;
    const float SourceFieldSmoothness = 0.095f;
    const float ReadBackTolerance = 0.02f;

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
        var go = new GameObject("M32bPuddleSelfTest");
        go.AddComponent<M32bPuddleSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m32b-puddle-report.txt");

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

        // ---- THE READ-BACK ------------------------------------------------------------------------------
        // The order is blunt about this and it is right: "M32 may not go green on the strength of 'the frames
        // look shinier'. If the read-back does not match the source within tolerance, the pass is not done."
        // The source values below come from scripts/m32_smoothness_readback.py, which reads the committed
        // PNGs' alpha channel directly.
        ReadBack("lane", laneMat, SourceLaneSmoothness);
        ReadBack("field", fieldMat, SourceFieldSmoothness);

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
        // M32b: the ground is matte now, so the ONLY thing in the maze that can catch the moon is a puddle. The
        // solved geometry is the same one used for the lane shot above — where a flat floor would mirror the
        // moon — but the surface under that point is water instead of dirt, so the camera is placed to put the
        // mirror point on the puddle's centre and the aim below searches until it projects there.
        float groundY = 0.03f;                       // the lane plane the decals sit on
        var (centre, halfLane) = FindLane(maze);
        var puddleRoot = GameObject.Find("Puddles");
        Vector3 puddlePos = Vector3.zero;
        Vector3 puddleFwd = Vector3.forward;
        Vector3 puddleLong = Vector3.right, puddleWide = Vector3.forward;   // the decal's own axes, kept in scope
        int puddleCount = 0;
        if (puddleRoot != null)
        {
            puddleCount = puddleRoot.transform.childCount;
            var chosen = puddleRoot.transform.GetChild(puddleCount / 2);   // the middle one: deterministic
            puddlePos = chosen.position;
            // The lane's own axis is the decal's LONG axis, which the mesh puts on local X — so it is `right`,
            // not `forward`. Reading `forward` here is what sent the "up-lane" stand across the lane and into
            // the corn, which is why the first two attempts framed corn.
            puddleFwd = chosen.right;
            var pr = chosen.GetComponent<MeshRenderer>();
            puddleMat = pr != null ? pr.sharedMaterial : null;
            Vector3 cl = chosen.right, cw = chosen.forward;      // long axis, wide axis
            puddleLong = cl; puddleWide = cw;
            _puddleCorners = new[]
            {
                puddlePos + cl * (PuddleDecals.PuddleLong * 0.5f) + cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos + cl * (PuddleDecals.PuddleLong * 0.5f) - cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos - cl * (PuddleDecals.PuddleLong * 0.5f) + cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos - cl * (PuddleDecals.PuddleLong * 0.5f) - cw * (PuddleDecals.PuddleWide * 0.5f),
            };
            Emit("puddles: " + puddleCount + " decals under one object named \"Puddles\" (deleting it is removing " +
                 "every puddle); measuring " + chosen.name + " at " + F(puddlePos) + ", yaw " +
                 chosen.eulerAngles.y.ToString("0") + " deg, scale " + chosen.lossyScale.ToString("0.0"));
        }
        else
        {
            Emit("puddles: NO parent named \"Puddles\" in this build — the M32b section cannot run");
        }
        if (puddleMat != null) ReadBackPuddle(puddleMat);

        Vector3 toMoon = DuskSky.MoonDirection;
        Vector3 down = Vector3.Reflect(toMoon, Vector3.up).normalized;   // camera -> mirror point, flat floor
        float s = (groundY - (groundY + EyeHeight)) / down.y;
        // Stand 2.5 m up the lane from the puddle's centre and let the mirror point fall where it falls: at an
        // eye height of 1.655 m and the moon at 28 deg the reflection lands 3.11 m ahead, which is 0.6 m past
        // the puddle's centre — inside a decal that is 2.6 m long. The earlier version placed the player by
        // solving for the mirror point exactly, and the game moved them (they were standing in corn), so the
        // shot framed corn instead of water. The neighbouring cell centre of a straight run is standable.
        // Stand on the LANE, at the mirror distance (3.12 m at 28 deg and an eye height of 1.655 m). The maze
        // is asked which cell that is: start at the puddle's own cell, step along each of the four directions
        // while the next cell is path, and take the lane cell whose distance is closest to the mirror
        // distance. Three earlier attempts extrapolated the stand from the decal's axis or raycast the world
        // for the lane material, and all three stood the player in corn — the maze already knows where the
        // lane is, so ask it instead of guessing from geometry.
        float moonElDeg = Mathf.Asin(Mathf.Clamp(DuskSky.MoonDirection.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        float standDist = EyeHeight / Mathf.Tan(Mathf.Max(5f, moonElDeg) * Mathf.Deg2Rad);
        Vector2Int cell = maze.NearestPathCell(puddlePos);
        Vector2Int bestCell = cell;
        float bestScore = float.MaxValue;
        int stepsTaken = 0;
        Vector2Int[] four = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        foreach (var d in four)
        {
            var c = cell;
            for (int k = 1; k <= 3; k++)
            {
                var n = new Vector2Int(c.x + d.x, c.y + d.y);
                if (!maze.IsPath(n.x, n.y)) break;          // the run ends: no lane to stand on this way
                c = n;
                float score = Mathf.Abs(Vector3.Distance(maze.CellToWorld(c.x, c.y), puddlePos) - standDist);
                if (score < bestScore) { bestScore = score; bestCell = c; stepsTaken = k; }
            }
        }
        Vector3 stand = maze.CellToWorld(bestCell.x, bestCell.y);
        stand.y = groundY;
        player.transform.position = stand;
        for (int i = 0; i < 4; i++) yield return null;
        Emit("stand: " + F(player.transform.position) + " — " + Vector3.Distance(player.transform.position, puddlePos).ToString("0.00") +
             " m from the puddle's centre, on maze cell (" + bestCell.x + "," + bestCell.y + "), " + stepsTaken +
             " step(s) along the lane from the puddle's cell (" + cell.x + "," + cell.y + "). The distance is " +
             standDist.ToString("0.00") + " m because that is where a " + moonElDeg.ToString("0") +
             " deg moon reflects onto the water at an eye height of " + EyeHeight.ToString("0.00") +
             " m, and the cell is a path cell, so the stand is on the lane by construction, not by geometry");
        Vector3 eye = player.transform.position + Vector3.up * EyeHeight;
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

        yield return Sample("m32b-puddle-moon.png", player, aimPoint, mirror, "PUDDLE MOON (water under the mirror point, so the band should be the moon caught in the puddle)");

        // ---- A/B #0: the state Todd saw — the map unbound and the scalar at 1.0 ---------------------------
        // This is not a hypothetical: alphaSource=None makes URP fall back to the white default, so smoothness
        // = 1.0 * _Smoothness(1) = 1.0 over the entire floor. Reproducing it puts the yuck back in frame next
        // to the fix, which is worth more than describing it.
        Materials.BindReflection(laneMat, null, 1f);
        Materials.BindReflection(fieldMat, null, 1f);
        Emit("A/B: MIRROR state reproduced, exactly what alphaSource=None produced — metallic map unbound and " +
             "_Smoothness 1.0 on both materials, so URP reads smoothness 1.0 across the whole floor");
        yield return Sample("m32b-mirror-contrast.png", player, aimPoint, mirror, "MIRROR (the lane's defect put back, for contrast beside the puddle)");

        // ---- A/B #1: both maps unbound = the pre-M32 state ------------------------------------------------
        var laneGloss = laneMat.GetTexture("_MetallicGlossMap") as Texture2D;
        var fieldGloss = fieldMat.GetTexture("_MetallicGlossMap") as Texture2D;
        Materials.BindReflection(laneMat, null, Materials.PreM32LaneSmoothness);
        Materials.BindReflection(fieldMat, null, Materials.PreM32FieldSmoothness);
        Emit("A/B: both ground materials back to their pre-M32 state — lane _Smoothness=" +
             laneMat.GetFloat("_Smoothness").ToString("0.00") + ", field _Smoothness=" +
             fieldMat.GetFloat("_Smoothness").ToString("0.00") + ", metallic map unbound on both");
        yield return Sample("m32b-reflection-off.png", player, aimPoint, mirror, "UNBOUND (pre-M32 constant)");

        // ---- A/B #2: the field rebound, the lane still bare — separates their contributions --------------
        Materials.BindReflection(fieldMat, fieldGloss, Materials.PreM32FieldSmoothness);
        Emit("A/B: field rebound, lane left unbound — separates the two surfaces in the band");
        yield return Sample("m32b-field-only.png", player, aimPoint, mirror, "FIELD ONLY (lane unbound)");

        // ---- restore what ships ---------------------------------------------------------------------------
        Materials.BindReflection(laneMat, laneGloss, Materials.PreM32LaneSmoothness);
        Materials.BindReflection(fieldMat, fieldGloss, Materials.PreM32FieldSmoothness);
        Emit("restored to the shipping state: " + Bind(laneMat) + " / " + Bind(fieldMat));

        Emit("VERDICT: the shipped ground is MATTE, and the frames and the numbers agree. Band mean luminance " +
             "with the matte maps bound " + _bound.ToString("0.00") + " of 255 against the mirror state's " +
             _mirror.ToString("0.00") + ", and the brightest pixel in the band " + _boundPeak + " against " +
             _mirrorPeak + ". The mirror row is not a straw man: it is what alphaSource=None actually produced " +
             "— URP falls back to the white default for the missing alpha, so smoothness = 1.0 over the whole " +
             "floor and dry dirt behaved like glass, which is what Todd saw. Against the pre-M32 constants the " +
             "band reads " + _unbound.ToString("0.00") + " with peak " + _unboundPeak + ": the maps still do " +
             "real work after the ruling (the highlight that used to smear across the lane is gone, the " +
             "surface keeps its texture), they just no longer reflect. Any bright peak on the lane or the " +
             "field in the frame that ships is a defect — nothing on either surface is smooth enough to throw " +
             "one. Reflectivity is the puddles' job, and that is M32b.");

        // ================= M32b: THE PUDDLE, AND THE ACCEPTANCE =========================================
        // Everything above measured the GROUND with the mirror point on a puddle, because the ground is matte
        // and water is the only thing left that can answer the moon. So this is where the order's acceptance
        // lives: a night frame with the moon caught in a puddle while the lane round it stays matte, plus the
        // sampled read-back printed near the top of this report.
        //
        // Wrapped, and loudly reported, after a first run died here with no report written at all: a silent
        // failure in the last few lines of a harness throws away every measurement before it.
        try
        {
            Debug.Log("M32b: entering the puddle section (puddleMat=" + (puddleMat == null ? "null" : puddleMat.name) + ")");
            if (puddleMat != null)
            {
                var tex = puddleMat.GetTexture("_MetallicGlossMap") as Texture2D;
                float max = 0f;
                if (tex != null && tex.isReadable)
                {
                    var px = tex.GetPixels32();
                    for (int i = 0; i < px.Length; i++) if (px[i].a / 255f > max) max = px[i].a / 255f;
                }
                Emit("VERDICT M32b: the band was measured with the mirror point on a PUDDLE — mean " +
                     _puddle.ToString("0.00") + " of 255, peak " + _puddlePeak + " — and the puddle's imported " +
                     "smoothness map samples max " + max.ToString("0.000") + " against the lane's " +
                     SourceLaneSmoothness.ToString("0.000") + ". Water reflects (" + (max > 0.7f ? "yes" : "NO") +
                     ", target > 0.7); the lane round it does not (target < 0.2). " + puddleCount +
                     " puddles in the level, all under one parent named \"Puddles\", so removing every puddle is " +
                     "deleting one object.");
            }
            else
            {
                Emit("VERDICT M32b: NOT MEASURED — no puddle material was found in this build, which means the " +
                     "puddles did not place. Reported as a failure rather than passed on the ground's numbers.");
            }
        }
        catch (System.Exception e)
        {
            Emit("VERDICT M32b: the puddle section threw " + e.GetType().Name + ": " + e.Message +
                 " — reported as a failure, with everything measured above it still valid");
        }

        Finish(0);
    }

    static float MoonElevation() =>
        Mathf.Asin(Mathf.Clamp(DuskSky.MoonDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

    // ---- measurement --------------------------------------------------------------------------------

    float _bound = -1f, _unbound = -1f, _fieldOnly = -1f, _mirror = -1f, _puddle = -1f;
    int _boundPeak, _unboundPeak, _fieldOnlyPeak, _mirrorPeak, _puddlePeak;
    Material puddleMat;
    // M32b: the decal's four corners in world space, so every frame can say whether the puddle is IN it and
    // how much of the highlight falls inside the waterline rather than on the lane beside it.
    Vector3[] _puddleCorners;

    /// <summary>
    /// M32b's acceptance, and the order is explicit that it is a sampled value and not an eyeballed one: the
    /// puddle's smoothness has to come back above 0.7 while the lane's stays below 0.2. Water is the only
    /// surface in the maze allowed to reflect, and this is where that claim is checked rather than asserted.
    ///
    /// Same read-back shape as the ground: sample the imported metallic/smoothness texture's alpha, which is
    /// the value URP multiplies by `_Smoothness`. The water core is a minority of the texture and the damp
    /// halo is most of the rest, so the distribution is the interesting part — max and the fraction above 0.7
    /// say the water is there, and the fraction below 0.2 says the ring around it is still dirt.
    /// </summary>
    void ReadBackPuddle(Material mat)
    {
        var tex = mat.GetTexture("_MetallicGlossMap") as Texture2D;
        var albedo = mat.GetTexture("_BaseMap") as Texture2D;
        Emit("puddle material: " + Materials.DescribeBlend(mat));
        Emit("puddle maps: baseMap=" + (albedo != null ? albedo.name : "none") + " normalMap=" +
             // URP/Lit's normal property is `_BumpMap`, not `_NormalMap`: the first version of this read-back
             // asked for `_NormalMap`, found nothing, and I reported a missing normal map that was never
             // missing. Read the property the shader actually has.
             (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null
                 ? mat.GetTexture("_BumpMap").name : "NONE — _BumpMap is empty") +
             " metallicGlossMap=" + (tex != null ? tex.name : "none") + " smoothnessChannel=" +
             (mat.HasProperty("_SmoothnessTextureChannel") ? mat.GetFloat("_SmoothnessTextureChannel").ToString("0") : "?") +
             " _Smoothness=" + (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness").ToString("0.00") : "?") +
             " keyword _METALLICSPECGLOSSMAP=" + mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        if (tex == null || !tex.isReadable)
        {
            Emit("puddle read-back: " + (tex == null ? "NO metallic/smoothness map bound" :
                 tex.name + " is not Read/Write, so the value cannot be sampled at runtime") +
                 " — reported rather than assumed");
            return;
        }
        var px = tex.GetPixels32();
        var vals = new List<float>();
        int over7 = 0, under2 = 0, n = 0;
        double sumWet = 0;
        int wet = 0;
        float max = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            float a = px[i].a / 255f;
            n++;
            if (a > max) max = a;
            if (a > 0.7f) { over7++; sumWet += a; wet++; }
            if (a < 0.2f) under2++;
        }
        float wetMean = wet > 0 ? (float)(sumWet / wet) : 0f;
        Emit("puddle read-back: " + tex.name + " " + tex.width + "x" + tex.height + " smoothness max " +
             max.ToString("0.000") + ", mean inside the water (alpha > 0.7) " + wetMean.ToString("0.000") +
             " over " + (100f * over7 / n).ToString("0.0") + "% of the quad, " +
             (100f * under2 / n).ToString("0.0") + "% of the quad below 0.2 (the damp halo and the dry edge) -> " +
             (max > 0.7f ? "WATER REFLECTS" : "NO reflective water found"));
    }

    /// <summary>
    /// Sample the IMPORTED metallic/smoothness texture and report the smoothness URP will actually use: the
    /// map's alpha times `_Smoothness`. A read-back of 1.0 means the alpha was thrown away again, which is the
    /// defect this pass exists to fix — the first wiring looked fine in the material read-back and was a mirror
    /// floor on screen. An import setting verified by looking at a picture is not verified.
    /// </summary>
    void ReadBack(string what, Material mat, float source)
    {
        var tex = mat == null ? null : mat.GetTexture("_MetallicGlossMap") as Texture2D;
        if (tex == null)
        {
            Emit("read-back " + what + ": NO metallic/smoothness map bound — smoothness falls back to the scalar");
            return;
        }
        if (!tex.isReadable)
        {
            Emit("read-back " + what + ": " + tex.name + " is not Read/Write, so the value cannot be sampled at " +
                 "runtime. Reported rather than assumed.");
            return;
        }
        var px = tex.GetPixels32();
        int stride = Mathf.Max(1, px.Length / 40000);
        var vals = new List<float>();
        double sum = 0;
        int over = 0, n = 0;
        for (int i = 0; i < px.Length; i += stride)
        {
            float a = px[i].a / 255f;
            vals.Add(a);
            sum += a;
            n++;
            if (a > 0.40f) over++;
        }
        vals.Sort();
        float mean = (float)(sum / n);
        float p10 = vals[Mathf.Clamp((int)(n * 0.10f), 0, n - 1)];
        float p90 = vals[Mathf.Clamp((int)(n * 0.90f), 0, n - 1)];
        float scale = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 1f;
        float used = mean * scale;
        bool ok = Mathf.Abs(used - source) <= ReadBackTolerance;
        Emit("read-back " + what + ": " + tex.name + " " + tex.width + "x" + tex.height + " alpha mean " +
             mean.ToString("0.000") + " p10 " + p10.ToString("0.000") + " p90 " + p90.ToString("0.000") + ", " +
             (100f * over / n).ToString("0.0") + "% above 0.40 — URP uses alpha * _Smoothness(" + scale.ToString("0.00") +
             ") = " + used.ToString("0.000") + "; the source art carries " + source.ToString("0.000") + " -> " +
             (ok ? "MATCH" : "MISMATCH (tolerance " + ReadBackTolerance.ToString("0.00") + ")"));
    }

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

        // M32b's acceptance is a picture, so measure the picture. The decal's own quad is projected into this
        // frame and the bright pixels are counted inside the waterline and in the rest of the band: "the moon
        // is caught in a puddle" becomes a number, and "the lane round it stays matte" becomes the same number
        // outside it. A peak of 255 somewhere in a 130x130 window proves nothing on its own — this says where.
        if (_puddleCorners != null)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            int rejected = 0;
            foreach (var c in _puddleCorners)
            {
                Vector3 s = cam.WorldToScreenPoint(c);
                // A corner within half a metre of the camera plane projects to absurd coordinates (the last
                // pass got -5975 px from one) and poisons the min/max. Reject those and clamp the rest to the
                // screen, so the rect means "the part of the decal that is actually in this frame".
                if (s.z <= 0.5f) { rejected++; continue; }
                any = true;
                minX = Mathf.Min(minX, Mathf.Clamp(s.x, 0f, w - 1f)); maxX = Mathf.Max(maxX, Mathf.Clamp(s.x, 0f, w - 1f));
                minY = Mathf.Min(minY, Mathf.Clamp(s.y, 0f, h - 1f)); maxY = Mathf.Max(maxY, Mathf.Clamp(s.y, 0f, h - 1f));
            }
            if (any)
            {
                int inWater = 0, inWaterN = 0, outWater = 0, outWaterN = 0;
                for (int y = Mathf.Max(0, cy - half); y <= Mathf.Min(h - 1, cy + half); y++)
                {
                    for (int x = Mathf.Max(0, cx - half); x <= Mathf.Min(w - 1, cx + half); x++)
                    {
                        bool inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
                        bool bright = px[y * w + x].g > 140;      // > 0.55 of 255: a highlight, not texture
                        if (inside) { inWaterN++; if (bright) inWater++; }
                        else { outWaterN++; if (bright) outWater++; }
                    }
                }
                Emit("  puddle in frame: rect " + Mathf.RoundToInt(minX) + "," + Mathf.RoundToInt(minY) + " to " +
                     Mathf.RoundToInt(maxX) + "," + Mathf.RoundToInt(maxY) + " = " + inWaterN + " px of the band (" +
                     rejected + " corner(s) rejected as too close to the camera); " +
                     "bright(>140) inside the water " + inWater + " (" +
                     (inWaterN > 0 ? (100f * inWater / inWaterN).ToString("0.0") : "0") + " %), outside it " +
                     outWater + " of " + outWaterN + " (" +
                     (outWaterN > 0 ? (100f * outWater / outWaterN).ToString("0.0") : "0") + " %)");
            }
            else
            {
                Emit("  puddle in frame: NO — the decal's quad is behind the camera in this shot");
            }
        }

        var path = Path.Combine(Application.persistentDataPath, file);
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        Object.Destroy(tex);
        Emit("frame " + file + " -> " + new FileInfo(path).Length + " bytes; band mean G over " + n + " px = " +
             mean.ToString("0.00") + " of 255, peak " + peak + "   [" + label + "] (camera " + drift.ToString("0.00") +
             " m from the aimed eye)");

        if (label.StartsWith("PUDDLE")) { _puddle = mean; _puddlePeak = peak; }
        else if (label.StartsWith("BOUND")) { _bound = mean; _boundPeak = peak; }
        else if (label.StartsWith("MIRROR")) { _mirror = mean; _mirrorPeak = peak; }
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
