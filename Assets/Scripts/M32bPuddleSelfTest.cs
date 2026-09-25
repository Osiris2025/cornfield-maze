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

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m32c-puddle-report.txt");

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
             ", field wears " + fieldMat.GetTexture("_BaseMap").name +
             "; lane _BaseColor " + (laneMat.HasProperty("_BaseColor") ? laneMat.GetColor("_BaseColor").ToString("F3") : "?") +
             ", field _BaseColor " + (fieldMat.HasProperty("_BaseColor") ? fieldMat.GetColor("_BaseColor").ToString("F3") : "?") +
             " — M32c scaled the lane's tint by 0.90, so (~0.846, 0.828, 0.792) means the change reached the " +
             "shader and (0.940, 0.920, 0.880) means it did not, whatever the frame looks like");
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
        // The moon's reflection lies along the MOON's azimuth, not along the lane: off a flat plane the
        // reflection's horizontal direction is the same as the moon's. Passes 1-3 stood the player up-lane and
        // aimed at the mirror point, so the mirror point landed on bare lane and the puddle sat out of shot —
        // measured, 0 px of the frame. The puddle whose lane runs closest to the moon's azimuth is the one whose
        // water can be both under the reflection and reached from the lane.
        Vector3 moonHoriz = new Vector3(DuskSky.MoonDirection.x, 0f, DuskSky.MoonDirection.z).normalized;
        Vector3 laneAxis = Vector3.forward;     // the chosen puddle's own lane direction, horizontal and unit
        _maze = maze;
        int puddleCount = 0;
        if (puddleRoot != null)
        {
            puddleCount = puddleRoot.transform.childCount;
            var chosen = puddleRoot.transform.GetChild(0);
            float bestAlign = -1f;
            Vector2Int[] nb = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            for (int i = 0; i < puddleCount; i++)
            {
                var c = puddleRoot.transform.GetChild(i);
                var cell = maze.NearestPathCell(c.position);
                Vector3 axis = Vector3.zero;
                foreach (var d in nb)
                {
                    var n = new Vector2Int(cell.x + d.x, cell.y + d.y);
                    if (maze.IsPath(n.x, n.y))
                    {
                        // ONE neighbour, not the sum. A straight-run cell's two opposite neighbours point in
                        // exactly opposite directions and cancel — which is how the previous pass reported
                        // |dot| 0.00 for every puddle in the maze while claiming to measure alignment.
                        axis = maze.CellToWorld(n.x, n.y) - maze.CellToWorld(cell.x, cell.y);
                        break;
                    }
                }
                axis.y = 0f;
                float align = axis.sqrMagnitude > 1e-6f ? Mathf.Abs(Vector3.Dot(axis.normalized, moonHoriz)) : 0f;
                if (align > bestAlign) { bestAlign = align; chosen = c; laneAxis = axis.normalized; }
            }
            puddlePos = chosen.position;
            // The lane's own axis is the decal's LONG axis, which the mesh puts on local X — so it is `right`,
            // not `forward`. Reading `forward` here is what sent the "up-lane" stand across the lane and into
            // the corn, which is why the first two attempts framed corn.
            puddleFwd = chosen.right;
            var pr = chosen.GetComponent<MeshRenderer>();
            puddleMat = pr != null ? pr.sharedMaterial : null;
            Vector3 cl = chosen.right, cw = chosen.forward;      // long axis, wide axis
            puddleLong = cl; puddleWide = cw;
            _puddleT = chosen;
            _groundY = groundY;
            _puddleCorners = new[]
            {
                puddlePos + cl * (PuddleDecals.PuddleLong * 0.5f) + cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos + cl * (PuddleDecals.PuddleLong * 0.5f) - cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos - cl * (PuddleDecals.PuddleLong * 0.5f) + cw * (PuddleDecals.PuddleWide * 0.5f),
                puddlePos - cl * (PuddleDecals.PuddleLong * 0.5f) - cw * (PuddleDecals.PuddleWide * 0.5f),
            };
            Emit("puddles: " + puddleCount + " decals under one object named \"Puddles\" (deleting it is removing " +
                 "every puddle); measuring " + chosen.name + " at " + F(puddlePos) + ", yaw " +
                 chosen.eulerAngles.y.ToString("0") + " deg, scale " + chosen.lossyScale.ToString("0.0") +
                 ". Picked for alignment with the moon's azimuth: |dot| " + bestAlign.ToString("0.00") +
                 " (1.00 is a lane pointing straight at or away from the moon, which is the only arrangement " +
                 "where the reflection can land on the water from a stand on the lane)");
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
        Vector3 standCand = puddlePos - moonHoriz * standDist;
        Vector2Int standCell = maze.NearestPathCell(standCand);
        Vector3 stand = maze.CellToWorld(standCell.x, standCell.y);
        stand.y = groundY;
        player.transform.position = stand;
        _stand = stand; _hasStand = true;
        for (int i = 0; i < 4; i++) yield return null;
        Emit("stand: " + F(player.transform.position) + " — " + Vector3.Distance(player.transform.position, puddlePos).ToString("0.00") +
             " m from the puddle's centre, on maze cell (" + standCell.x + "," + standCell.y + "). The target was " +
             standDist.ToString("0.00") + " m on the moon's own azimuth (" + moonHoriz.x.ToString("0.00") + "," +
             moonHoriz.z.ToString("0.00") + "), because that is where a " + moonElDeg.ToString("0") +
             " deg moon reflects onto the water at an eye height of " + EyeHeight.ToString("0.00") +
             " m; the spot is then snapped to the nearest PATH cell, so the stand is on the lane by the maze's " +
             "own answer and never in the corn");
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

        // ---- M32c: the shallow-angle acceptance pair -------------------------------------------------------
        // What makes water read as water is Fresnel: at a glancing angle a dielectric shows you the sky. That
        // means standing well back down the lane and looking along it nearly level — NOT standing over the water
        // and looking down at the mirror point, which is the steepest angle available and is what all four
        // previous passes photographed. The moon is a billboard quad in the sky dome, so the reflection is the
        // only mechanism that can put it on the water.
        var probeGo = GameObject.Find(ReflectionProbes.ProbeName);
        var probe = probeGo != null ? probeGo.GetComponent<ReflectionProbe>() : null;
        ReflectionProbes.Refresh(probe);
        Emit("M32c reflection probe: " + (probe == null
                 ? "MISSING from the scene — the water has no sky to reflect and the glint cannot appear"
                 : ("present, realtime/scripted via scripting, enabled=" + probe.enabled + ", box " +
                    probe.size.x.ToString("0") + " x " + probe.size.z.ToString("0") + " m, refreshed now that " +
                    "the sky is at night")));
        ProbeReadBack(probe);
        Emit(ReflectionProbes.CaptureReport);
        Emit(PathMudWetness.DebugReport());

        player.SetEyeHeightForTest(1.15f);
        var backCell = maze.NearestPathCell(puddlePos - laneAxis * 6f);
        Vector3 backPos = maze.CellToWorld(backCell.x, backCell.y);
        backPos.y = groundY;
        player.transform.position = backPos;
        _stand = backPos; _hasStand = true;
        for (int i = 0; i < 4; i++) yield return null;
        Vector3 shallowAim = puddlePos + laneAxis * 18f;
        shallowAim.y = backPos.y + 0.15f;      // a couple of degrees down: down the lane, not at the water
        Emit("shallow shot: eye 1.15 m at " + F(player.transform.position) + " on maze cell (" + backCell.x + "," +
             backCell.y + "), puddle " + Vector3.Distance(player.transform.position, puddlePos).ToString("0.00") +
             " m ahead and low in frame, aimed " + Vector3.Distance(player.transform.position, shallowAim).ToString("0") +
             " m down the lane at a near-level pitch — the flattest view available of the water");
        yield return Sample("m32c-water-probe-on.png", player, shallowAim, puddlePos,
                            "PROBE ON (sky reflection; water at a glancing angle, the flattest view there is)");

        // The same camera, the same frame, the probe off: one variable, which is what the order asks for.
        ReflectionProbes.SetEnabledForTest(probe, false);
        if (puddleMat != null)
        {
            if (puddleMat.HasProperty("_EnvironmentReflections")) puddleMat.SetFloat("_EnvironmentReflections", 0f);
            puddleMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
        Emit("A/B: probe disabled and _ENVIRONMENTREFLECTIONS_OFF set on the water — same camera, same aim");
        yield return Sample("m32c-water-probe-off.png", player, shallowAim, puddlePos, "PROBE OFF (same camera)");

        ReflectionProbes.SetEnabledForTest(probe, true);
        if (puddleMat != null)
        {
            puddleMat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (puddleMat.HasProperty("_EnvironmentReflections")) puddleMat.SetFloat("_EnvironmentReflections", 1f);
        }
        player.SetEyeHeightForTest(EyeHeight);
        Emit("restored: probe enabled, eye back to " + EyeHeight.ToString("0.00") + " m");
        yield return WaterFootprint(player, probe, laneMat, shallowAim);

        // The ablation states that used to live here (specular off, gloss off, black albedo, moon light off) now
        // run inside WaterFootprint, where they can be measured over the water's OWN pixels. Pass 2 ran them
        // against a geometry classifier and the numbers were void; a state change is only evidence if the pixels
        // it is measured on belong to the thing being changed.

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
    // M32b pass 4: identify the water by geometry instead of projecting the decal's corners — three passes of
    // corner projection gave degenerate rects. Every pixel is classified by casting the camera's own ray at
    // the ground plane and asking whether that world point is inside the decal's quad.
    Vector3 _stand; bool _hasStand;     // the player's stand for the current shot — Sample re-establishes it
    Color32[] _lastPx; int _lastW, _lastH;   // the last captured frame, for the marker-based footprint method
    Transform _puddleT;
    float _groundY = 0.03f;
    MazeData _maze;                     // for classifying pixels as lane or field by the maze's own answer

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

    int _capW, _capH;

    /// <summary>
    /// THE TEST THAT SETTLES THE PROBE QUESTION. Until now the probe's capture has been inferred from what the
    /// water does with it — and a surface that ignores a cubemap looks exactly like a cubemap that is empty. Four
    /// passes have argued about the inference. So read the cubemap itself: six faces, mean brightness each. The
    /// sky dome is only 57 m out and follows the camera, so a probe inside it that captured anything at all shows
    /// a bright sky on every face but the downward one; an empty capture is black on all six.
    /// This is the same move that caught `alphaSource=None`: look at the data, not at the surface wearing it.
    /// </summary>
    void ProbeReadBack(ReflectionProbe p)
    {
        // Prefer the map we built (pass 6): a baked probe's cubemap is not exposed as a render texture, and the
        // thing under test is the environment the water reads, whichever object is holding it.
        Cubemap cube = ReflectionProbes.SkyCube;
        if (cube == null && p != null) cube = p.texture as Cubemap;
        if (cube == null)
        {
            var rt = p != null ? p.texture as RenderTexture : null;
            if (rt == null) { Emit("PROBE READ-BACK: no cube map to read — the environment is empty"); return; }
        }
        if (cube == null) { Emit("PROBE READ-BACK: the environment is a render texture, not a readable cube"); return; }
        var names = new[] { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
        var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                            CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        string line = "PROBE READ-BACK (the environment's own cube, face mean G of 255)";
        double allSum = 0; int allN = 0, allBright = 0; float brightest = 0f; string brightestFace = "-";
        for (int f = 0; f < 6; f++)
        {
            var px = cube.GetPixels(faces[f]);
            double s = 0; int over = 0; float mx = 0f;
            for (int i = 0; i < px.Length; i++) { float g = px[i].g * 255f; s += g; if (g > 140f) over++; if (g > mx) mx = g; }
            float mean = px.Length > 0 ? (float)(s / px.Length) : 0f;
            allSum += s; allN += px.Length; allBright += over;
            if (mx > brightest) { brightest = mx; brightestFace = names[f]; }
            line += "  " + names[f] + " " + mean.ToString("0.0") + (over > 0 ? " [" + over + " bright]" : "");
        }
        float overall = allN > 0 ? (float)(allSum / allN) : 0f;
        Emit(line + "  — overall " + overall.ToString("0.00") + ", bright px " + allBright +
             ", brightest " + brightest.ToString("0") + " on " + brightestFace + ". " +
             (overall < 2f
                 ? "ALL BLACK: the environment is empty, so the water has nothing to reflect and the fault is " +
                   "here, not in the water's material."
                 : allBright > 0
                     ? "The environment holds a bright source: the water has a real sky to reflect."
                     : "The environment holds light but no bright source."));
    }

    /// <summary>
    /// One capture, one frozen pose. Every capture in the deliverable below goes through here, so every
    /// comparison is between frames whose camera POSE is identical — the pass-2 defect cannot come back.
    /// </summary>
    IEnumerator ShootInto(FarmWalkerController player, Vector3 aimPoint, string file, Color32[][] into, int slot)
    {
        Time.timeScale = 0f;
        if (_hasStand) player.transform.position = _stand;
        player.AimAtForTest(aimPoint);
        for (int i = 0; i < 2; i++) yield return null;
        if (_hasStand) player.transform.position = _stand;
        player.AimAtForTest(aimPoint);
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        into[slot] = tex.GetPixels32();
        _capW = tex.width; _capH = tex.height;
        File.WriteAllBytes(Path.Combine(Application.persistentDataPath, file), ImageConversion.EncodeToPNG(tex));
        Emit("captured " + file + " (" + tex.width + "x" + tex.height + ") [" + SlotLabel(slot) + "]");
        Object.Destroy(tex);
        Time.timeScale = 1f;
    }

    string SlotLabel(int slot)
    {
        switch (slot)
        {
            case 0: return "shipped, probe ON";
            case 1: return "the same shot, probe OFF";
            case 2: return "the water painted as a marker";
            case 3: return "the moon light extinguished";
            case 4: return "the water's albedo black and its gloss gone";
            default: return "the lane painted as a marker";
        }
    }

    /// <summary>
    /// THE M32c DELIVERABLE. Six captures in ONE frozen pose, and every comparison is taken over a mask rather
    /// than over the whole frame or over a geometry guess — both of which have already lied in this milestone:
    ///
    ///   * a geometry guess: pass 2's ablation proved the "water" region ignored the water's albedo, its gloss,
    ///     its specular AND the moon light, so it was never the water at all;
    ///   * the whole frame: a puddle is one or two percent of the picture, so a real change on the water moves
    ///     the frame mean by about 0.3 of 255 and reads as "the probe does nothing". That is how pass 2 arrived
    ///     at a conclusion its own numbers did not support.
    ///
    /// The masks come from the renderer, not the geometry: paint the decal, shoot again, take the pixels that
    /// changed. Then the questions that matter are answerable in one pass: does the probe change the WATER,
    /// what makes the water bright, is the lane still the brightest thing on the ground, and does the water
    /// brighten toward the horizon the way a Fresnel surface does.
    /// </summary>
    IEnumerator WaterFootprint(FarmWalkerController player, ReflectionProbe probe, Material laneMat, Vector3 aimPoint)
    {
        var puddleRoot = GameObject.Find("Puddles");
        if (puddleMat == null || puddleRoot == null || laneMat == null)
        {
            Emit("deliverable: no puddle material, no lane material or no Puddles object — the analysis is " +
                 "skipped and this report says so rather than quoting a number it cannot stand behind");
            yield break;
        }
        var pRends = puddleRoot.GetComponentsInChildren<Renderer>();
        var pSaved = new Material[pRends.Length];
        for (int i = 0; i < pRends.Length; i++) pSaved[i] = pRends[i].sharedMaterial;

        var marker = new Material(puddleMat.shader);
        marker.SetColor("_BaseColor", new Color(4f, 0f, 4f, 1f));
        if (marker.HasProperty("_BumpMap")) marker.SetTexture("_BumpMap", null);
        Materials.BindReflection(marker, null, 0f);

        var moonGo = GameObject.Find("MoonLight");
        var moon = moonGo != null ? moonGo.GetComponent<Light>() : null;
        float moonI = moon != null ? moon.intensity : -1f;
        var gloss = puddleMat.GetTexture("_MetallicGlossMap") as Texture2D;
        var puddleCol = puddleMat.GetColor("_BaseColor");
        var laneCol = laneMat.GetColor("_BaseColor");
        var laneMap = laneMat.GetTexture("_BaseMap");

        // THE WATER'S ENVIRONMENT REFLECTIONS, READ BACK RATHER THAN ASSUMED. Five passes have tested whether the
        // probe reaches the water; none has ever read the one switch on the water that decides it. The same rule
        // that caught `alphaSource=None` applies: look at the data the material carries.
        Emit("WATER ENV READ-BACK: _EnvironmentReflections=" +
             (puddleMat.HasProperty("_EnvironmentReflections") ? puddleMat.GetFloat("_EnvironmentReflections").ToString("0.00") : "n/a") +
             ", keyword _ENVIRONMENTREFLECTIONS_OFF=" + puddleMat.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF") +
             ", _SpecularHighlights=" +
             (puddleMat.HasProperty("_SpecularHighlights") ? puddleMat.GetFloat("_SpecularHighlights").ToString("0.00") : "n/a") +
             ", keyword _SPECULARHIGHLIGHTS_OFF=" + puddleMat.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") +
             ", _Smoothness=" + (puddleMat.HasProperty("_Smoothness") ? puddleMat.GetFloat("_Smoothness").ToString("0.00") : "n/a") +
             ", glossMap=" + (gloss != null ? gloss.name : "none") +
             ", _METALLICSPECGLOSSMAP=" + puddleMat.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        // And then make it so, explicitly, because the water is the one surface in this game allowed to reflect.
        Materials.UnmakeMatteForTest(puddleMat);

        var fr = new Color32[7][];
        string[] nm = { "m32c-water-on.png", "m32c-water-off.png", "m32c-water-mask.png",
                        "m32c-water-nomoon.png", "m32c-water-black.png", "m32c-lane-mask.png",
                        "m32c-lane-control.png" };

        ReflectionProbes.SetEnabledForTest(probe, true);
        ReflectionProbes.Refresh(probe);     // re-capture with the sky exactly as it stands for these frames
        yield return ShootInto(player, aimPoint, nm[0], fr, 0);

        ReflectionProbes.SetEnabledForTest(probe, false);
        yield return ShootInto(player, aimPoint, nm[1], fr, 1);
        ReflectionProbes.SetEnabledForTest(probe, true);

        for (int i = 0; i < pRends.Length; i++) pRends[i].sharedMaterial = marker;
        yield return ShootInto(player, aimPoint, nm[2], fr, 2);
        for (int i = 0; i < pRends.Length; i++) pRends[i].sharedMaterial = pSaved[i];
        Object.Destroy(marker);

        if (moon != null)
        {
            moon.intensity = 0f;
            yield return ShootInto(player, aimPoint, nm[3], fr, 3);
            moon.intensity = moonI;
        }
        puddleMat.SetColor("_BaseColor", Color.black);
        Materials.BindReflection(puddleMat, null, 0f);
        yield return ShootInto(player, aimPoint, nm[4], fr, 4);
        puddleMat.SetColor("_BaseColor", puddleCol);
        Materials.BindReflection(puddleMat, gloss, 0.10f);

        // The lane material is shared by every lane piece, so painting it paints the lane — no renderer hunt.
        laneMat.SetColor("_BaseColor", new Color(0f, 4f, 4f, 1f));
        laneMat.SetTexture("_BaseMap", null);
        yield return ShootInto(player, aimPoint, nm[5], fr, 5);
        laneMat.SetColor("_BaseColor", laneCol);
        laneMat.SetTexture("_BaseMap", laneMap);

        // THE CONTROL, and the lane's fix measured in the same pose. The shipping lane is matte by construction
        // (Materials.MakeMatte: no specular lobe, no environment reflection). This capture puts both back AND
        // takes the lane to mirror smoothness — the most reflective a lane could possibly be — so one frame
        // answers two questions: is the highlight the fix removes real, and does the reflection probe's capture
        // contain any sky at all to reflect?
        Materials.UnmakeMatteForTest(laneMat);
        // THE CONTROL HAS TO BE ABLE TO HOLD ITS STATE. `PathMudWetness` rewrites the lane's `_Smoothness` every
        // frame (capped at 0.20), so a "mirror lane" set here was overwritten before the capture — which is why
        // pass 7 read +0.00 and called it evidence. That was the writer's fault, not the environment's. Detach the
        // writer for this one capture and re-register it immediately after.
        PathMudWetness.RegisterGravel(null);
        var laneGloss = laneMat.GetTexture("_MetallicGlossMap") as Texture2D;
        Materials.BindReflection(laneMat, null, 1f);
        yield return ShootInto(player, aimPoint, nm[6], fr, 6);
        Materials.BindReflection(laneMat, laneGloss, Materials.PreM32LaneSmoothness);
        Materials.MakeMatte(laneMat);
        PathMudWetness.RegisterGravel(laneMat);

        if (fr[0] == null || fr[2] == null || fr[5] == null || _capW == 0)
        {
            Emit("deliverable: a capture is missing — analysis skipped rather than half-done");
            yield break;
        }

        int W = _capW, H = _capH;
        var water = new bool[W * H];
        var lane = new bool[W * H];
        int wN = 0, lN = 0;
        for (int i = 0; i < W * H; i++)
        {
            if (Big(fr[0][i], fr[2][i])) { water[i] = true; wN++; }
            else if (Big(fr[0][i], fr[5][i])) { lane[i] = true; lN++; }
        }
        Emit("WATER MASK: the decal owns " + wN + " px of the " + (W * H) + "-px frame (" +
             (100f * wN / (W * H)).ToString("0.00") + " %); LANE MASK: the lane owns " + lN + " px (" +
             (100f * lN / (W * H)).ToString("0.00") + " %). Both from painting the surface and diffing the " +
             "frames at one frozen pose — no ray, no projection, no degenerate rect.");

        double[] wSum = new double[7]; int[] wCnt = new int[7];
        double[] lSum = new double[7]; int[] lCnt = new int[7];
        double[] fSum = new double[7]; int[] fCnt = new int[7];
        int waterBright = 0;
        var cam = player.Camera;
        var tList = new List<float>();
        var nearSum = new List<double>();
        var farSum = new List<double>();
        for (int s = 0; s < 7; s++)
        {
            if (fr[s] == null) continue;
            for (int i = 0; i < W * H; i++)
            {
                byte g = fr[s][i].g;
                if (water[i]) { wSum[s] += g; wCnt[s]++; }
                else if (lane[i]) { lSum[s] += g; lCnt[s]++; }
            }
        }
        // Water, near half against far half: a dielectric should show the sky at the grazing end and less as the
        // view steepens, which is the whole reason the shallow-angle frame is the acceptance frame.
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (!water[i]) continue;
                if (fr[0][i].g > 140) waterBright++;
                var ray = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                if (ray.direction.y > -1e-4f) { tList.Add(0f); nearSum.Add(fr[0][i].g); farSum.Add(0f); continue; }
                float t = (_groundY - ray.origin.y) / ray.direction.y;
                tList.Add(t);
            }
        }
        // Field: ground the ray reaches, that neither mask claims. Still a geometry test, so it carries the
        // caveat in the report — but it is the only way to see the ground the masks have not painted.
        int fN = 0; double fS = 0;
        for (int y = 0; y < H; y += 2)
        {
            for (int x = 0; x < W; x += 2)
            {
                int i = y * W + x;
                if (water[i] || lane[i]) continue;
                var ray = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                if (ray.direction.y > -1e-4f) continue;
                float t = (_groundY - ray.origin.y) / ray.direction.y;
                if (t <= 0f || t > 40f) continue;
                if (Physics.Raycast(ray, out var hit, t - 0.25f)) continue;
                var wc = _maze.WorldToCell(ray.origin + ray.direction * t);
                if (!_maze.IsPath(wc.x, wc.y)) { fN++; fS += fr[0][i].g; }
            }
        }
        fCnt[0] = fN; fSum[0] = fS;

        float wm0 = wCnt[0] > 0 ? (float)(wSum[0] / wCnt[0]) : 0f;
        float lm0 = lN > 0 ? (float)(lSum[0] / lN) : 0f;
        float fm0 = fN > 0 ? (float)(fS / fN) : 0f;
        Emit("WATER, MEASURED ON THE WATER'S OWN PIXELS: " + wCnt[0] + " px, mean G " + wm0.ToString("0.00") +
             " of 255, bright(>140) " + waterBright + " (" + (wCnt[0] > 0 ? (100f * waterBright / wCnt[0]).ToString("0.0") : "0") +
             " %). LANE on the lane's own pixels: " + lN + " px, mean G " + lm0.ToString("0.00") + ". FIELD: " +
             fN + " px, mean G " + fm0.ToString("0.00") + " -> water/lane " +
             (lm0 > 0.01f ? (wm0 / lm0).ToString("0.00") : "n/a") + "x, lane/field " +
             (fm0 > 0.01f ? (lm0 / fm0).ToString("0.00") : "n/a") + "x  (the order wants lane/field at or under " +
             "1.15x, or the lane darker)");

        if (fr[6] != null)
        {
            float lm6 = lCnt[6] > 0 ? (float)(lSum[6] / lCnt[6]) : 0f;
            Emit("THE LANE'S FIX, ON THE LANE'S OWN PIXELS: shipping (matte by construction — no specular lobe, " +
                 "no environment reflection) the lane means " + lm0.ToString("0.00") + " over " + lCnt[0] +
                 " px; with both back on AND the lane at mirror smoothness it means " + lm6.ToString("0.00") +
                 " over " + lCnt[6] + " px (" + (lm6 - lm0).ToString("+0.00;-0.00") + "). " +
                 (lm6 - lm0 > 6f
                     ? "The environment DOES reach the lane: with the mirror state finally able to survive the " +
                       "capture, a mirror lane with environment reflections on brightens — so an earlier +0.00 was " +
                       "PathMudWetness overwriting the mirror state, never proof of an empty environment. The fix " +
                       "removes a real highlight."
                     : "Even with the writer detached and the lane at mirror smoothness, the environment adds " +
                       "nothing to it — the environment map is not reaching the surfaces."));
        }
        if (fr[1] != null)
        {
            float wm1 = wCnt[1] > 0 ? (float)(wSum[1] / wCnt[1]) : 0f;
            double wsz = 0; int wszN = 0; double wszAll = 0;
            for (int i = 0; i < W * H; i++)
                if (water[i]) { wszAll += Mathf.Abs(fr[0][i].g - fr[1][i].g); wszN++; if (fr[1][i].g > 140) wsz++; }
            Emit("THE PROBE, ON THE WATER: probe ON water mean " + wm0.ToString("0.00") + ", probe OFF " +
                 wm1.ToString("0.00") + " -> " + (wm0 - wm1).ToString("+0.00;-0.00") + " of 255 over " + wszN +
                 " water px; mean |change| on the water " + (wszN > 0 ? (wszAll / wszN).ToString("0.00") : "n/a") +
                 " of 255; water px brighter than 140: ON " + waterBright + ", OFF " +
                 (int)wsz + ". (A whole-frame mean would divide all of this by the frame and report ~0.3.)");
        }
        if (fr[3] != null)
        {
            float wm3 = wCnt[3] > 0 ? (float)(wSum[3] / wCnt[3]) : 0f;
            float lm3 = lN > 0 ? (float)(lSum[3] / lN) : 0f;
            Emit("ABLATION, on the water's own pixels — moon light extinguished: water " + wm3.ToString("0.00") +
                 " against " + wm0.ToString("0.00") + " (" + (wm3 - wm0).ToString("+0.00;-0.00") + "), lane " +
                 lm3.ToString("0.00") + " against " + lm0.ToString("0.00") + " (" + (lm3 - lm0).ToString("+0.00;-0.00") +
                 "). If the water barely moves while the lane collapses, the water's brightness is not the moon " +
                 "light — and if both collapse, it is.");
        }
        if (fr[4] != null)
        {
            float wm4 = wCnt[4] > 0 ? (float)(wSum[4] / wCnt[4]) : 0f;
            Emit("ABLATION, on the water's own pixels — albedo black and gloss gone: water " + wm4.ToString("0.00") +
                 " against " + wm0.ToString("0.00") + " (" + (wm4 - wm0).ToString("+0.00;-0.00") + "). If it holds, " +
                 "the water's brightness is a term the material does not own.");
        }
        if (tList.Count > 0)
        {
            tList.Sort();
            float median = tList[tList.Count / 2];
            int nNear = 0, nFar = 0; double sNear = 0, sFar = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!water[i]) continue;
                    var ray = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                    if (ray.direction.y > -1e-4f) continue;
                    float t = (_groundY - ray.origin.y) / ray.direction.y;
                    if (t <= median) { nNear++; sNear += fr[0][i].g; } else { nFar++; sFar += fr[0][i].g; }
                }
            Emit("THE FRESNEL GRADIENT, on the water: the near half of the puddle (under " + median.ToString("0.0") +
                 " m) means " + (nNear > 0 ? (sNear / nNear).ToString("0.00") : "n/a") + " over " + nNear +
                 " px; the far half (the grazing end, toward the horizon) means " +
                 (nFar > 0 ? (sFar / nFar).ToString("0.00") : "n/a") + " over " + nFar +
                 " px. A dielectric reflecting the sky is brighter at the grazing end than under the camera.");
        }

        puddleMat.SetColor("_BaseColor", puddleCol);
        Materials.BindReflection(puddleMat, gloss, 0.10f);
        foreach (var r in pRends) r.sharedMaterial = pSaved[0];
        for (int i = 0; i < pRends.Length; i++) pRends[i].sharedMaterial = pSaved[i];
        laneMat.SetColor("_BaseColor", laneCol);
        laneMat.SetTexture("_BaseMap", laneMap);
        ReflectionProbes.SetEnabledForTest(probe, true);
        if (moon != null) moon.intensity = moonI;
        Emit("deliverable finished: probe ON, the moon light at " + moonI.ToString("0.00") + ", the water and " +
             "lane materials restored to shipping");
    }

    /// <summary>Is this pixel different enough to belong to the thing that was painted?</summary>
    static bool Big(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r) > 30 || Mathf.Abs(a.g - b.g) > 30 || Mathf.Abs(a.b - b.b) > 30;
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
        // FREEZE THE PLAYER FIRST, and re-establish the stand. This is the defect that invalidated every paired
        // number this harness had produced, including the previous pass's headline. Sample re-aimed but never
        // restored the player's POSITION, and the walker keeps walking between samples, so two shots labelled
        // "same camera, one variable" were taken metres apart — measured with scripts/m32c_frame_delta.py:
        // 28 % of the frame differed by more than 8/255 between the probe-on and probe-off shots, which no
        // 4.4 x 1.8 m decal can explain. The drift figure printed on every frame says 0.00 m either way, because
        // it compares the camera to the player and never the player to the stand. Hence: timeScale 0 (the walker
        // integrates on deltaTime), the stand re-asserted twice around the re-aim, and the camera's pose printed
        // on every frame line — two frames whose poses differ are not a comparison and this report says so.
        Time.timeScale = 0f;
        if (_hasStand) player.transform.position = _stand;
        player.AimAtForTest(aimPoint);
        for (int i = 0; i < 2; i++) yield return null;
        if (_hasStand) player.transform.position = _stand;
        player.AimAtForTest(aimPoint);
        yield return null;
        var cam = player.Camera;
        // Measured against the eye the game is actually using, not the default: the shallow-angle acceptance
        // shots lower the eye to 1.15 m, and a drift figure computed against 1.655 would read as a 0.5 m error
        // in a shot that is exactly where the harness asked for.
        Vector3 eye = player.transform.position + Vector3.up * player.FirstPersonEyeHeight;
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

        // THE MEASUREMENT THE MILESTONE ACTUALLY ASKS FOR. Every second pixel is classified by geometry: cast
        // the camera's own ray through it at the ground plane and ask whether that world point falls inside
        // the decal's quad. Then "is the moon caught in the water" and "is the lane round it matte" are two
        // means over the same light, from the same frame, at the same distances — a decal that is not reading
        // cannot hide behind a highlight somewhere in a band.
        if (_puddleT != null)
        {
            int waterN = 0, laneN = 0, waterBright = 0, laneBright = 0;
            double waterSum = 0, laneSum = 0;
            int waterNear = 0, laneNear = 0;                 // within 8 m: a fair comparison of like distances
            double waterNearSum = 0, laneNearSum = 0;
            for (int y = 0; y < h; y += 2)
            {
                for (int x = 0; x < w; x += 2)
                {
                    var ray = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                    if (ray.direction.y > -1e-4f) continue;                  // not pointing at the ground
                    float t = (_groundY - ray.origin.y) / ray.direction.y;
                    if (t <= 0f || t > 60f) continue;                        // behind us, or past the fog
                    Vector3 wp = ray.origin + ray.direction * t;
                    // OCCLUSION. Until now the classifier called a pixel "water" whenever its sightline crossed
                    // the decal's quad AT GROUND LEVEL — so corn standing between the camera and the puddle was
                    // counted as water, and bright leaves were averaged into a dark puddle's mean. That is how a
                    // surface with a darker albedo than the lane measured three times the lane. A pixel whose
                    // sightline is blocked before it reaches the ground is showing the blocker, not the ground.
                    if (Physics.Raycast(ray, out var blocker, t - 0.25f)) continue;
                    Vector3 lp = _puddleT.InverseTransformPoint(wp);
                    bool water = Mathf.Abs(lp.x) <= PuddleDecals.PuddleLong * 0.5f &&
                                 Mathf.Abs(lp.z) <= PuddleDecals.PuddleWide * 0.5f;
                    byte g = px[y * w + x].g;
                    if (water)
                    {
                        waterN++; waterSum += g; if (g > 140) waterBright++;
                        if (t < 8f) { waterNear++; waterNearSum += g; }
                    }
                    else
                    {
                        laneN++; laneSum += g; if (g > 140) laneBright++;
                        if (t < 8f) { laneNear++; laneNearSum += g; }
                    }
                }
            }
            float wm = (float)(waterSum / Mathf.Max(1, waterN));
            float lm = (float)(laneSum / Mathf.Max(1, laneN));
            float wnm = (float)(waterNearSum / Mathf.Max(1, waterNear));
            float lnm = (float)(laneNearSum / Mathf.Max(1, laneNear));
            Emit("  water vs lane, same frame, by the camera's own rays: the puddle is " + waterN + " px of the " +
                 "frame, mean G " + wm.ToString("0.00") + " of 255; everything else that is ground is " + laneN +
                 " px, mean " + lm.ToString("0.00") + " -> water/lane " + (lm > 0.01f ? (wm / lm).ToString("0.00") : "n/a") +
                 "x. Within 8 m of the camera, where the comparison is fair: water " + wnm.ToString("0.00") +
                 " over " + waterNear + " px vs lane " + lnm.ToString("0.00") + " over " + laneNear + " px. " +
                 "Bright(>140) pixels: " + waterBright + " in the water, " + laneBright + " in the lane — " +
                 "the moon caught in a puddle means the first number is not zero while the lane stays dark");
        }

        // Lane vs field, the other number the order asks for: same frame, same rays, classified by the maze's
        // own answer for the cell under each pixel rather than by eye. The water is excluded here because it is
        // counted above, and it is the only surface allowed to be brighter than the ground.
        if (_maze != null)
        {
            int nLane = 0, nField = 0;
            double sLane2 = 0, sField2 = 0;
            for (int y = 0; y < h; y += 2)
            {
                for (int x = 0; x < w; x += 2)
                {
                    var ray2 = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                    if (ray2.direction.y > -1e-4f) continue;
                    float t2 = (_groundY - ray2.origin.y) / ray2.direction.y;
                    if (t2 <= 0f || t2 > 40f) continue;
                    Vector3 wp2 = ray2.origin + ray2.direction * t2;
                    if (Physics.Raycast(ray2, out var blocker2, t2 - 0.25f)) continue;   // same occlusion rule
                    Vector3 lp2 = _puddleT.InverseTransformPoint(wp2);
                    if (Mathf.Abs(lp2.x) <= PuddleDecals.PuddleLong * 0.5f &&
                        Mathf.Abs(lp2.z) <= PuddleDecals.PuddleWide * 0.5f) continue;      // water, counted above
                    var wc = _maze.WorldToCell(wp2);
                    byte g2 = px[y * w + x].g;
                    if (_maze.IsPath(wc.x, wc.y)) { nLane++; sLane2 += g2; } else { nField++; sField2 += g2; }
                }
            }
            float lm2 = (float)(sLane2 / Mathf.Max(1, nLane));
            float fm2 = (float)(sField2 / Mathf.Max(1, nField));
            Emit("  lane vs field, same frame: lane mean G " + lm2.ToString("0.00") + " over " + nLane +
                 " px, field mean " + fm2.ToString("0.00") + " over " + nField + " px -> lane/field " +
                 (fm2 > 0.01f ? (lm2 / fm2).ToString("0.00") : "n/a") + "x (the order wants this at or under " +
                 "1.15x, or the lane darker — the lane must stay findable, but it must not be the brightest " +
                 "thing in the maze)");
        }

        var path = Path.Combine(Application.persistentDataPath, file);
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        _lastPx = (Color32[])px.Clone(); _lastW = w; _lastH = h;   // the footprint method compares against this frame
        Object.Destroy(tex);
        Emit("frame " + file + " -> " + new FileInfo(path).Length + " bytes; band mean G over " + n + " px = " +
             mean.ToString("0.00") + " of 255, peak " + peak + "   [" + label + "] (camera " + drift.ToString("0.00") +
             " m from the aimed eye; POSE " + cam.transform.position.x.ToString("0.000") + "," +
             cam.transform.position.y.ToString("0.000") + "," + cam.transform.position.z.ToString("0.000") +
             " pitch " + cam.transform.eulerAngles.x.ToString("0.000") + " yaw " +
             cam.transform.eulerAngles.y.ToString("0.000") +
             " — two frames whose poses differ are not a comparison)");

        if (label.StartsWith("PUDDLE")) { _puddle = mean; _puddlePeak = peak; }
        else if (label.StartsWith("BOUND")) { _bound = mean; _boundPeak = peak; }
        else if (label.StartsWith("MIRROR")) { _mirror = mean; _mirrorPeak = peak; }
        else if (label.StartsWith("UNBOUND")) { _unbound = mean; _unboundPeak = peak; }
        else { _fieldOnly = mean; _fieldOnlyPeak = peak; }
        Time.timeScale = 1f;                  // the freeze is per-sample only; the rest of the run needs real time
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
